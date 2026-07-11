using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

internal static class LeagueStateHelper
{
    internal static async Task<LeagueState> GetLeagueStateAsync(HttpClient http, ILogger logger)
    {
        var response = await http.GetAsync("api/league/state");
        response.EnsureSuccessStatusCode();
        var leagueStateJson = await response.Content.ReadAsStringAsync();
        var leagueState = System.Text.Json.JsonSerializer.Deserialize<LeagueState>(leagueStateJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        logger.LogTrace("Successfully retrieved league state. Current phase: " + leagueState.Phase);
        return leagueState;
    }

    internal static async Task<LeagueState?> SetLeagueStateAsync(int? season, int? week, string phase, string updatedBy, HttpClient http, ILogger logger)
    {
        var response = await http.PutAsJsonAsync("api/league/state", new
        {
            season,
            week,
            phase,
            updatedBy
        });

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("Failed to set league state for Season: {Season}, Week: {Week}, Phase: {Phase}. Status code: {StatusCode}. Response: {Response}", season, week, phase, response.StatusCode, error);
            return null;
        }

        var leagueStateJson = await response.Content.ReadAsStringAsync();
        var leagueState = JsonSerializer.Deserialize<LeagueState>(leagueStateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (leagueState == null)
        {
            logger.LogError("League state response was not a JSON object.");
            return null;
        }

        logger.LogInformation("Successfully set league state to Season: {Season}, Week: {Week}, Phase: {Phase}, UpdatedBy: {UpdatedBy}.", season, week, phase, updatedBy);
        return leagueState;
    }
}