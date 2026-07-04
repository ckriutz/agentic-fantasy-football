import { useEffect, useState } from 'react'
import { CalendarDays, Trophy, UserCircle2 } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

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

function HomePage() {
  const [leagueState, setLeagueState] = useState<LeagueState | null>(null)
  const [leagueStateError, setLeagueStateError] = useState<string | null>(null)
  const [isLoadingLeagueState, setIsLoadingLeagueState] = useState(true)

  const [agents, setAgents] = useState<AgentProfile[]>([])
  const [agentsError, setAgentsError] = useState<string | null>(null)
  const [isLoadingAgents, setIsLoadingAgents] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function fetchLeagueState() {
      try {
        setIsLoadingLeagueState(true)
        setLeagueStateError(null)
        const response = await fetch('http://localhost:5000/api/league/state', {
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
    const controller = new AbortController()

    async function fetchAgents() {
      try {
        setIsLoadingAgents(true)
        setAgentsError(null)
        const response = await fetch('http://localhost:5000/api/agent-profiles/', {
          signal: controller.signal,
        })
        if (!response.ok) {
          throw new Error(`Request failed with status ${response.status}`)
        }
        const data = (await response.json()) as AgentProfile[]
        setAgents(data)
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
  }, [])

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
              Wins and losses will populate once matchups are wired up.
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
                {agents.map((agent, index) => (
                  <li
                    key={agent.agentId}
                    className="flex items-center gap-4 py-3 first:pt-0 last:pb-0"
                  >
                    <span className="w-6 text-right text-sm font-semibold text-slate-400">
                      {index + 1}
                    </span>
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-full border border-white/10 bg-slate-950 text-slate-400">
                      <UserCircle2 className="size-6" />
                    </div>
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
                      0-0
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

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
      </section>

    </main>
  )
}

export default HomePage
