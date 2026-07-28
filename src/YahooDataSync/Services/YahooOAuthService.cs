using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using YahooDataSync.Configuration;
using YahooDataSync.Models;

namespace YahooDataSync.Services;

internal sealed class YahooOAuthService(IHttpClientFactory httpClientFactory, YahooOAuthStateStore stateStore, YahooOAuthOptions options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan AuthorizationStateLifetime = TimeSpan.FromMinutes(15);
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly YahooOAuthStateStore _stateStore = stateStore;
    private readonly YahooOAuthOptions _options = options;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset? _accessTokenExpiresAtUtc;

    internal async Task<YahooAuthStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await _authLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await _stateStore.GetAsync(cancellationToken);
            return BuildStatus(snapshot.State);
        }
        finally
        {
            _authLock.Release();
        }
    }

    internal async Task<YahooAuthorizationUrlResponse> CreateAuthorizationUrlAsync(CancellationToken cancellationToken)
    {
        var authorizationState = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(AuthorizationStateLifetime);

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await _stateStore.GetAsync(cancellationToken);
            var updatedState = snapshot.State with
            {
                AuthorizationState = authorizationState,
                AuthorizationStateExpiresAtUtc = expiresAtUtc
            };
            await _stateStore.SaveAsync(updatedState, snapshot.ETag, cancellationToken);
        }
        finally
        {
            _authLock.Release();
        }

        var authorizationUrl = QueryHelpers.AddQueryString(_options.AuthorizationUrl, new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = _options.Scope,
            ["language"] = "en-us",
            ["state"] = authorizationState
        });

        return new YahooAuthorizationUrlResponse(authorizationUrl, _options.RedirectUri, authorizationState);
    }

    internal async Task<YahooAuthStatus> ExchangeAuthorizationCodeAsync(YahooAuthorizationExchangeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (code, returnedState) = ExtractAuthorizationValues(request);

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await _stateStore.GetAsync(cancellationToken);
            ValidateAuthorizationState(snapshot.State, returnedState);

            var tokenResponse = await RequestTokensAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _options.RedirectUri,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            }, cancellationToken);

            var nowUtc = DateTimeOffset.UtcNow;
            var updatedState = ApplyTokenResponse(snapshot.State, tokenResponse, nowUtc) with
            {
                AuthorizationState = null,
                AuthorizationStateExpiresAtUtc = null
            };
            await _stateStore.SaveAsync(updatedState, snapshot.ETag, cancellationToken);
            SetAccessToken(tokenResponse, nowUtc);
            return BuildStatus(updatedState);
        }
        finally
        {
            _authLock.Release();
        }
    }

    internal async Task RejectAuthorizationAsync(string returnedState, CancellationToken cancellationToken)
    {
        await _authLock.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await _stateStore.GetAsync(cancellationToken);
            ValidateAuthorizationState(snapshot.State, returnedState);
            var updatedState = snapshot.State with { AuthorizationState = null, AuthorizationStateExpiresAtUtc = null };
            await _stateStore.SaveAsync(updatedState, snapshot.ETag, cancellationToken);
        }
        finally
        {
            _authLock.Release();
        }
    }

    internal async Task<YahooAuthStatus> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _authLock.WaitAsync(cancellationToken);
        try
        {
            var state = await RefreshAccessTokenInternalAsync(cancellationToken);
            return BuildStatus(state);
        }
        finally
        {
            _authLock.Release();
        }
    }

    internal async Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _accessToken;
            }

            await RefreshAccessTokenInternalAsync(cancellationToken);
            return _accessToken ?? throw new InvalidOperationException("Yahoo did not return an access token.");
        }
        finally
        {
            _authLock.Release();
        }
    }

    internal async Task<string> ForceRefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _authLock.WaitAsync(cancellationToken);
        try
        {
            await RefreshAccessTokenInternalAsync(cancellationToken);
            return _accessToken ?? throw new InvalidOperationException("Yahoo did not return an access token.");
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task<YahooOAuthState> RefreshAccessTokenInternalAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _stateStore.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(snapshot.State.RefreshToken))
        {
            throw new InvalidOperationException("Yahoo refresh token is missing. Complete the authorization flow first.");
        }

        var tokenResponse = await RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = snapshot.State.RefreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        }, cancellationToken);

        var nowUtc = DateTimeOffset.UtcNow;
        var updatedState = ApplyTokenResponse(snapshot.State, tokenResponse, nowUtc);
        await _stateStore.SaveAsync(updatedState, snapshot.ETag, cancellationToken);
        SetAccessToken(tokenResponse, nowUtc);
        return updatedState;
    }

    private async Task<YahooTokenResponse> RequestTokensAsync(IReadOnlyDictionary<string, string> formValues, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("YahooOAuth");
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl) { Content = new FormUrlEncodedContent(formValues) };
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Yahoo token request failed with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<YahooTokenResponse>(stream, SerializerOptions, cancellationToken) ?? throw new InvalidDataException("Yahoo token response could not be parsed.");
    }

    private static YahooOAuthState ApplyTokenResponse(YahooOAuthState state, YahooTokenResponse tokenResponse, DateTimeOffset nowUtc)
    {
        return state with
        {
            RefreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken) ? state.RefreshToken : tokenResponse.RefreshToken,
            TokenType = tokenResponse.TokenType,
            Scope = tokenResponse.Scope,
            IssuedAtUtc = nowUtc,
            LastRefreshedAtUtc = nowUtc
        };
    }

    private static void ValidateAuthorizationState(YahooOAuthState state, string returnedState)
    {
        if (string.IsNullOrWhiteSpace(returnedState))
        {
            throw new InvalidOperationException("Yahoo OAuth state is required.");
        }

        if (string.IsNullOrWhiteSpace(state.AuthorizationState) || state.AuthorizationStateExpiresAtUtc is null)
        {
            throw new InvalidOperationException("There is no pending Yahoo authorization request.");
        }

        if (state.AuthorizationStateExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("The Yahoo authorization request has expired. Start authorization again.");
        }

        var expectedBytes = Encoding.UTF8.GetBytes(state.AuthorizationState);
        var returnedBytes = Encoding.UTF8.GetBytes(returnedState);
        if (expectedBytes.Length != returnedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, returnedBytes))
        {
            throw new InvalidOperationException("Returned Yahoo OAuth state did not match the pending authorization state.");
        }
    }

    private static (string Code, string State) ExtractAuthorizationValues(YahooAuthorizationExchangeRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            if (!string.IsNullOrWhiteSpace(request.RedirectUrl))
            {
                throw new ArgumentException("Provide either Code and State or RedirectUrl, not both.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.State))
            {
                throw new ArgumentException("State is required when exchanging a Yahoo authorization code directly.", nameof(request));
            }

            return (request.Code.Trim(), request.State.Trim());
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUrl))
        {
            throw new ArgumentException("Provide either Code and State or RedirectUrl.", nameof(request));
        }

        if (!Uri.TryCreate(request.RedirectUrl, UriKind.Absolute, out var redirectUri))
        {
            throw new ArgumentException("RedirectUrl must be an absolute URI.", nameof(request));
        }

        var query = QueryHelpers.ParseQuery(redirectUri.Query);
        var code = query["code"].ToString();
        var state = query["state"].ToString();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("RedirectUrl must contain both code and state query values.", nameof(request));
        }

        return (code, state);
    }

    private YahooAuthStatus BuildStatus(YahooOAuthState state)
    {
        return new YahooAuthStatus(
            true,
            !string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAtUtc > DateTimeOffset.UtcNow,
            !string.IsNullOrWhiteSpace(state.RefreshToken),
            _accessTokenExpiresAtUtc,
            state.LastRefreshedAtUtc,
            !string.IsNullOrWhiteSpace(state.AuthorizationState) && state.AuthorizationStateExpiresAtUtc > DateTimeOffset.UtcNow,
            state.Scope);
    }

    private void SetAccessToken(YahooTokenResponse tokenResponse, DateTimeOffset nowUtc)
    {
        _accessToken = tokenResponse.AccessToken;
        _accessTokenExpiresAtUtc = tokenResponse.ExpiresIn > 0 ? nowUtc.AddSeconds(tokenResponse.ExpiresIn) : nowUtc;
    }
}
