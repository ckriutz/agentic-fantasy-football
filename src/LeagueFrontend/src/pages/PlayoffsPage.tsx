import { useEffect, useMemo, useState } from 'react'
import { AlertCircle, Loader2, Trophy } from 'lucide-react'

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
}

type PlayoffBracket = {
  season: number
  status: string
  regularSeasonEndWeek: number
  playoffStartWeek: number
  championshipWeek: number
  playoffTeamCount: number
  firstRoundByeCount: number
  seeds: PlayoffSeed[]
  games: PlayoffGame[]
}

const ROUND_LABELS: Record<string, string> = {
  wild_card: 'Wild Card',
  semifinal: 'Semifinals',
  championship: 'Championship',
  third_place: 'Third Place',
}

function formatRecord(seed: PlayoffSeed) {
  return seed.ties > 0 ? `${seed.wins}-${seed.losses}-${seed.ties}` : `${seed.wins}-${seed.losses}`
}

function Participant({
  agentId,
  seed,
  source,
  agentsById,
}: {
  agentId: string | null
  seed: number | null
  source: string | null
  agentsById: Map<string, AgentProfile>
}) {
  const agent = agentId ? agentsById.get(agentId) : undefined

  return (
    <div className="flex min-h-14 items-center gap-3 px-3 py-2">
      {agentId && <AgentAvatar agentId={agentId} />}
      <span className="w-5 shrink-0 text-center text-xs font-semibold text-emerald-300">
        {seed ? `#${seed}` : ''}
      </span>
      <div className="min-w-0">
        <p className="truncate text-sm font-semibold text-white">
          {agent?.teamName ?? source ?? 'To be determined'}
        </p>
        <p className="truncate text-xs text-slate-400">
          {agent ? agent.agentId : source ? 'Advances after prior round' : ''}
        </p>
      </div>
    </div>
  )
}

function GameCard({ game, agentsById }: { game: PlayoffGame; agentsById: Map<string, AgentProfile> }) {
  return (
    <Card className="border-white/10 bg-slate-900 text-slate-50">
      <CardHeader className="pb-2">
        <CardDescription className="text-xs font-semibold uppercase tracking-widest text-slate-400">
          Week {game.week} · Game {game.gameSlot}
        </CardDescription>
      </CardHeader>
      <CardContent className="p-0">
        <div className="divide-y divide-white/10">
          <Participant agentId={game.homeAgentId} seed={game.homeSeed} source={game.homeSource} agentsById={agentsById} />
          <Participant agentId={game.awayAgentId} seed={game.awaySeed} source={game.awaySource} agentsById={agentsById} />
        </div>
      </CardContent>
    </Card>
  )
}

function PlayoffsPage() {
  const [bracket, setBracket] = useState<PlayoffBracket | null>(null)
  const [agents, setAgents] = useState<AgentProfile[]>([])
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
          fetch(`${apiBaseUrl}/api/agent-profiles?enabledOnly=true`, { signal: controller.signal }),
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

  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
      <div>
        <div className="flex flex-wrap items-center gap-3">
          <h2 className="flex items-center gap-2 text-3xl font-semibold tracking-tight text-white">
            <Trophy className="size-7 text-emerald-300" />
            Playoffs
          </h2>
          {bracket && (
            <span className="rounded-full border border-amber-300/30 bg-amber-300/10 px-3 py-1 text-xs font-semibold uppercase tracking-widest text-amber-200">
              {bracket.status}
            </span>
          )}
        </div>
        <p className="mt-2 text-sm text-slate-400">
          Projected from finalized regular-season results. Seeds lock after Week {bracket?.regularSeasonEndWeek ?? 14}.
        </p>
      </div>

      {isLoading && (
        <div className="flex items-center gap-2 text-slate-300">
          <Loader2 className="size-5 animate-spin" />
          <span className="text-sm">Loading projected bracket…</span>
        </div>
      )}

      {error && (
        <div className="flex items-center gap-2 text-rose-300">
          <AlertCircle className="size-5" />
          <span className="text-sm font-medium">Could not load playoff projection: {error}</span>
        </div>
      )}

      {bracket && !isLoading && !error && (
        <>
          <Card className="border-white/10 bg-slate-900 text-slate-50">
            <CardHeader>
              <CardTitle className="text-xl text-white">Projected seeds</CardTitle>
              <CardDescription className="text-slate-400">
                Winning percentage, Points For, head-to-head when every tied pair played equally, then higher Points Against as strength of schedule.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                {bracket.seeds.map((seed) => {
                  const agent = agentsById.get(seed.agentId)
                  return (
                    <div key={seed.agentId} className="flex items-center gap-3 rounded-lg border border-white/10 bg-slate-950 px-4 py-3">
                      <span className="w-7 text-center text-lg font-bold text-emerald-300">#{seed.seed}</span>
                      <AgentAvatar agentId={seed.agentId} />
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-semibold text-white">{agent?.teamName ?? seed.agentId}</p>
                        <p className="text-xs text-slate-400">
                          {formatRecord(seed)} · {seed.pointsFor.toFixed(2)} PF
                        </p>
                      </div>
                      {seed.hasFirstRoundBye && (
                        <span className="rounded bg-emerald-300/10 px-2 py-1 text-[10px] font-semibold uppercase tracking-widest text-emerald-200">
                          Bye
                        </span>
                      )}
                    </div>
                  )
                })}
              </div>
            </CardContent>
          </Card>

          <section className="grid gap-6 xl:grid-cols-4">
            {['wild_card', 'semifinal', 'championship', 'third_place'].map((round) => (
              <div key={round} className="flex flex-col gap-3">
                <h3 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-300">
                  {ROUND_LABELS[round]}
                </h3>
                {bracket.games
                  .filter((game) => game.round === round)
                  .map((game) => <GameCard key={`${game.round}-${game.gameSlot}`} game={game} agentsById={agentsById} />)}
              </div>
            ))}
          </section>
        </>
      )}
    </main>
  )
}

export default PlayoffsPage
