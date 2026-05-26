namespace LeagueAPI.Models;

public sealed record UpsertAgentProfileRequest(
    string ModelName,
    string Connection,
    string? TeamName = null,
    bool? IsBootstrapped = null,
    bool? IsEnabled = null);

public sealed record SetAgentTeamNameRequest(string TeamName);

public sealed record SetAgentBootstrapStatusRequest(bool IsBootstrapped);
