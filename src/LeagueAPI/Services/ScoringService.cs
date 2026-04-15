using System.Text.Json;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class ScoringService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task RecalculatePointsAsync(
        LeagueApiDbContext dbContext,
        IReadOnlyCollection<WeeklyPlayerStat> weeklyPlayerStats,
        DateTimeOffset calculatedAtUtc,
        CancellationToken cancellationToken)
    {
        foreach (var weeklyPlayerStat in weeklyPlayerStats)
        {
            if (weeklyPlayerStat.Points.Count > 0)
            {
                dbContext.WeeklyPlayerPoints.RemoveRange(weeklyPlayerStat.Points);
                weeklyPlayerStat.Points.Clear();
            }
        }

        var activeTemplates = await dbContext.ScoringTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .Include(template => template.Rules)
            .ToListAsync(cancellationToken);

        foreach (var weeklyPlayerStat in weeklyPlayerStats)
        {
            var statValuesById = weeklyPlayerStat.StatValues.ToDictionary(statValue => statValue.StatId);

            foreach (var template in activeTemplates)
            {
                decimal fantasyPoints = 0;
                var breakdown = new Dictionary<string, decimal>();

                foreach (var rule in template.Rules)
                {
                    if (!statValuesById.TryGetValue(rule.StatId, out var statValue))
                    {
                        continue;
                    }

                    var contribution = statValue.Value * rule.Modifier;
                    fantasyPoints += contribution;
                    breakdown[rule.StatId.ToString()] = contribution;
                }

                weeklyPlayerStat.Points.Add(new WeeklyPlayerPoint
                {
                    TemplateKey = template.TemplateKey,
                    FantasyPoints = fantasyPoints,
                    BreakdownJson = JsonSerializer.Serialize(breakdown, SerializerOptions),
                    CalculatedAtUtc = calculatedAtUtc
                });
            }
        }
    }
}
