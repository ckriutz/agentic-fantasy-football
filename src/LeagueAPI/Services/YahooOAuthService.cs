using System.Security.Cryptography;
using System.Text.Json;
using LeagueAPI.Configuration;
using LeagueAPI.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace LeagueAPI.Services;

public sealed class YahooOAuthService(
    IHttpClientFactory httpClientFactory,
    JsonFileYahooAuthStateStore authStateStore,
    IOptions<YahooOAuthOptions> yahooOAuthOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly JsonFileYahooAuthStateStore _authStateStore = authStateStore;
    private readonly YahooOAuthOptions _yahooOAuthOptions = yahooOAuthOptions.Value;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    public async Task<YahooAuthStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var state = await _authStateStore.GetStateAsync(cancellationToken);
        return BuildStatus(state);
    }

    public async Task<YahooAuthorizationUrlResponse> CreateAuthorizationUrlAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var oauthState = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        await _authLock.WaitAsync(cancellationToken);

        try
        {
            var state = await _authStateStore.GetStateAsync(cancellationToken);
            state.AuthorizationState = oauthState;
            await _authStateStore.SaveStateAsync(state, cancellationToken);
        }
        finally
        {
            _authLock.Release();
        }

        var authorizationUrl =
            $"{_yahooOAuthOptions.AuthorizationUrl}?client_id={Uri.EscapeDataString(GetClientId())}" +
            $"&redirect_uri={Uri.EscapeDataString(GetRedirectUri())}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(_yahooOAuthOptions.Scope)}" +
            "&language=en-us" +
            $"&state={Uri.EscapeDataString(oauthState)}";

        return new YahooAuthorizationUrlResponse
        {
            AuthorizationUrl = authorizationUrl,
            RedirectUri = GetRedirectUri(),
            State = oauthState
        };
    }

    public async Task<YahooAuthStatus> ExchangeAuthorizationCodeAsync(
        YahooAuthorizationExchangeRequest request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var (authorizationCode, returnedState) = ExtractAuthorizationValues(request);

        await _authLock.WaitAsync(cancellationToken);

        try
        {
            var state = await _authStateStore.GetStateAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(returnedState)
                && !string.IsNullOrWhiteSpace(state.AuthorizationState)
                && !string.Equals(returnedState, state.AuthorizationState, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Returned Yahoo OAuth state did not match the stored authorization state.");
            }

            var tokenResponse = await RequestTokensAsync(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = authorizationCode,
                    ["redirect_uri"] = GetRedirectUri(),
                    ["client_id"] = GetClientId(),
                    ["client_secret"] = GetClientSecret()
                },
                cancellationToken);

            ApplyTokenResponse(state, tokenResponse, DateTimeOffset.UtcNow);
            state.AuthorizationState = null;

            await _authStateStore.SaveStateAsync(state, cancellationToken);
            return BuildStatus(state);
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task<YahooAuthStatus> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var refreshedState = await RefreshAccessTokenStateAsync(cancellationToken);
        return BuildStatus(refreshedState);
    }

    public async Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        await _authLock.WaitAsync(cancellationToken);

        try
        {
            var state = await _authStateStore.GetStateAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(state.AccessToken)
                && state.AccessTokenExpiresAtUtc is DateTimeOffset expiresAtUtc
                && expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return state.AccessToken;
            }

            if (!string.IsNullOrWhiteSpace(state.AccessToken)
                && string.IsNullOrWhiteSpace(state.RefreshToken)
                && state.AccessTokenExpiresAtUtc is null)
            {
                return state.AccessToken;
            }

            var refreshedState = await RefreshAccessTokenStateInternalAsync(state, cancellationToken);
            return refreshedState.AccessToken
                ?? throw new InvalidOperationException("Yahoo access token was not present after refresh.");
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task<string> ForceRefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var refreshedState = await RefreshAccessTokenStateAsync(cancellationToken);
        return refreshedState.AccessToken
            ?? throw new InvalidOperationException("Yahoo access token was not present after refresh.");
    }

    private async Task<YahooOAuthState> RefreshAccessTokenStateAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        await _authLock.WaitAsync(cancellationToken);

        try
        {
            var state = await _authStateStore.GetStateAsync(cancellationToken);
            return await RefreshAccessTokenStateInternalAsync(state, cancellationToken);
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task<YahooOAuthState> RefreshAccessTokenStateInternalAsync(
        YahooOAuthState state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.RefreshToken))
        {
            throw new InvalidOperationException("Yahoo refresh token is missing. Complete the authorization flow first.");
        }

        var tokenResponse = await RequestTokensAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = state.RefreshToken,
                ["client_id"] = GetClientId(),
                ["client_secret"] = GetClientSecret()
            },
            cancellationToken);

        ApplyTokenResponse(state, tokenResponse, DateTimeOffset.UtcNow);
        await _authStateStore.SaveStateAsync(state, cancellationToken);

        return state;
    }

    private async Task<YahooTokenResponse> RequestTokensAsync(
        IReadOnlyDictionary<string, string> formValues,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("YahooOAuth");

        using var request = new HttpRequestMessage(HttpMethod.Post, _yahooOAuthOptions.TokenUrl)
        {
            Content = new FormUrlEncodedContent(formValues)
        };

        request.Headers.Accept.ParseAdd("application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Yahoo token request failed with status {(int)response.StatusCode}: {payload}");
        }

        return JsonSerializer.Deserialize<YahooTokenResponse>(payload, SerializerOptions)
            ?? throw new InvalidOperationException("Yahoo token response could not be parsed.");
    }

    private static void ApplyTokenResponse(
        YahooOAuthState state,
        YahooTokenResponse tokenResponse,
        DateTimeOffset nowUtc)
    {
        state.AccessToken = tokenResponse.AccessToken;
        state.RefreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? state.RefreshToken
            : tokenResponse.RefreshToken;
        state.TokenType = tokenResponse.TokenType;
        state.ExpiresInSeconds = tokenResponse.ExpiresIn;
        state.Scope = tokenResponse.Scope;
        state.IssuedAtUtc = nowUtc;
        state.LastRefreshedAtUtc = nowUtc;
        state.AccessTokenExpiresAtUtc =
            tokenResponse.ExpiresIn > 0
                ? nowUtc.AddSeconds(tokenResponse.ExpiresIn)
                : null;
    }

    private YahooAuthStatus BuildStatus(YahooOAuthState state)
    {
        return new YahooAuthStatus
        {
            IsConfigured = IsConfigured(),
            HasAccessToken = !string.IsNullOrWhiteSpace(state.AccessToken),
            HasRefreshToken = !string.IsNullOrWhiteSpace(state.RefreshToken),
            AccessTokenExpiresAtUtc = state.AccessTokenExpiresAtUtc,
            LastRefreshedAtUtc = state.LastRefreshedAtUtc,
            HasPendingAuthorizationState = !string.IsNullOrWhiteSpace(state.AuthorizationState)
        };
    }

    private (string code, string? state) ExtractAuthorizationValues(YahooAuthorizationExchangeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            return (request.Code, null);
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUrl))
        {
            throw new InvalidOperationException("Provide either an authorization code or a redirect URL.");
        }

        var redirectUri = new Uri(request.RedirectUrl, UriKind.Absolute);
        var query = QueryHelpers.ParseQuery(redirectUri.Query);

        if (!query.TryGetValue("code", out var codeValues) || string.IsNullOrWhiteSpace(codeValues[0]))
        {
            throw new InvalidOperationException("No Yahoo authorization code was found in the redirect URL.");
        }

        query.TryGetValue("state", out var stateValues);
        return (codeValues[0]!, stateValues.Count > 0 ? stateValues[0] : null);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured())
        {
            throw new InvalidOperationException(
                "Yahoo OAuth is not configured. Set YahooOAuth:ClientId and YahooOAuth:ClientSecret or the YAHOO_CLIENT_ID and YAHOO_CLIENT_SECRET environment variables.");
        }

        if (string.IsNullOrWhiteSpace(GetRedirectUri()))
        {
            throw new InvalidOperationException("YahooOAuth:RedirectUri must be configured.");
        }

        EnsureAscii(GetClientId(), "ClientId");
        EnsureAscii(GetClientSecret(), "ClientSecret");
    }

    private static void EnsureAscii(string value, string name)
    {
        foreach (var ch in value)
        {
            if (ch > '\x7F')
            {
                throw new InvalidOperationException(
                    $"Yahoo OAuth {name} contains a non-ASCII character '{ch}' (U+{(int)ch:X4}). " +
                    "This usually means the value was copied from a source that replaced letters with Cyrillic look-alikes. " +
                    "Re-copy the value directly from the Yahoo Developer Console.");
            }
        }
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(GetClientId())
            && !string.IsNullOrWhiteSpace(GetClientSecret());
    }

    private string GetClientId()
    {
        return string.IsNullOrWhiteSpace(_yahooOAuthOptions.ClientId)
            ? Environment.GetEnvironmentVariable("YAHOO_CLIENT_ID") ?? string.Empty
            : _yahooOAuthOptions.ClientId;
    }

    private string GetClientSecret()
    {
        return string.IsNullOrWhiteSpace(_yahooOAuthOptions.ClientSecret)
            ? Environment.GetEnvironmentVariable("YAHOO_CLIENT_SECRET") ?? string.Empty
            : _yahooOAuthOptions.ClientSecret;
    }

    private string GetRedirectUri()
    {
        return string.IsNullOrWhiteSpace(_yahooOAuthOptions.RedirectUri)
            ? Environment.GetEnvironmentVariable("YAHOO_REDIRECT_URI") ?? string.Empty
            : _yahooOAuthOptions.RedirectUri;
    }
}
