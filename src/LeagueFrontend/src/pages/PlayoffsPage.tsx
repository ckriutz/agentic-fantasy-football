import { useEffect, useMemo, useState } from 'react'
import { AlertCircle, Check, Crown, Loader2, Lock, Medal, Trophy } from 'lucide-react'

import AgentAvatar from '@/components/AgentAvatar'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { apiBaseUrl } from '@/lib/config'

type LeagueState = {
  season: number
}

type AgentProfile = {
  agentId: string
  teamName: string
  modelName: string
}

type PlayoffSeed = {
  seed: number
  agentId: string
  wins: number
  losses: number
  ties: number
  winningPercentage: number
  pointsFor: number
  pointsAgainst: number
  hasFirstRoundBye: boolean
}

type PlayoffGameStatus = 'pending' | 'scheduled' | 'complete'

type PlayoffGame = {
  round: string
  gameSlot: number
  week: number
  homeSeed: number | null
  awaySeed: number | null
  homeAgentId: string | null
  awayAgentId: string | null
  homeSource: string | null
  awaySource: string | null
  status?: string | null
  winnerAgentId?: string | null
  loserAgentId?: string | null
}

type PlayoffFinalPlacements = {
  championAgentId?: string | null
  runnerUpAgentId?: string | null
  thirdPlaceAgentId?: string | null
  fourthPlaceAgentId?: string | null
}

type PlayoffBracket = {
  season: number
  status?: string | null
  regularSeasonEndWeek: number
  playoffStartWeek: number
  championshipWeek: number
  playoffTeamCount: number
  firstRoundByeCount: number
  seeds?: PlayoffSeed[] | null
  games?: PlayoffGame[] | null
  finalPlacements?: PlayoffFinalPlacements | null
}

type ScheduleMatchup = {
  week: number
  matchupType: string
  homeAgentId: string
  awayAgentId: string
  homePoints: number
  awayPoints: number
  isComplete: boolean
}

type GameScore = {
  homePoints: number
  awayPoints: number
}

type BracketLifecycle = 'projected' | 'locked' | 'complete'

type ParticipantOutcome = 'winner' | 'loser' | 'undecided'

const ROUND_ORDER = ['wild_card', 'semifinal', 'championship', 'third_place']

const ROUND_LABELS: Record<string, string> = {
  wild_card: 'Wild Card',
  semifinal: 'Semifinals',
  championship: 'Championship',
  third_place: 'Third Place',
}

const GAME_STATUS_LABELS: Record<PlayoffGameStatus, string> = {
  pending: 'Awaiting teams',
  scheduled: 'Scheduled',
  complete: 'Final',
}

const GAME_STATUS_STYLES: Record<PlayoffGameStatus, string> = {
  pending: 'border-white/10 bg-white/5 text-slate-300',
  scheduled: 'border-sky-300/30 bg-sky-300/10 text-sky-200',
  complete: 'border-emerald-300/30 bg-emerald-300/10 text-emerald-200',
}

const LIFECYCLE_BADGE_STYLES: Record<BracketLifecycle, string> = {
  projected: 'border-amber-300/30 bg-amber-300/10 text-amber-200',
  locked: 'border-sky-300/30 bg-sky-300/10 text-sky-200',
  complete: 'border-emerald-300/30 bg-emerald-300/10 text-emerald-200',
}

const LIFECYCLE_BADGE_LABELS: Record<BracketLifecycle, string> = {
  projected: 'Projected',
  locked: 'Locked',
  complete: 'Complete',
}

function formatRecord(seed: PlayoffSeed) {
  return seed.ties > 0 ? `${seed.wins}-${seed.losses}-${seed.ties}` : `${seed.wins}-${seed.losses}`
}

function resolveLifecycle(bracket: PlayoffBracket): BracketLifecycle {
  const status = (bracket.status ?? '').toLowerCase()
  if (status === 'complete' || status === 'completed') return 'complete'
  if (status === 'locked') return 'locked'
  if (status === 'projected') return 'projected'
  // Status missing or unrecognized: infer from the data we do have.
  if (bracket.finalPlacements?.championAgentId) return 'complete'
  return (bracket.games ?? []).some((game) => game.winnerAgentId) ? 'locked' : 'projected'
}

function resolveGameStatus(game: PlayoffGame): PlayoffGameStatus {
  const status = (game.status ?? '').toLowerCase()
  if (status === 'complete' || status === 'completed') return 'complete'
  if (status === 'scheduled') return 'scheduled'
  if (status === 'pending') return 'pending'
  if (game.winnerAgentId) return 'complete'
  return game.homeAgentId && game.awayAgentId ? 'scheduled' : 'pending'
}

function resolveOutcome(game: PlayoffGame, agentId: string | null): ParticipantOutcome {
  if (!agentId || resolveGameStatus(game) !== 'complete') return 'undecided'
  if (game.winnerAgentId === agentId) return 'winner'
  if (game.loserAgentId === agentId) return 'loser'
  return game.winnerAgentId ? 'loser' : 'undecided'
}

function describeGameStatus(game: PlayoffGame, status: PlayoffGameStatus) {
  if (status !== 'pending') return GAME_STATUS_LABELS[status]
  return game.homeAgentId || game.awayAgentId ? 'Awaiting opponent' : GAME_STATUS_LABELS.pending
}

function scoreKey(week: number, homeAgentId: string, awayAgentId: string) {
  return `${week}|${homeAgentId}|${awayAgentId}`
}

function findGameScore(game: PlayoffGame, scoresByKey: Map<string, ScheduleMatchup>): GameScore | null {
  if (!game.homeAgentId || !game.awayAgentId) return null

  const direct = scoresByKey.get(scoreKey(game.week, game.homeAgentId, game.awayAgentId))
  if (direct) return hasPlayed(direct) ? { homePoints: direct.homePoints, awayPoints: direct.awayPoints } : null

  const flipped = scoresByKey.get(scoreKey(game.week, game.awayAgentId, game.homeAgentId))
  if (flipped) return hasPlayed(flipped) ? { homePoints: flipped.awayPoints, awayPoints: flipped.homePoints } : null

  return null
}

function hasPlayed(matchup: ScheduleMatchup) {
  return matchup.isComplete || matchup.homePoints > 0 || matchup.awayPoints > 0
}

function formatPoints(points: number | null | undefined) {
  return typeof points === 'number' && Number.isFinite(points) ? points.toFixed(2) : null
}

function resolvePlacements(bracket: PlayoffBracket): PlayoffFinalPlacements {
  const placements = bracket.finalPlacements ?? {}
  if (placements.championAgentId) return placements

  // Backend completion fields may not be present yet; fall back to finished games.
  const games = bracket.games ?? []
  const championship = games.find((game) => game.round === 'championship' && resolveGameStatus(game) === 'complete')
  const thirdPlace = games.find((game) => game.round === 'third_place' && resolveGameStatus(game) === 'complete')

  return {
    championAgentId: placements.championAgentId ?? championship?.winnerAgentId ?? null,
    runnerUpAgentId: placements.runnerUpAgentId ?? championship?.loserAgentId ?? null,
    thirdPlaceAgentId: placements.thirdPlaceAgentId ?? thirdPlace?.winnerAgentId ?? null,
    fourthPlaceAgentId: placements.fourthPlaceAgentId ?? thirdPlace?.loserAgentId ?? null,
  }
}

type ParticipantProps = {
  agentId: string | null
  seed: number | null
  source: string | null
  outcome: ParticipantOutcome
  points: string | null
  agentsById: Map<string, AgentProfile>
}

function Participant({ agentId, seed, source, outcome, points, agentsById }: ParticipantProps) {
  const agent = agentId ? agentsById.get(agentId) : undefined
  const nameClassName = outcome === 'winner'
    ? 'text-emerald-200'
    : outcome === 'loser'
      ? 'text-slate-400'
      : 'text-white'

  return (
    <div className={`flex min-h-14 items-center gap-3 px-3 py-2 ${outcome === 'winner' ? 'bg-emerald-300/5' : ''}`}>
      {agentId && <AgentAvatar agentId={agentId} sizeClassName="size-9" iconClassName="size-5" />}
      <span className="w-5 shrink-0 text-center text-xs font-semibold text-emerald-300">
        {seed ? `#${seed}` : ''}
      </span>
      <div className="min-w-0 flex-1">
        <p className={`flex min-w-0 items-center gap-1.5 text-sm font-semibold ${nameClassName}`}>
          <span className="truncate">{agent?.teamName ?? agentId ?? source ?? 'To be determined'}</span>
          {outcome === 'winner' && <Check aria-hidden="true" className="size-4 shrink-0 text-emerald-300" />}
        </p>
        <p className="truncate text-xs text-slate-400">
          {outcome === 'winner'
            ? 'Winner'
            : outcome === 'loser'
              ? 'Eliminated'
              : agent
                ? agent.agentId
                : source
                  ? 'Advances after prior round'
                  : ''}
        </p>
      </div>
      {points && (
        <span className={`shrink-0 font-mono text-sm font-semibold ${outcome === 'loser' ? 'text-slate-400' : 'text-slate-100'}`}>
          {points}
        </span>
      )}
    </div>
  )
}

type GameCardProps = {
  game: PlayoffGame
  agentsById: Map<string, AgentProfile>
  scoresByKey: Map<string, ScheduleMatchup>
  showScores: boolean
}

function GameCard({ game, agentsById, scoresByKey, showScores }: GameCardProps) {
  const status = resolveGameStatus(game)
  const score = showScores && status !== 'pending' ? findGameScore(game, scoresByKey) : null

  return (
    <Card className="min-w-0 border-white/10 bg-slate-900 text-slate-50">
      <CardHeader className="flex-row items-center justify-between gap-2 pb-2">
        <CardDescription className="text-xs font-semibold uppercase tracking-widest text-slate-400">
          Week {game.week} · Game {game.gameSlot}
        </CardDescription>
        <span className={`shrink-0 rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-widest ${GAME_STATUS_STYLES[status]}`}>
          {describeGameStatus(game, status)}
        </span>
      </CardHeader>
      <CardContent className="p-0">
        <div className="divide-y divide-white/10">
          <Participant
            agentId={game.homeAgentId}
            seed={game.homeSeed}
            source={game.homeSource}
            outcome={resolveOutcome(game, game.homeAgentId)}
            points={formatPoints(score?.homePoints)}
            agentsById={agentsById}
          />
          <Participant
            agentId={game.awayAgentId}
            seed={game.awaySeed}
            source={game.awaySource}
            outcome={resolveOutcome(game, game.awayAgentId)}
            points={formatPoints(score?.awayPoints)}
            agentsById={agentsById}
          />
        </div>
      </CardContent>
    </Card>
  )
}

type PlacementRowProps = {
  label: string
  agentId: string | null | undefined
  agentsById: Map<string, AgentProfile>
}

function PlacementRow({ label, agentId, agentsById }: PlacementRowProps) {
  if (!agentId) return null
  const agent = agentsById.get(agentId)

  return (
    <div className="flex min-w-0 items-center gap-3 rounded-lg border border-white/10 bg-slate-950 px-4 py-3">
      <Medal aria-hidden="true" className="size-4 shrink-0 text-slate-400" />
      <AgentAvatar agentId={agentId} sizeClassName="size-9" iconClassName="size-5" />
      <div className="min-w-0 flex-1">
        <p className="text-[10px] font-semibold uppercase tracking-widest text-slate-400">{label}</p>
        <p className="truncate text-sm font-semibold text-white">{agent?.teamName ?? agentId}</p>
      </div>
    </div>
  )
}

type ChampionCardProps = {
  placements: PlayoffFinalPlacements
  season: number
  agentsById: Map<string, AgentProfile>
}

function ChampionCard({ placements, season, agentsById }: ChampionCardProps) {
  const champion = placements.championAgentId ? agentsById.get(placements.championAgentId) : undefined

  return (
    <Card className="border-emerald-300/30 bg-gradient-to-br from-emerald-400/10 to-slate-900 text-slate-50 shadow-2xl shadow-slate-950/40">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-xl text-white">
          <Crown aria-hidden="true" className="size-5 text-amber-300" />
          {season} Champion
        </CardTitle>
        <CardDescription className="text-slate-300">
          Final placements after the championship week.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {placements.championAgentId ? (
          <div className="flex min-w-0 flex-wrap items-center gap-4 rounded-xl border border-emerald-300/30 bg-slate-950/60 px-5 py-4">
            <AgentAvatar agentId={placements.championAgentId} sizeClassName="size-14" iconClassName="size-8" />
            <div className="min-w-0 flex-1">
              <p className="text-[10px] font-semibold uppercase tracking-widest text-emerald-200">Champion</p>
              <p className="truncate text-2xl font-bold text-white">
                {champion?.teamName ?? placements.championAgentId}
              </p>
              {champion && <p className="truncate text-xs text-slate-400">{champion.agentId} · {champion.modelName}</p>}
            </div>
            <Trophy aria-hidden="true" className="size-10 shrink-0 text-amber-300" />
          </div>
        ) : (
          <p className="text-sm text-slate-300">The champion has not been recorded yet.</p>
        )}
        <div className="grid gap-3 sm:grid-cols-3">
          <PlacementRow label="Runner-up" agentId={placements.runnerUpAgentId} agentsById={agentsById} />
          <PlacementRow label="Third place" agentId={placements.thirdPlaceAgentId} agentsById={agentsById} />
          <PlacementRow label="Fourth place" agentId={placements.fourthPlaceAgentId} agentsById={agentsById} />
        </div>
      </CardContent>
    </Card>
  )
}

function PlayoffsPage() {
  const [bracket, setBracket] = useState<PlayoffBracket | null>(null)
  const [agents, setAgents] = useState<AgentProfile[]>([])
  const [schedule, setSchedule] = useState<ScheduleMatchup[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function loadBracket() {
      try {
        setIsLoading(true)
        setError(null)
        const [stateResponse, agentsResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/league/state`, { signal: controller.signal }),
          fetch(`${apiBaseUrl}/api/agent-profiles?enabledOnly=false`, { signal: controller.signal }),
        ])
        if (!stateResponse.ok || !agentsResponse.ok) {
          const response = !stateResponse.ok ? stateResponse : agentsResponse
          throw new Error(`Request failed with status ${response.status}`)
        }

        const state = (await stateResponse.json()) as LeagueState
        const bracketResponse = await fetch(
          `${apiBaseUrl}/api/league/seasons/${state.season}/playoffs/bracket`,
          { signal: controller.signal },
        )
        if (!bracketResponse.ok) {
          const body = await bracketResponse.json().catch(() => null) as { error?: string } | null
          throw new Error(body?.error ?? `Request failed with status ${bracketResponse.status}`)
        }

        setAgents((await agentsResponse.json()) as AgentProfile[])
        setBracket((await bracketResponse.json()) as PlayoffBracket)

        // Scores are a best-effort enhancement: a failure here must not break the bracket.
        try {
          const scheduleResponse = await fetch(
            `${apiBaseUrl}/api/league/seasons/${state.season}/schedule`,
            { signal: controller.signal },
          )
          setSchedule(scheduleResponse.ok ? ((await scheduleResponse.json()) as ScheduleMatchup[]) : [])
        } catch (scheduleError) {
          if ((scheduleError as { name?: string }).name === 'AbortError') return
          setSchedule([])
        }
      } catch (loadError) {
        if ((loadError as { name?: string }).name === 'AbortError') return
        setError(loadError instanceof Error ? loadError.message : 'Unknown error')
      } finally {
        setIsLoading(false)
      }
    }

    void loadBracket()
    return () => controller.abort()
  }, [])

  const agentsById = useMemo(
    () => new Map(agents.map((agent) => [agent.agentId, agent])),
    [agents],
  )

  const scoresByKey = useMemo(
    () => new Map((schedule ?? []).map((matchup) => [scoreKey(matchup.week, matchup.homeAgentId, matchup.awayAgentId), matchup])),
    [schedule],
  )

  const lifecycle = bracket ? resolveLifecycle(bracket) : 'projected'
  const seeds = bracket?.seeds ?? []
  const games = bracket?.games ?? []
  const placements = bracket ? resolvePlacements(bracket) : {}
  const hasPlacements = Boolean(
    placements.championAgentId
    || placements.runnerUpAgentId
    || placements.thirdPlaceAgentId
    || placements.fourthPlaceAgentId,
  )

  const summary = bracket
    ? lifecycle === 'projected'
      ? `Projected from finalized regular-season results. Seeds and matchups lock after Week ${bracket.regularSeasonEndWeek}.`
      : lifecycle === 'locked'
        ? `Seeds are locked. The bracket advances as each playoff week is finalized (Weeks ${bracket.playoffStartWeek}–${bracket.championshipWeek}).`
        : `Playoffs are complete. Final placements and every bracket result are shown below.`
    : 'Seeds, matchups, and results for the playoff bracket.'

  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
      <div>
        <div className="flex flex-wrap items-center gap-3">
          <h2 className="flex items-center gap-2 text-3xl font-semibold tracking-tight text-white">
            <Trophy aria-hidden="true" className="size-7 text-emerald-300" />
            Playoffs
          </h2>
          {bracket && (
            <span className={`flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-semibold uppercase tracking-widest ${LIFECYCLE_BADGE_STYLES[lifecycle]}`}>
              {lifecycle !== 'projected' && <Lock aria-hidden="true" className="size-3" />}
              {LIFECYCLE_BADGE_LABELS[lifecycle]}
            </span>
          )}
        </div>
        <p className="mt-2 text-sm text-slate-400">{summary}</p>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 text-slate-300">
          <Loader2 aria-hidden="true" className="size-5 animate-spin" />
          <span className="text-sm">Loading playoff bracket…</span>
        </div>
      )}

      {error && (
        <div className="flex items-center gap-2 text-rose-300">
          <AlertCircle aria-hidden="true" className="size-5" />
          <span className="text-sm font-medium">Could not load the playoff bracket: {error}</span>
        </div>
      )}

      {bracket && !isLoading && !error && (
        <>
          {lifecycle === 'complete' && hasPlacements && (
            <ChampionCard placements={placements} season={bracket.season} agentsById={agentsById} />
          )}

          <Card className="border-white/10 bg-slate-900 text-slate-50">
            <CardHeader>
              <CardTitle className="text-xl text-white">
                {lifecycle === 'projected' ? 'Projected seeds' : 'Final seeds'}
              </CardTitle>
              <CardDescription className="text-slate-400">
                {lifecycle === 'projected'
                  ? 'Winning percentage, Points For, head-to-head when every tied pair played equally, then higher Points Against as strength of schedule. These seeds can still change.'
                  : `Locked after Week ${bracket.regularSeasonEndWeek}. Top ${bracket.firstRoundByeCount} seeds earned a first-round bye.`}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {seeds.length === 0 ? (
                <p className="text-sm text-slate-300">Seeds are not available yet.</p>
              ) : (
                <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                  {seeds.map((seed) => {
                    const agent = agentsById.get(seed.agentId)
                    return (
                      <div key={seed.agentId} className="flex min-w-0 items-center gap-3 rounded-lg border border-white/10 bg-slate-950 px-4 py-3">
                        <span className="w-7 shrink-0 text-center text-lg font-bold text-emerald-300">#{seed.seed}</span>
                        <AgentAvatar agentId={seed.agentId} />
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-semibold text-white">{agent?.teamName ?? seed.agentId}</p>
                          <p className="truncate text-xs text-slate-400">
                            {formatRecord(seed)} · {seed.pointsFor.toFixed(2)} PF
                          </p>
                        </div>
                        {seed.hasFirstRoundBye && (
                          <span
                            aria-label="First-round bye"
                            className="shrink-0 rounded bg-emerald-300/10 px-2 py-1 text-[10px] font-semibold uppercase tracking-widest text-emerald-200"
                          >
                            Bye
                          </span>
                        )}
                      </div>
                    )
                  })}
                </div>
              )}
            </CardContent>
          </Card>

          {games.length === 0 ? (
            <p className="text-sm text-slate-300">
              Bracket matchups appear once the playoff bracket is generated.
            </p>
          ) : (
            <section className="grid gap-6 md:grid-cols-2 xl:grid-cols-4">
              {ROUND_ORDER.map((round) => {
                const roundGames = games.filter((game) => game.round === round)
                if (roundGames.length === 0) return null

                return (
                  <div key={round} className="flex min-w-0 flex-col gap-3">
                    <h3 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-300">
                      {ROUND_LABELS[round] ?? round}
                    </h3>
                    {roundGames.map((game) => (
                      <GameCard
                        key={`${game.round}-${game.gameSlot}`}
                        game={game}
                        agentsById={agentsById}
                        scoresByKey={scoresByKey}
                        showScores={lifecycle !== 'projected'}
                      />
                    ))}
                  </div>
                )
              })}
            </section>
          )}
        </>
      )}
    </main>
  )
}

export default PlayoffsPage
