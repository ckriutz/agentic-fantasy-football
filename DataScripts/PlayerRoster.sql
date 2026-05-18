SELECT "AgentId",
       players."FullName",
         players."Position",
         players."Team",
         players."ByeWeek",
         roster_assignments."SlotType",
         roster_assignments."AcquiredAtUtc",
         roster_assignments."AcquisitionSource"
FROM public.roster_assignments
JOIN public.players ON players."SleeperPlayerId" = roster_assignments."SleeperPlayerId"
WHERE roster_assignments."AgentId" = 'player-10'
ORDER BY "AcquiredAtUtc"