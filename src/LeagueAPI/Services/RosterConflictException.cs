namespace LeagueAPI.Services;

public sealed class RosterConflictException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
