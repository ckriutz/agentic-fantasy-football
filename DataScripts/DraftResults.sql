--initial draft order: player-08 -> player-10 -> player-05 -> player-04 -> player-07 -> player-02 -> player-09 -> player-03 -> player-06 -> player-01

SELECT 
        roster_assignments."AgentId",
       players."FullName",
       players."Position",
       "AcquiredAtUtc",
       "AcquisitionSource"
FROM public.roster_assignments
JOIN public.players ON players."SleeperPlayerId" = roster_assignments."SleeperPlayerId"
ORDER BY "AcquiredAtUtc"