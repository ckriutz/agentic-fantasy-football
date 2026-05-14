namespace LeagueAPI.Services;

internal static class RosterSlotRules
{
    public const string BenchSlot = "BN";

    public const int MaxRosterSize = 16;

    public static readonly IReadOnlyList<string> StarterSlots =
    [
        "QB1",
        "RB1",
        "RB2",
        "WR1",
        "WR2",
        "TE1",
        "FLEX1",
        "K1",
        "DEF1"
    ];

    public static readonly IReadOnlyList<string> AllSlotTypes = [.. StarterSlots, BenchSlot];

    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedPositionsBySlot =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["QB1"] = new HashSet<string>(StringComparer.Ordinal) { "QB" },
            ["RB1"] = new HashSet<string>(StringComparer.Ordinal) { "RB" },
            ["RB2"] = new HashSet<string>(StringComparer.Ordinal) { "RB" },
            ["WR1"] = new HashSet<string>(StringComparer.Ordinal) { "WR" },
            ["WR2"] = new HashSet<string>(StringComparer.Ordinal) { "WR" },
            ["TE1"] = new HashSet<string>(StringComparer.Ordinal) { "TE" },
            ["FLEX1"] = new HashSet<string>(StringComparer.Ordinal) { "RB", "WR", "TE" },
            ["K1"] = new HashSet<string>(StringComparer.Ordinal) { "K" },
            ["DEF1"] = new HashSet<string>(StringComparer.Ordinal) { "DEF" }
        };

    public static string NormalizeSlotType(string? slotType)
    {
        return string.IsNullOrWhiteSpace(slotType)
            ? BenchSlot
            : slotType.Trim().ToUpperInvariant();
    }

    public static bool IsStarterSlot(string? slotType)
    {
        return AllowedPositionsBySlot.ContainsKey(NormalizeSlotType(slotType));
    }

    public static bool IsBenchSlot(string? slotType)
    {
        return string.Equals(NormalizeSlotType(slotType), BenchSlot, StringComparison.Ordinal);
    }

    public static bool IsKnownSlotType(string? slotType)
    {
        var normalizedSlotType = NormalizeSlotType(slotType);
        return IsBenchSlot(normalizedSlotType) || AllowedPositionsBySlot.ContainsKey(normalizedSlotType);
    }

    public static bool CanPlayerOccupySlot(string? slotType, string? position, string? fantasyPositionsTokenized)
    {
        var normalizedSlotType = NormalizeSlotType(slotType);
        if (IsBenchSlot(normalizedSlotType))
        {
            return true;
        }

        if (!AllowedPositionsBySlot.TryGetValue(normalizedSlotType, out var allowedPositions))
        {
            return false;
        }

        var eligiblePositions = GetEligiblePositions(position, fantasyPositionsTokenized);
        return eligiblePositions.Overlaps(allowedPositions);
    }

    public static IReadOnlyList<string> GetEligibleStarterSlots(string? position, string? fantasyPositionsTokenized)
    {
        var eligiblePositions = GetEligiblePositions(position, fantasyPositionsTokenized);

        return StarterSlots
            .Where(slotType => AllowedPositionsBySlot[slotType].Overlaps(eligiblePositions))
            .ToArray();
    }

    public static bool CanPlayerBeRostered(string? position, string? fantasyPositionsTokenized)
    {
        return GetEligibleStarterSlots(position, fantasyPositionsTokenized).Count > 0;
    }

    private static HashSet<string> GetEligiblePositions(string? position, string? fantasyPositionsTokenized)
    {
        var eligiblePositions = new HashSet<string>(StringComparer.Ordinal);
        var normalizedPrimaryPosition = PlayerRecordFactory.NormalizeToken(position);

        if (!string.IsNullOrWhiteSpace(normalizedPrimaryPosition))
        {
            eligiblePositions.Add(normalizedPrimaryPosition);
        }

        if (!string.IsNullOrWhiteSpace(fantasyPositionsTokenized))
        {
            foreach (var token in fantasyPositionsTokenized.Split(
                         '|',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalizedToken = PlayerRecordFactory.NormalizeToken(token);
                if (!string.IsNullOrWhiteSpace(normalizedToken))
                {
                    eligiblePositions.Add(normalizedToken);
                }
            }
        }

        return eligiblePositions;
    }
}
