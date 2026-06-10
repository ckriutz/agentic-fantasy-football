WITH active_templates AS (
   SELECT "TemplateKey"
   FROM scoring_templates
   WHERE "IsActive" = TRUE
 )
 SELECT
   ra."AgentId",
   p."SleeperPlayerId",
   p."FullName",
   p."Position",
   ra."SlotType",
   p."Team",
   COALESCE(SUM(wpp."FantasyPoints"), 0) AS total_points
 FROM roster_assignments ra
 JOIN players p
   ON p."SleeperPlayerId" = ra."SleeperPlayerId"
  AND p."Active" = TRUE
 LEFT JOIN weekly_player_stats wps
   ON wps."SleeperPlayerId" = p."SleeperPlayerId"
  AND wps."Season" = 2025
  AND wps."Week"   = 1
 LEFT JOIN weekly_player_points wpp
   ON wpp."WeeklyPlayerStatId" = wps."WeeklyPlayerStatId"
  AND wpp."TemplateKey" IN (SELECT "TemplateKey" FROM active_templates)
 WHERE ra."AgentId" = 'player-06'
 GROUP BY
   ra."AgentId",
   p."SleeperPlayerId",
   p."FullName",
   p."Position",
   ra."SlotType",
   p."Team"
 ORDER BY
   p."FullName";
