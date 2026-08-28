namespace LeagueAPI.Models;

// --- Requests ---

public sealed record WaiverClaimItem(
    int ClaimOrder,
    string AddSleeperPlayerId,
    string DropSleeperPlayerId);

public sealed record SeedWaiverPriorityRequest(
    IReadOnlyList<string> DraftOrder);

// --- Results ---

public sealed record WaiverClaimResult(
    Guid WaiverClaimId,
    string AgentId,
    int Season,
    int Week,
    int ClaimOrder,
    string AddSleeperPlayerId,
    string? DropSleeperPlayerId,
    int PriorityAtSubmission,
    string Status,
    string? FailureReason,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ProcessedAtUtc);

public sealed record WaiverPriorityEntry(
    string AgentId,
    int Priority,
    DateTimeOffset UpdatedAtUtc);

public sealed record WaiverPriorityResult(
    IReadOnlyList<WaiverPriorityEntry> Priority);

public sealed record ProcessWaiverClaimsResult(
    Guid WaiverProcessRunId,
    int Season,
    int Week,
    string Status,
    int ClaimsProcessed,
    int ClaimsSucceeded,
    int ClaimsFailed,
    string? ErrorMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<WaiverClaimResult> ProcessedClaims);

public sealed record WaiverProcessStatusResult(
    int Season,
    int Week,
    bool HasBeenProcessed,
    Guid? WaiverProcessRunId,
    string? Status,
    int ClaimsSucceeded,
    int ClaimsFailed,
    DateTimeOffset? CompletedAtUtc);

public sealed record AddFreeAgentResult(
    string AgentId,
    string AddedSleeperPlayerId,
    string? DroppedSleeperPlayerId,
    DateTimeOffset AcquiredAtUtc);

public sealed record WaiverPlayerSummary(
    string SleeperPlayerId,
    string? FullName,
    string? Team,
    string? Position);

public sealed record MyWaiverClaimSummary(
    Guid WaiverClaimId,
    int ClaimOrder,
    WaiverPlayerSummary AddPlayer,
    WaiverPlayerSummary? DropPlayer,
    int PriorityAtSubmission,
    string Status,
    string? FailureReason,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ProcessedAtUtc,
    bool WasSuccessful,
    bool WasSuperseded);

public sealed record MyWaiverStatusResult(
    string AgentId,
    int Season,
    int Week,
    string Phase,
    int? MyPriority,
    int TotalAgents,
    bool HasPendingClaims,
    IReadOnlyList<MyWaiverClaimSummary> MyClaims,
    DateTimeOffset? WaiversProcessedAtUtc);
