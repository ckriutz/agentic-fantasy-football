using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public sealed class LeagueRunnerReporter(HttpClient http, ILogger logger)
{
    private readonly HttpClient _http = http;
    private readonly ILogger _logger = logger;
    private readonly Dictionary<string, AgentRunRecord> _agents = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal Guid? RunId { get; private set; }
    internal int Attempts { get; private set; }
    internal int FailedAttempts { get; private set; }

    internal async Task StartAsync(string mode, int? season, int? week, string? phase)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/runner/runs", new { mode, season, week, phase });
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to start runner run. Status code: {StatusCode}. Response: {Response}", response.StatusCode, await response.Content.ReadAsStringAsync());
                return;
            }

            var run = await response.Content.ReadFromJsonAsync<RunnerRunResponse>(JsonOptions);
            RunId = run?.RunnerRunId;
            _logger.LogInformation("Started runner run {RunnerRunId}.", RunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start runner run.");
        }
    }

    internal async Task RecordAgentAsync(string agentId, bool succeeded, string? errorMessage)
    {
        Attempts++;
        if (!succeeded)
            FailedAttempts++;

        if (_agents.TryGetValue(agentId, out var existing))
        {
            succeeded = existing.Succeeded && succeeded;
            errorMessage = Join(existing.ErrorMessage, errorMessage);
        }

        _agents[agentId] = new AgentRunRecord(succeeded, succeeded ? null : errorMessage);
        if (RunId is null)
            return;

        try
        {
            var response = await _http.PostAsJsonAsync($"api/runner/runs/{RunId}/agents", new { agentId, succeeded, errorMessage });
            if (!response.IsSuccessStatusCode)
                _logger.LogError("Failed to record agent {AgentId}. Status code: {StatusCode}. Response: {Response}", agentId, response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record agent {AgentId}.", agentId);
        }
    }

    internal async Task CompleteAsync(string status, string? summary, string? errorMessage)
    {
        if (RunId is null)
        {
            _logger.LogError("Cannot complete runner run because it was not started. Status: {Status}. Summary: {Summary}", status, summary);
            return;
        }

        try
        {
            var response = await _http.PostAsJsonAsync($"api/runner/runs/{RunId}/complete", new { status, summary, errorMessage });
            if (!response.IsSuccessStatusCode)
                _logger.LogError("Failed to complete runner run {RunnerRunId}. Status code: {StatusCode}. Response: {Response}", RunId, response.StatusCode, await response.Content.ReadAsStringAsync());
            else
                _logger.LogInformation("Completed runner run {RunnerRunId} as {Status}.", RunId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete runner run {RunnerRunId}.", RunId);
        }
    }

    private static string? Join(string? first, string? second)
    {
        var parts = new[] { first, second }.Where(value => !string.IsNullOrWhiteSpace(value));
        var joined = string.Join("; ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private sealed record AgentRunRecord(bool Succeeded, string? ErrorMessage);
    private sealed class RunnerRunResponse { public Guid RunnerRunId { get; set; } }
}

public sealed record SeasonRunResult(string Status, string Summary);
