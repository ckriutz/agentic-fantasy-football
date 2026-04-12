namespace PlayerDatabase.Models;

public sealed class PlayerQuery
{
    public string? Name { get; init; }

    public string? Team { get; init; }

    public string? Position { get; init; }

    public int Limit { get; init; } = 25;
}
