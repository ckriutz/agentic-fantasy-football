using System.ComponentModel;
using LeagueAPI.Models;
using LeagueAPI.Services;
using ModelContextProtocol.Server;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class LeagueStateTools(ILeagueStateService leagueStateService)
{
    private readonly ILeagueStateService _leagueStateService = leagueStateService;

    [McpServerTool, Description("Get the current persisted league state, including season, week, and league phase.")]
    public Task<LeagueState> GetLeagueState()
    {
        return _leagueStateService.GetLeagueStateAsync(CancellationToken.None);
    }
}
