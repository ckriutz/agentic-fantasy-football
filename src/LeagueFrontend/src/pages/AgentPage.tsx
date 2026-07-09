import { useEffect, useMemo, useState } from 'react'
import { AlertCircle, ArrowLeft, ChevronRight, Loader2, ScrollText, UserCircle2 } from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import AgentAvatar from '@/components/AgentAvatar'

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

type LeagueState = {
  season: number
  week: number
  phase: string
}

type RosterPlayer = {
  sleeperPlayerId: string
  fullName: string
  team: string | null
  position: string | null
  injuryStatus: string | null
}

type RosterEntry = {
  player: RosterPlayer | null
  ownerAgentId: string
  isAvailable: boolean
  acquiredAtUtc: string | null
  acquisitionSource: string | null
  slotType: string | null
  isStarter: boolean
  weeklyPoints: Record<string, number> | null
}

type Decision = {
  decisionId: number
  agentId: string
  week: number
  type: string | null
  reasoning: string | null
  action: string | null
  createdAtUtc: string
  inputTokenCount: number | null
  outputTokenCount: number | null
  cachedInputTokenCount: number | null
  reasoningTokenCount: number | null
}

const SLOT_ORDER: Record<string, number> = {
  QB: 1,
  RB: 2,
  WR: 3,
  TE: 4,
  WRT: 5,
  FLEX: 5,
  K: 6,
  DEF: 7,
  BN: 8,
  IR: 9,
}

function slotGroup(slotType: string | null): string {
  if (!slotType) return 'BN'
  const match = slotType.match(/^([A-Z]+)/i)
  return match ? match[1].toUpperCase() : 'BN'
}

function slotIndex(slotType: string | null): number {
  const match = slotType?.match(/(\d+)$/)
  return match ? parseInt(match[1], 10) : 0
}

function compareEntries(a: RosterEntry, b: RosterEntry): number {
  const groupA = slotGroup(a.slotType)
  const groupB = slotGroup(b.slotType)
  const orderA = SLOT_ORDER[groupA] ?? 99
  const orderB = SLOT_ORDER[groupB] ?? 99
  if (orderA !== orderB) return orderA - orderB
  return slotIndex(a.slotType) - slotIndex(b.slotType)
}

function positionBadgeClass(position: string | null): string {
  switch (position?.toUpperCase()) {
    case 'QB': return 'bg-blue-500/20 text-blue-300 border-blue-500/40'
    case 'RB': return 'bg-green-500/20 text-green-300 border-green-500/40'
    case 'WR': return 'bg-amber-500/20 text-amber-300 border-amber-500/40'
    case 'TE': return 'bg-orange-500/20 text-orange-300 border-orange-500/40'
    case 'K':  return 'bg-purple-500/20 text-purple-300 border-purple-500/40'
    case 'DEF':return 'bg-rose-500/20 text-rose-300 border-rose-500/40'
    default:   return 'bg-slate-500/20 text-slate-300 border-slate-500/40'
  }
}

function injuryBadgeClass(status: string | null): string | null {
  if (!status) return null
  const s = status.toUpperCase()
  if (['OUT', 'IR', 'SUSPENDED', 'PUP'].includes(s)) return 'bg-red-500/20 text-red-300'
  if (s === 'DOUBTFUL') return 'bg-orange-500/20 text-orange-300'
  if (s === 'QUESTIONABLE' || s === 'QUES') return 'bg-yellow-500/20 text-yellow-300'
  return 'bg-slate-500/20 text-slate-300'
}

function RosterRow({ entry, currentWeek }: { entry: RosterEntry; currentWeek: number | null }) {
  const player = entry.player
  const group = slotGroup(entry.slotType)
  const points = currentWeek != null ? entry.weeklyPoints?.[String(currentWeek)] : undefined
  const injuryCls = injuryBadgeClass(player?.injuryStatus ?? null)

  return (
    <li className="flex items-center gap-3 py-2 first:pt-0 last:pb-0">
      <span className="flex w-12 shrink-0 items-center justify-center rounded-md border border-white/10 bg-slate-950 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-slate-300">
        {group}
      </span>
      <div className="flex size-8 shrink-0 items-center justify-center rounded-full border border-white/10 bg-slate-900 text-slate-400">
        <UserCircle2 className="size-5" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <Link
            to={player ? `/players/${player.sleeperPlayerId}` : '#'}
            className="truncate text-sm font-semibold text-white hover:text-emerald-300"
          >
            {player?.fullName ?? 'Unknown player'}
          </Link>
          {player?.position && (
            <span className={`rounded border px-1.5 py-0.5 text-[10px] font-semibold ${positionBadgeClass(player.position)}`}>
              {player.position}
            </span>
          )}
          {player?.team && (
            <span className="text-xs font-medium text-slate-400">{player.team}</span>
          )}
          {player?.injuryStatus && injuryCls && (
            <span className={`rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase ${injuryCls}`}>
              {player.injuryStatus}
            </span>
          )}
        </div>
      </div>
      <div className="shrink-0 text-right">
        <p className="font-mono text-base font-semibold text-white leading-tight">
          {points != null ? points.toFixed(2) : '—'}
        </p>
        <p className="text-[10px] uppercase tracking-widest text-slate-500 leading-tight">
          {currentWeek != null ? `Wk ${currentWeek}` : 'Wk'}
        </p>
      </div>
    </li>
  )
}

function decisionPreview(reasoning: string | null, maxLen = 140): string {
  const flat = (reasoning ?? '').replace(/\s+/g, ' ').trim()
  return flat.length > maxLen ? `${flat.slice(0, maxLen).trimEnd()}…` : flat
}

function formatTokenCount(value: number | null) {
  return (value ?? 0).toLocaleString()
}

function DecisionItem({ decision }: { decision: Decision }) {
  return (
    <details className="group rounded-lg border border-white/10 bg-slate-950 open:bg-slate-900/60">
      <summary className="flex cursor-pointer list-none items-start gap-3 px-3 py-2">
        <ChevronRight className="mt-1 size-4 shrink-0 text-slate-400 transition-transform group-open:rotate-90" />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded border border-emerald-400/40 bg-emerald-400/10 px-1.5 py-0.5 text-[10px] font-semibold uppercase text-emerald-200">
              {decision.type ?? 'Decision'}
            </span>
            <span className="rounded border border-white/10 bg-slate-950 px-1.5 py-0.5 text-[10px] font-semibold uppercase text-slate-300">
              {decision.action ?? 'Action'}
            </span>
            <span className="text-[10px] uppercase tracking-widest text-slate-500">
              Wk {decision.week}
            </span>
            <span className="ml-auto text-[10px] text-slate-500">
              {new Date(decision.createdAtUtc).toLocaleString()}
            </span>
          </div>
          <p className="mt-1 truncate text-xs text-slate-400 group-open:hidden">
            {decisionPreview(decision.reasoning)}
          </p>
        </div>
      </summary>
      <div className="border-t border-white/5 px-3 py-3 text-sm leading-6 text-slate-200">
        <pre className="whitespace-pre-wrap font-sans">{decision.reasoning ?? 'No reasoning recorded.'}</pre>
        <p className="mt-3 text-[10px] uppercase tracking-widest text-slate-500">
          Tokens · in {formatTokenCount(decision.inputTokenCount)} · out {formatTokenCount(decision.outputTokenCount)} · cached {formatTokenCount(decision.cachedInputTokenCount)} · reasoning {formatTokenCount(decision.reasoningTokenCount)}
        </p>
      </div>
    </details>
  )
}

function AgentPage() {
  const [searchParams] = useSearchParams()
  const agentId = searchParams.get('agentId')

  const [profile, setProfile] = useState<AgentProfile | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [isLoadingProfile, setIsLoadingProfile] = useState(true)

  const [roster, setRoster] = useState<RosterEntry[]>([])
  const [rosterError, setRosterError] = useState<string | null>(null)
  const [isLoadingRoster, setIsLoadingRoster] = useState(true)

  const [leagueState, setLeagueState] = useState<LeagueState | null>(null)

  const [decisions, setDecisions] = useState<Decision[]>([])
  const [decisionsError, setDecisionsError] = useState<string | null>(null)
  const [isLoadingDecisions, setIsLoadingDecisions] = useState(true)

  useEffect(() => {
    if (!agentId) return
    const controller = new AbortController()

    async function fetchProfile() {
      try {
        setIsLoadingProfile(true)
        setProfileError(null)
        const response = await fetch('http://localhost:5000/api/agent-profiles/', {
          signal: controller.signal,
        })
        if (!response.ok) throw new Error(`Request failed with status ${response.status}`)
        const data = (await response.json()) as AgentProfile[]
        const match = data.find((a) => a.agentId === agentId)
        if (!match) throw new Error(`Agent "${agentId}" not found`)
        setProfile(match)
      } catch (error) {
        if ((error as { name?: string }).name === 'AbortError') return
        setProfileError(error instanceof Error ? error.message : 'Unknown error')
      } finally {
        setIsLoadingProfile(false)
      }
    }

    fetchProfile()
    return () => controller.abort()
  }, [agentId])

  useEffect(() => {
    if (!agentId) return
    const controller = new AbortController()

    async function fetchRoster() {
      try {
        setIsLoadingRoster(true)
        setRosterError(null)
        const response = await fetch(`http://localhost:5000/api/rosters/${agentId}`, {
          signal: controller.signal,
        })
        if (!response.ok) throw new Error(`Request failed with status ${response.status}`)
        const data = (await response.json()) as RosterEntry[]
        setRoster(data)
      } catch (error) {
        if ((error as { name?: string }).name === 'AbortError') return
        setRosterError(error instanceof Error ? error.message : 'Unknown error')
      } finally {
        setIsLoadingRoster(false)
      }
    }

    fetchRoster()
    return () => controller.abort()
  }, [agentId])

  useEffect(() => {
    if (!agentId) return
    const controller = new AbortController()

    async function fetchDecisions() {
      try {
        setIsLoadingDecisions(true)
        setDecisionsError(null)
        const response = await fetch(`http://localhost:5000/api/decisions/${agentId}`, {
          signal: controller.signal,
        })
        if (!response.ok) throw new Error(`Request failed with status ${response.status}`)
        const data = (await response.json()) as Decision[]
        setDecisions(data)
      } catch (error) {
        if ((error as { name?: string }).name === 'AbortError') return
        setDecisionsError(error instanceof Error ? error.message : 'Unknown error')
      } finally {
        setIsLoadingDecisions(false)
      }
    }

    fetchDecisions()
    return () => controller.abort()
  }, [agentId])

  useEffect(() => {
    const controller = new AbortController()
    async function fetchLeagueState() {
      try {
        const response = await fetch('http://localhost:5000/api/league/state', {
          signal: controller.signal,
        })
        if (!response.ok) return
        setLeagueState((await response.json()) as LeagueState)
      } catch {
        /* ignore */
      }
    }
    fetchLeagueState()
    return () => controller.abort()
  }, [])

  const { starters, bench, ir } = useMemo(() => {
    const sorted = [...roster].sort(compareEntries)
    const startersList: RosterEntry[] = []
    const benchList: RosterEntry[] = []
    const irList: RosterEntry[] = []
    for (const entry of sorted) {
      const group = slotGroup(entry.slotType)
      if (group === 'IR') irList.push(entry)
      else if (group === 'BN' || !entry.isStarter) benchList.push(entry)
      else startersList.push(entry)
    }
    return { starters: startersList, bench: benchList, ir: irList }
  }, [roster])

  const sortedDecisions = useMemo(
    () =>
      [...decisions].sort(
        (a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime(),
      ),
    [decisions],
  )

  if (!agentId) {
    return (
      <main className="flex flex-1 flex-col gap-4 px-6 py-6 xl:px-10">
        <div className="flex items-center gap-2 text-red-400">
          <AlertCircle className="size-5" />
          <span className="text-sm font-medium">Missing agentId query parameter.</span>
        </div>
        <Link to="/" className="text-sm text-emerald-300 hover:underline">
          Back to overview
        </Link>
      </main>
    )
  }

  const currentWeek = leagueState?.week ?? null

  return (
    <main className="flex flex-1 flex-col gap-4 px-6 py-6 xl:px-10">
      <div className="flex items-center gap-3 flex-wrap">
        <Link
          to="/"
          aria-label="Back to overview"
          className="inline-flex size-9 items-center justify-center rounded-md text-slate-300 hover:bg-white/5 hover:text-white transition-colors"
        >
          <ArrowLeft className="size-4" />
        </Link>
        <h2 className="text-2xl font-semibold tracking-tight text-white">
          {profile ? profile.teamName : 'Agent'}
        </h2>
      </div>

      {isLoadingProfile && (
        <div className="flex items-center gap-2 text-slate-300">
          <Loader2 className="size-5 animate-spin" />
          <span className="text-sm">Loading agent…</span>
        </div>
      )}
      {profileError && (
        <div className="flex items-center gap-2 text-red-400">
          <AlertCircle className="size-5" />
          <span className="text-sm font-medium">Could not load agent: {profileError}</span>
        </div>
      )}

      {profile && (
        <Card className="border-white/10 bg-slate-900 text-slate-50">
          <CardContent className="flex flex-wrap items-center gap-4 py-3">
            <AgentAvatar agentId={profile.agentId} sizeClassName="size-14" iconClassName="size-9" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-base font-semibold text-white">{profile.teamName}</p>
              <p className="truncate text-xs text-slate-400">
                <span className="font-mono text-slate-200">{profile.agentId}</span>
                {' · '}
                {profile.modelName}
                {' · '}
                {profile.connection}
              </p>
              <p className="text-[10px] text-slate-500">
                Last updated {new Date(profile.lastUpdatedAt).toLocaleString()}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <span
                className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold ${
                  profile.isBootstrapped
                    ? 'border-emerald-400/40 bg-emerald-400/10 text-emerald-200'
                    : 'border-slate-500/40 bg-slate-500/10 text-slate-300'
                }`}
              >
                {profile.isBootstrapped ? 'Bootstrapped' : 'Not bootstrapped'}
              </span>
              <span
                className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold ${
                  profile.isEnabled
                    ? 'border-emerald-400/40 bg-emerald-400/10 text-emerald-200'
                    : 'border-rose-500/40 bg-rose-500/10 text-rose-200'
                }`}
              >
                {profile.isEnabled ? 'Enabled' : 'Disabled'}
              </span>
            </div>
          </CardContent>
        </Card>
      )}

      <Card className="border-white/10 bg-slate-900 text-slate-50">
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center justify-between text-lg text-white">
            <span>Roster</span>
            {leagueState && (
              <span className="text-xs font-normal text-emerald-300">
                Week {leagueState.week}
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {isLoadingRoster ? (
            <p className="text-sm text-slate-300">Loading roster…</p>
          ) : rosterError ? (
            <p className="text-sm text-rose-300">Failed to load roster: {rosterError}</p>
          ) : roster.length === 0 ? (
            <p className="text-sm text-slate-300">No players on roster.</p>
          ) : (
            <div className="space-y-3">
              {starters.length > 0 && (
                <div>
                  <p className="mb-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-slate-400">
                    Starters
                  </p>
                  <ul className="divide-y divide-white/10">
                    {starters.map((entry) => (
                      <RosterRow
                        key={entry.player?.sleeperPlayerId ?? `${entry.slotType}-starter`}
                        entry={entry}
                        currentWeek={currentWeek}
                      />
                    ))}
                  </ul>
                </div>
              )}
              {bench.length > 0 && (
                <div>
                  <p className="mb-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-slate-400">
                    Bench
                  </p>
                  <ul className="divide-y divide-white/10">
                    {bench.map((entry) => (
                      <RosterRow
                        key={entry.player?.sleeperPlayerId ?? `${entry.slotType}-bench`}
                        entry={entry}
                        currentWeek={currentWeek}
                      />
                    ))}
                  </ul>
                </div>
              )}
              {ir.length > 0 && (
                <div>
                  <p className="mb-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-slate-400">
                    Injured Reserve
                  </p>
                  <ul className="divide-y divide-white/10">
                    {ir.map((entry) => (
                      <RosterRow
                        key={entry.player?.sleeperPlayerId ?? `${entry.slotType}-ir`}
                        entry={entry}
                        currentWeek={currentWeek}
                      />
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <Card className="border-white/10 bg-slate-900 text-slate-50">
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-lg text-white">
            <ScrollText className="size-5 text-emerald-300" />
            Decision log
            {sortedDecisions.length > 0 && (
              <span className="text-xs font-normal text-slate-400">
                ({sortedDecisions.length})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {isLoadingDecisions ? (
            <p className="text-sm text-slate-300">Loading decisions…</p>
          ) : decisionsError ? (
            <p className="text-sm text-rose-300">Failed to load decisions: {decisionsError}</p>
          ) : sortedDecisions.length === 0 ? (
            <p className="text-sm text-slate-300">No decisions recorded yet.</p>
          ) : (
            <div className="space-y-2">
              {sortedDecisions.map((decision) => (
                <DecisionItem key={decision.decisionId} decision={decision} />
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </main>
  )
}

export default AgentPage
