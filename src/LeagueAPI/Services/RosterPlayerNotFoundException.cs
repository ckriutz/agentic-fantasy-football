namespace LeagueAPI.Services;

public sealed class RosterPlayerNotFoundException(string message)
    : InvalidOperationException(message);
