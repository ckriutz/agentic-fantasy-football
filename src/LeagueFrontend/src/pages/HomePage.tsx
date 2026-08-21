import { useEffect, useMemo, useState } from 'react'
import { CalendarDays, ChevronLeft, ChevronRight, Swords, Trophy } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import AgentAvatar from '@/components/AgentAvatar'
import { apiBaseUrl } from '@/lib/config'

type LeagueState = {
  season: number
  week: number
  phase: string
  updatedAtUtc: string
  updatedBy: string
}

type AgentProfile = {
  agentId: string
  teamName: string
  modelName: string
  connection: string
  createdAtUtc: string
  lastUpdatedAt: string
  isBootstrapped: boolean
  isEnabled: boolean
}

type AgentStanding = {
  agentId: string
  wins: number
  losses: number
  ties: number
  winningPercentage: number
  pointsFor: number
  pointsAgainst: number
}

type Matchup = {
  matchupId: number
  season: number
  week: number
  matchupType: string
  homeAgentId: string
  awayAgentId: string
  homePoints: number
  awayPoints: number
  isComplete: boolean
  winnerAgentId: string | null
  isTie: boolean
}

function MatchupSide({
  agent,
  points,
  isWinning,
  isLosing,
}: {
  agent: AgentProfile | undefined
  points: number
  isWinning: boolean
  isLosing: boolean
}) {
  const scoreColor = isWinning
    ? 'text-emerald-300'
    : isLosing
      ? 'text-rose-300'
      : 'text-slate-200'

  return (
    <div className="flex min-w-0 flex-1 items-center gap-2">
      {agent && <AgentAvatar agentId={agent.agentId} />}
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold text-white">
          {agent?.teamName ?? 'Unknown'}
        </p>
        <p className="truncate text-[10px] text-slate-400">
          {agent ? `${agent.agentId} | ${agent.modelName}` : ''}
        </p>
      </div>
      <span className={`font-mono text-base font-semibold ${scoreColor}`}>
        {points.toFixed(2)}
      </span>
    </div>
  )
}

function MatchupsCard({
  season,
  currentWeek,
  agentsById,
}: {
  season: number
  currentWeek: number
  agentsById: Map<string, AgentProfile>
}) {
  const [week, setWeek] = useState(currentWeek)
  const [matchups, setMatchups] = useState<Matchup[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    setWeek(currentWeek)
  }, [currentWeek])

  useEffect(() => {
    const controller = new AbortController()

    async function fetchSchedule() {
      try {
        setIsLoading(true)
        setError(null)
        const response = await fetch(
          `${apiBaseUrl}/api/league/seasons/${season}/schedule/${week}`,
          { signal: controller.signal },
        )
        if (!response.ok) throw new Error(`Request failed with status ${response.status}`)
        const data = (await response.json()) as Matchup[]
        setMatchups(data)
      } catch (err) {
        if ((err as { name?: string }).name === 'AbortError') return
        setError(err instanceof Error ? err.message : 'Unknown error')
      } finally {
        setIsLoading(false)
      }
    }

    fetchSchedule()
    return () => controller.abort()
  }, [season, week])

  return (
    <Card className="border-white/10 bg-slate-900 text-slate-50">
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between gap-3">
          <CardTitle className="flex items-center gap-2 text-xl text-white">
            <Swords className="size-5 text-emerald-300" />
            Matchups
          </CardTitle>
          <div className="flex items-center gap-1">
            <button
              type="button"
              onClick={() => setWeek((w) => Math.max(1, w - 1))}
              disabled={week <= 1 || isLoading}
              aria-label="Previous week"
              className="inline-flex size-7 items-center justify-center rounded-md text-slate-300 hover:bg-white/5 hover:text-white disabled:opacity-30"
            >
              <ChevronLeft className="size-4" />
            </button>
            <span className="min-w-[4rem] text-center text-sm font-semibold text-emerald-300">
              Week {week}
            </span>
            <button
              type="button"
              onClick={() => setWeek((w) => w + 1)}
              disabled={isLoading}
              aria-label="Next week"
              className="inline-flex size-7 items-center justify-center rounded-md text-slate-300 hover:bg-white/5 hover:text-white disabled:opacity-30"
            >
              <ChevronRight className="size-4" />
            </button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="pt-0">
        {isLoading ? (
          <p className="text-sm text-slate-300">Loading matchups…</p>
        ) : error ? (
          <p className="text-sm text-rose-300">Failed to load matchups: {error}</p>
        ) : matchups.length === 0 ? (
          <p className="text-sm text-slate-300">No matchups scheduled for week {week}.</p>
        ) : (
          <ul className="space-y-2">
            {matchups.map((m) => {
              const homeAgent = agentsById.get(m.homeAgentId)
              const awayAgent = agentsById.get(m.awayAgentId)
              const homeWinning = m.homePoints > m.awayPoints
              const awayWinning = m.awayPoints > m.homePoints
              return (
                <li key={m.matchupId}>
                  <Link
                    to={`/matchups/${m.matchupId}`}
                    className="flex items-center gap-3 rounded-lg border border-white/10 bg-slate-950 px-3 py-2 transition-colors hover:border-emerald-300/40 hover:bg-slate-900"
                  >
                    <MatchupSide
                      agent={homeAgent}
                      points={m.homePoints}
                      isWinning={homeWinning}
                      isLosing={awayWinning}
                    />
                    <span className="flex shrink-0 flex-col items-center">
                      <span className="text-[10px] font-semibold uppercase tracking-widest text-slate-500">
                        vs
                      </span>
                      <span
                        className={`text-[9px] font-semibold uppercase tracking-widest ${m.isComplete ? 'text-slate-500' : 'text-emerald-400'}`}
                      >
                        {m.isComplete ? 'Final' : 'Live'}
                      </span>
                    </span>
                    <MatchupSide
                      agent={awayAgent}
                      points={m.awayPoints}
                      isWinning={awayWinning}
                      isLosing={homeWinning}
                    />
                  </Link>
                </li>
              )
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

function HomePage() {
  const [leagueState, setLeagueState] = useState<LeagueState | null>(null)
  const [leagueStateError, setLeagueStateError] = useState<string | null>(null)
  const [isLoadingLeagueState, setIsLoadingLeagueState] = useState(true)

  const [agents, setAgents] = useState<AgentProfile[]>([])
  const [standings, setStandings] = useState<AgentStanding[]>([])
  const [agentsError, setAgentsError] = useState<string | null>(null)
  const [isLoadingAgents, setIsLoadingAgents] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function fetchLeagueState() {
      try {
        setIsLoadingLeagueState(true)
        setLeagueStateError(null)
        const response = await fetch(`${apiBaseUrl}/api/league/state`, {
          signal: controller.signal,
        })
        if (!response.ok) {
          throw new Error(`Request failed with status ${response.status}`)
        }
        const data = (await response.json()) as LeagueState
        setLeagueState(data)
      } catch (error) {
        if ((error as { name?: string }).name === 'AbortError') {
          return
        }
        setLeagueStateError(error instanceof Error ? error.message : 'Unknown error')
      } finally {
        setIsLoadingLeagueState(false)
      }
    }

    fetchLeagueState()
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!leagueState) return

    const season = leagueState.season
    const controller = new AbortController()

    async function fetchAgents() {
      try {
        setIsLoadingAgents(true)
        setAgentsError(null)
        const [agentsResponse, standingsResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/agent-profiles?enabledOnly=true`, { signal: controller.signal }),
          fetch(`${apiBaseUrl}/api/league/seasons/${season}/standings`, {
            signal: controller.signal,
          }),
        ])
        if (!agentsResponse.ok || !standingsResponse.ok) {
          throw new Error(
            `Request failed with status ${!agentsResponse.ok ? agentsResponse.status : standingsResponse.status}`,
          )
        }
        setAgents((await agentsResponse.json()) as AgentProfile[])
        setStandings((await standingsResponse.json()) as AgentStanding[])
      } catch (error) {
        if ((error as { name?: string }).name === 'AbortError') {
          return
        }
        setAgentsError(error instanceof Error ? error.message : 'Unknown error')
      } finally {
        setIsLoadingAgents(false)
      }
    }

    fetchAgents()
    return () => controller.abort()
  }, [leagueState])

  const agentsById = useMemo(() => {
    const map = new Map<string, AgentProfile>()
    for (const a of agents) map.set(a.agentId, a)
    return map
  }, [agents])
  const standingsByAgentId = useMemo(
    () => new Map(standings.map((standing) => [standing.agentId, standing])),
    [standings],
  )
  const agentsInStandingOrder = useMemo(
    () =>
      [...agents].sort((a, b) => {
        const aStanding = standingsByAgentId.get(a.agentId)
        const bStanding = standingsByAgentId.get(b.agentId)
        if (!aStanding || !bStanding) return a.agentId.localeCompare(b.agentId)
        return (
          bStanding.wins - aStanding.wins
          || aStanding.losses - bStanding.losses
          || bStanding.ties - aStanding.ties
          || a.agentId.localeCompare(b.agentId)
        )
      }),
    [agents, standingsByAgentId],
  )

  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
      <section className="grid flex-1 items-start gap-6 lg:grid-cols-[1.4fr_0.9fr]">
        <Card className="border-white/10 bg-white/5 text-slate-50 shadow-2xl shadow-slate-950/40">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-xl text-white">
              <Trophy className="size-5 text-emerald-300" />
              Agents & standings
            </CardTitle>
            <CardDescription className="text-slate-300">
              Records from completed matchups.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {isLoadingAgents ? (
              <p className="text-sm text-slate-300">Loading agents…</p>
            ) : agentsError ? (
              <p className="text-sm text-rose-300">Failed to load agents: {agentsError}</p>
            ) : agents.length === 0 ? (
              <p className="text-sm text-slate-300">No agents found.</p>
            ) : (
              <ul className="divide-y divide-white/10">
                {agentsInStandingOrder.map((agent, index) => (
                  <li
                    key={agent.agentId}
                    className="flex items-center gap-4 py-3 first:pt-0 last:pb-0"
                  >
                    <span className="w-6 text-right text-sm font-semibold text-slate-400">
                      {index + 1}
                    </span>
                    <AgentAvatar agentId={agent.agentId} />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-base font-semibold text-white">
                        {agent.teamName}
                      </p>
                      <p className="truncate text-xs text-slate-400">
                        <Link
                          to={`/agents?agentId=${encodeURIComponent(agent.agentId)}`}
                          className="text-emerald-300 hover:underline"
                        >
                          {agent.agentId}
                        </Link>
                        {' | '}
                        {agent.modelName}
                        {' | '}
                        {agent.connection}
                      </p>
                    </div>
                    <span className="shrink-0 rounded-full border border-white/10 bg-slate-950 px-3 py-1 text-sm font-mono text-slate-200">
                      {(() => {
                        const standing = standingsByAgentId.get(agent.agentId)
                        if (!standing || standing.ties === 0) {
                          return `${standing?.wins ?? 0}-${standing?.losses ?? 0}`
                        }
                        return `${standing.wins}-${standing.losses}-${standing.ties}`
                      })()}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <div className="flex flex-col gap-6">
          <Card className="border-white/10 bg-slate-900 text-slate-50">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-xl text-white">
                <CalendarDays className="size-5 text-emerald-300" />
                League state
              </CardTitle>
              <CardDescription className="text-slate-300">
                Live snapshot from <code className="rounded bg-white/10 px-1.5 py-0.5 text-slate-100">/api/league/state</code>.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {isLoadingLeagueState ? (
                <p className="text-sm text-slate-300">Loading league state…</p>
              ) : leagueStateError ? (
                <p className="text-sm text-rose-300">Failed to load league state: {leagueStateError}</p>
              ) : leagueState ? (
                <div className="flex flex-wrap items-center gap-x-6 gap-y-2 rounded-lg border border-white/10 bg-slate-950 px-4 py-3">
                  <div className="flex items-baseline gap-2">
                    <span className="text-xs font-medium uppercase tracking-[0.2em] text-slate-400">Season</span>
                    <span className="text-lg font-semibold text-white">{leagueState.season}</span>
                  </div>
                  <div className="flex items-baseline gap-2">
                    <span className="text-xs font-medium uppercase tracking-[0.2em] text-slate-400">Week</span>
                    <span className="text-lg font-semibold text-white">{leagueState.week}</span>
                  </div>
                  <div className="flex items-baseline gap-2">
                    <span className="text-xs font-medium uppercase tracking-[0.2em] text-slate-400">Phase</span>
                    <span className="text-lg font-semibold text-emerald-200">{leagueState.phase}</span>
                  </div>
                </div>
              ) : null}
            </CardContent>
          </Card>

          {leagueState && (
            <MatchupsCard season={leagueState.season} currentWeek={leagueState.week} agentsById={agentsById} />
          )}
        </div>
      </section>

    </main>
  )
}

export default HomePage
