namespace LeagueAPI.Models;

public sealed record SetLeagueStateRequest(
    int Season,
    int Week,
    string Phase,
    string UpdatedBy);
