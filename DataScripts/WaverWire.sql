SELECT "WaiverClaimId",
       "AgentId",
       "Season",
       "Week",
       "ClaimOrder",
       addPlayer."FullName" as "AddPlayerName",
       dropPlayer."FullName" as "DropPlayerName",
       "PriorityAtSubmission",
       public.waiver_claims."Status",
       "FailureReason",
       "SubmittedAtUtc",
       "ProcessedAtUtc"
FROM public.waiver_claims
JOIN public.players addPlayer ON addPlayer."SleeperPlayerId" = public.waiver_claims."AddSleeperPlayerId"
JOIN public.players dropPlayer ON dropPlayer."SleeperPlayerId" = public.waiver_claims."DropSleeperPlayerId"