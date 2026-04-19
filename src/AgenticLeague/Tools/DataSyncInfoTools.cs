using System.ComponentModel;
using System.Text.RegularExpressions;

public sealed class DataSyncInfoTools
{
    HttpClient httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };

    public DataSyncInfoTools()
    {

    }

    [Description("Checks the current status of the Yahoo data pipeline.")]
    public async Task<string> CheckYahooStatus()
    {
        using var response = await httpClient.GetAsync("api/yahoo/auth/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [Description("Checks the current status of the Sleeper data pipeline.")]
    public async Task<string> CheckSleeperStatus()
    {
        using var response = await httpClient.GetAsync("api/sync/sleeper/latest");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [Description("Checks the current status of the SportsDataIO data pipeline.")]
    public async Task<string> CheckSportsDataIOStatus()
    {
        using var response = await httpClient.GetAsync("api/sync/sportsdata/latest");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
