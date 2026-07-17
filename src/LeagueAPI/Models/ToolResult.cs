namespace LeagueAPI.Models;

public sealed class ToolResult<TResult, TDetails> where TResult : class where TDetails : class
{
    public required bool Ok { get; init; }

    public TResult? Result { get; init; }

    public ToolError<TDetails>? Error { get; init; }

    public static ToolResult<TResult, TDetails> Success(TResult result)
    {
        return new ToolResult<TResult, TDetails>
        {
            Ok = true,
            Result = result
        };
    }

    public static ToolResult<TResult, TDetails> Failure(string code, string message, TDetails? details, string nextStep)
    {
        return new ToolResult<TResult, TDetails>
        {
            Ok = false,
            Error = new ToolError<TDetails>
            {
                Code = code,
                Message = message,
                Details = details,
                NextStep = nextStep
            }
        };
    }
}

public sealed class ToolError<TDetails> where TDetails : class
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public TDetails? Details { get; init; }

    public required string NextStep { get; init; }
}
