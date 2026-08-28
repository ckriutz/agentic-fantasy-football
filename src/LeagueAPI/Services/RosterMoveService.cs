using LeagueAPI.Models;

namespace LeagueAPI.Services;

public sealed class RosterMoveService(IRosterWriter rosterWriter, WaiverService waiverService, LeagueStateService leagueStateService, PlayerGameLockService playerGameLockService)
{
    private readonly IRosterWriter _rosterWriter = rosterWriter;
    private readonly WaiverService _waiverService = waiverService;
    private readonly LeagueStateService _leagueStateService = leagueStateService;
    private readonly PlayerGameLockService _playerGameLockService = playerGameLockService;

    public async Task<RosterMoveResult> MakeRosterMoveAsync(string agentId, string? addSleeperPlayerId, string? dropSleeperPlayerId, string? acquisitionSource, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeRequired(agentId, nameof(agentId));
        var normalizedAddId = NormalizeOptional(addSleeperPlayerId);
        var normalizedDropId = NormalizeOptional(dropSleeperPlayerId);

        if (normalizedAddId is null && normalizedDropId is null)
            throw new ArgumentException("Provide a player to add, a player to drop, or both.");

        var leagueState = await _leagueStateService.GetLeagueStateAsync(cancellationToken);

        return leagueState.Phase switch
        {
            LeagueStatePhases.Drafting => await MakeDraftMoveAsync(leagueState, normalizedAgentId, normalizedAddId, normalizedDropId, acquisitionSource, cancellationToken),
            LeagueStatePhases.WaiverWindow => await SubmitWaiverMoveAsync(leagueState, normalizedAgentId, normalizedAddId, normalizedDropId, cancellationToken),
            LeagueStatePhases.FreeAgency => await MakeFreeAgencyMoveAsync(leagueState, normalizedAgentId, normalizedAddId, normalizedDropId, cancellationToken),
            _ => throw new LeaguePhaseException(
                $"{LeagueStatePhases.Drafting}, {LeagueStatePhases.WaiverWindow}, or {LeagueStatePhases.FreeAgency}",
                leagueState.Phase,
                leagueState.Season,
                leagueState.Week,
                $"Roster moves are not allowed during the '{leagueState.Phase}' phase.")
        };
    }

    private async Task<RosterMoveResult> MakeDraftMoveAsync(LeagueState leagueState, string agentId, string? addSleeperPlayerId, string? dropSleeperPlayerId, string? acquisitionSource, CancellationToken cancellationToken)
    {
        if (addSleeperPlayerId is null)
            throw new ArgumentException("Draft moves require a player to add.", nameof(addSleeperPlayerId));
        if (dropSleeperPlayerId is not null)
            throw new ArgumentException("Players cannot be dropped during the draft.", nameof(dropSleeperPlayerId));

        var normalizedSource = string.IsNullOrWhiteSpace(acquisitionSource) ? "draft" : acquisitionSource.Trim().ToLowerInvariant();
        if (normalizedSource is not ("draft" or "auto-draft"))
            throw new ArgumentException("Draft acquisition source must be 'draft' or 'auto-draft'.", nameof(acquisitionSource));

        await _rosterWriter.AddPlayerToRosterAsync(agentId, addSleeperPlayerId, normalizedSource, cancellationToken);
        return new RosterMoveResult(
            RosterMoveStatuses.Completed,
            leagueState.Phase,
            leagueState.Season,
            leagueState.Week,
            agentId,
            addSleeperPlayerId,
            null,
            null,
            $"Player '{addSleeperPlayerId}' was added to agent '{agentId}' during the draft.");
    }

    private async Task<RosterMoveResult> SubmitWaiverMoveAsync(LeagueState leagueState, string agentId, string? addSleeperPlayerId, string? dropSleeperPlayerId, CancellationToken cancellationToken)
    {
        if (addSleeperPlayerId is null)
            throw new ArgumentException("Waiver moves require a player to add.", nameof(addSleeperPlayerId));

        var claim = await _waiverService.SubmitWaiverClaimForCurrentWeekAsync(agentId, addSleeperPlayerId, dropSleeperPlayerId, cancellationToken);
        return new RosterMoveResult(
            RosterMoveStatuses.PendingWaiver,
            leagueState.Phase,
            leagueState.Season,
            leagueState.Week,
            agentId,
            addSleeperPlayerId,
            dropSleeperPlayerId,
            claim.WaiverClaimId,
            $"Waiver claim submitted for player '{addSleeperPlayerId}'.");
    }

    private async Task<RosterMoveResult> MakeFreeAgencyMoveAsync(LeagueState leagueState, string agentId, string? addSleeperPlayerId, string? dropSleeperPlayerId, CancellationToken cancellationToken)
    {
        if (addSleeperPlayerId is not null)
        {
            var result = await _waiverService.AddFreeAgentForCurrentWeekAsync(agentId, addSleeperPlayerId, dropSleeperPlayerId, cancellationToken);
            return new RosterMoveResult(
                RosterMoveStatuses.Completed,
                leagueState.Phase,
                leagueState.Season,
                leagueState.Week,
                agentId,
                result.AddedSleeperPlayerId,
                result.DroppedSleeperPlayerId,
                null,
                $"Free-agent move completed for player '{result.AddedSleeperPlayerId}'.");
        }

        var lockStatuses = await _playerGameLockService.GetPlayerLockStatusesAsync([dropSleeperPlayerId!], cancellationToken);
        var lockStatus = lockStatuses[dropSleeperPlayerId!];
        if (lockStatus.IsAddDropLocked)
        {
            throw new FreeAgentOperationException(FreeAgentFailureType.DropPlayerLocked, lockStatus.AddDropLockReason ?? "The player cannot be dropped.")
            {
                AgentId = agentId,
                DropSleeperPlayerId = dropSleeperPlayerId,
                LockReason = lockStatus.AddDropLockReason
            };
        }

        await _rosterWriter.RemovePlayerFromRosterAsync(agentId, dropSleeperPlayerId!, cancellationToken);
        return new RosterMoveResult(
            RosterMoveStatuses.Completed,
            leagueState.Phase,
            leagueState.Season,
            leagueState.Week,
            agentId,
            null,
            dropSleeperPlayerId,
            null,
            $"Player '{dropSleeperPlayerId}' was dropped from agent '{agentId}'.");
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
