namespace LeagueAPI.Services;

public class RosterConflictException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
