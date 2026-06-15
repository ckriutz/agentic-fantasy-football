using Microsoft.Extensions.Logging;

public class YahooRunner
{
    private readonly ILogger<YahooRunner> _logger;

    public YahooRunner(ILogger<YahooRunner> logger)
    {
        _logger = logger;
    }

    public async Task CheckYahooStatusAsync()
    {

        using HttpClient httpClient = new() { BaseAddress = new Uri("http://localhost:5000/") };

        // First step, lets hit the League API and get some information and make sure it's working.
        var response = await httpClient.GetAsync("api/yahoo/auth/test-connection");
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully connected to the Yahoo API.");
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Response from Yahoo API: " + content);
        }
        else
        {
            _logger.LogError("Failed to connect to the Yahoo API. Status code: " + response.StatusCode);
            return;
        }

        // Now we check Auth Status.
        var authStatusResponse = await httpClient.GetAsync("api/yahoo/auth/status");
        if (authStatusResponse.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully retrieved Yahoo Auth Status.");
            var content = await authStatusResponse.Content.ReadAsStringAsync();
            _logger.LogInformation("Yahoo Auth Status: " + content); 
        }
        else
        {
            _logger.LogError("Failed to retrieve Yahoo Auth Status. Status code: " + authStatusResponse.StatusCode);
        }
    }
}