using System.Net.Http.Headers;
using System.Text.Json;

public sealed class CreditCheckTool
{
    private static readonly string openRouterEndpoint = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1/";

    public CreditCheckTool()
    {
    }

    public async Task<CreditsResponse> GetRemainingCreditsAsync(string apiKey)
    {
        using var openRouterHttpClient = CreateOpenRouterHttpClient();
        var response = await openRouterHttpClient.GetAsync("");
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseBody);

        var data = json.RootElement.GetProperty("data");
        return new CreditsResponse
        {
            TotalCredits = data.GetProperty("total_credits").GetDouble(),
            TotalUsage = data.GetProperty("total_usage").GetDouble()
        };
    }

    private static HttpClient CreateOpenRouterHttpClient()
    {
        var apiKey = EnvironmentVariableHelper.GetRequired("OPENROUTER_API_KEY");
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{openRouterEndpoint.TrimEnd('/')}/credits")
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/ckriutz/agentic-fantasy-football");
        httpClient.DefaultRequestHeaders.Add("X-Title", "Agentic Fantasy Football");
        return httpClient;
    }
}

public sealed class CreditsResponse
{
    public double TotalCredits { get; set; }
    public double TotalUsage { get; set; }
}