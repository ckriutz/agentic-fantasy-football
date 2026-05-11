SELECT "AgentId",
       players."FullName",
         players."Position",
         players."Team",
         players."ByeWeek",
       "AcquiredAtUtc",
       "AcquisitionSource"
FROM public.roster_assignments
JOIN public.players ON players."SleeperPlayerId" = roster_assignments."SleeperPlayerId"
WHERE roster_assignments."AgentId" = 'player-04'