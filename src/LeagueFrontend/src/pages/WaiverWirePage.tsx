import { useCallback, useEffect, useMemo, useState } from 'react'
import { ChevronLeft, ChevronRight, CircleCheck, CircleX, Clock3, Loader2, RefreshCw, RotateCcw, Users } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { apiBaseUrl } from '@/lib/config'

type LeagueState = {
  season: number
  week: number
}

type Player = {
  sleeperPlayerId: string
  fullName: string
  team: string | null
  position: string | null
}

type WaiverClaim = {
  waiverClaimId: string
  agentId: string
  claimOrder: number
  addSleeperPlayerId: string
  dropSleeperPlayerId: string | null
  priorityAtSubmission: number
  status: string
  failureReason: string | null
  submittedAtUtc: string
  processedAtUtc: string | null
}

type WaiverPriority = {
  agentId: string
  priority: number
}

type WaiverProcessStatus = {
  hasBeenProcessed: boolean
  claimsSucceeded: number
  claimsFailed: number
  completedAtUtc: string | null
}

type FetchState = 'loading' | 'success' | 'error'

function playerLabel(playerId: string | null, playersById: Map<string, Player>) {
  if (!playerId) return 'No drop'
  const player = playersById.get(playerId)
  if (!player) return playerId
  return `${player.fullName}${player.position ? ` · ${player.position}` : ''}${player.team ? ` · ${player.team}` : ''}`
}

function claimStatus(status: string) {
  switch (status.toLowerCase()) {
    case 'successful':
      return { label: 'Added', className: 'border-emerald-400/30 bg-emerald-400/10 text-emerald-200', icon: CircleCheck }
    case 'pending':
      return { label: 'Pending', className: 'border-amber-400/30 bg-amber-400/10 text-amber-200', icon: Clock3 }
    case 'superseded':
      return { label: 'Superseded', className: 'border-slate-500/40 bg-slate-500/10 text-slate-300', icon: RotateCcw }
    default:
      return { label: 'Unsuccessful', className: 'border-rose-400/30 bg-rose-400/10 text-rose-200', icon: CircleX }
  }
}

function WaiverWirePage() {
  const [leagueState, setLeagueState] = useState<LeagueState | null>(null)
  const [week, setWeek] = useState<number | null>(null)
  const [claims, setClaims] = useState<WaiverClaim[]>([])
  const [priority, setPriority] = useState<WaiverPriority[]>([])
  const [processStatus, setProcessStatus] = useState<WaiverProcessStatus | null>(null)
  const [players, setPlayers] = useState<Player[]>([])
  const [state, setState] = useState<FetchState>('loading')
  const [error, setError] = useState<string | null>(null)

  const fetchLeagueState = useCallback(async () => {
    const response = await fetch(`${apiBaseUrl}/api/league/state`)
    if (!response.ok) throw new Error(`Request failed with status ${response.status}`)
    const currentState = (await response.json()) as LeagueState
    setLeagueState(currentState)
    setWeek((selectedWeek) => selectedWeek ?? currentState.week)
  }, [])

  useEffect(() => {
    void fetchLeagueState().catch((ex: unknown) => {
      setError(ex instanceof Error ? ex.message : 'Unknown error')
      setState('error')
    })
  }, [fetchLeagueState])

  const fetchWaiverWire = useCallback(async () => {
    if (!leagueState || week === null) return

    setState('loading')
    setError(null)
    try {
      const [claimsResponse, priorityResponse, statusResponse, playersResponse] = await Promise.all([
        fetch(`${apiBaseUrl}/api/league/waivers/${leagueState.season}/${week}`),
        fetch(`${apiBaseUrl}/api/league/waivers/priority`),
        fetch(`${apiBaseUrl}/api/league/waivers/${leagueState.season}/${week}/status`),
        fetch(`${apiBaseUrl}/api/players?limit=1000`),
      ])
      const failedResponse = [claimsResponse, priorityResponse, statusResponse, playersResponse]
        .find((response) => !response.ok)
      if (failedResponse) throw new Error(`Request failed with status ${failedResponse.status}`)

      setClaims((await claimsResponse.json()) as WaiverClaim[])
      setPriority(((await priorityResponse.json()) as { priority: WaiverPriority[] }).priority)
      setProcessStatus((await statusResponse.json()) as WaiverProcessStatus)
      setPlayers((await playersResponse.json()) as Player[])
      setState('success')
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Unknown error')
      setState('error')
    }
  }, [leagueState, week])

  useEffect(() => {
    void fetchWaiverWire()
  }, [fetchWaiverWire])

  const playersById = useMemo(
    () => new Map(players.map((player) => [player.sleeperPlayerId, player])),
    [players],
  )
  const claimsInProcessingOrder = useMemo(
    () => [...claims].sort((a, b) =>
      a.priorityAtSubmission - b.priorityAtSubmission
      || a.agentId.localeCompare(b.agentId)
      || a.claimOrder - b.claimOrder),
    [claims],
  )

  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-3xl font-semibold tracking-tight text-white">Waiver Wire</h2>
          <p className="mt-1 text-sm text-slate-400">
            Review claims in their submitted waiver-priority order and revisit completed weeks.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => void fetchWaiverWire()}
          disabled={state === 'loading' || !leagueState}
          className="border-white/20 text-slate-300 hover:border-white/40 hover:text-white"
        >
          {state === 'loading' ? <Loader2 className="animate-spin" /> : <RefreshCw />}
          Refresh
        </Button>
      </div>

      {leagueState && week !== null && (
        <>
          <div className="flex items-center justify-center gap-2">
            <Button
              variant="ghost"
              size="icon"
              onClick={() => setWeek((value) => Math.max(1, (value ?? leagueState.week) - 1))}
              disabled={week <= 1 || state === 'loading'}
              className="text-slate-300 hover:bg-white/5 hover:text-white"
              aria-label="Previous waiver week"
            >
              <ChevronLeft />
            </Button>
            <span className="min-w-32 text-center text-lg font-semibold text-emerald-300">
              Season {leagueState.season} · Week {week}
            </span>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => setWeek((value) => Math.min(leagueState.week, (value ?? leagueState.week) + 1))}
              disabled={week >= leagueState.week || state === 'loading'}
              className="text-slate-300 hover:bg-white/5 hover:text-white"
              aria-label="Next waiver week"
            >
              <ChevronRight />
            </Button>
          </div>

          {state === 'loading' ? (
            <div className="flex items-center gap-2 text-slate-300"><Loader2 className="size-5 animate-spin" /> Loading waiver wire…</div>
          ) : state === 'error' ? (
            <p className="text-sm text-rose-300">Failed to load waiver wire: {error}</p>
          ) : (
            <section className="grid gap-6 xl:grid-cols-[0.7fr_1.3fr]">
              <div className="space-y-6">
                <Card className="border-white/10 bg-slate-900 text-slate-50">
                  <CardHeader className="pb-3">
                    <CardTitle className="text-xl text-white">Processing status</CardTitle>
                  </CardHeader>
                  <CardContent>
                    {processStatus?.hasBeenProcessed ? (
                      <div className="space-y-2">
                        <p className="font-semibold text-emerald-200">Processed</p>
                        <p className="text-sm text-slate-300">
                          {processStatus.claimsSucceeded} successful · {processStatus.claimsFailed} unsuccessful
                        </p>
                        {processStatus.completedAtUtc && (
                          <p className="text-xs text-slate-400">
                            Completed {new Date(processStatus.completedAtUtc).toLocaleString()}
                          </p>
                        )}
                      </div>
                    ) : (
                      <p className="text-sm text-amber-200">Waiting for the waiver wire to be processed.</p>
                    )}
                  </CardContent>
                </Card>

                <Card className="border-white/10 bg-slate-900 text-slate-50">
                  <CardHeader className="pb-3">
                    <CardTitle className="flex items-center gap-2 text-xl text-white"><Users className="size-5 text-emerald-300" /> Current priority</CardTitle>
                    <CardDescription className="text-slate-400">Live priority order for the next processing run.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {priority.length === 0 ? (
                      <p className="text-sm text-slate-300">No waiver priority has been seeded.</p>
                    ) : (
                      <ol className="space-y-2">
                        {priority.map((entry) => (
                          <li key={entry.agentId} className="flex items-center gap-3 rounded-md bg-slate-950 px-3 py-2">
                            <span className="w-5 text-right font-mono text-sm text-emerald-300">{entry.priority}</span>
                            <Link to={`/agents?agentId=${encodeURIComponent(entry.agentId)}`} className="text-sm font-medium text-slate-100 hover:text-emerald-300 hover:underline">
                              {entry.agentId}
                            </Link>
                          </li>
                        ))}
                      </ol>
                    )}
                  </CardContent>
                </Card>
              </div>

              <Card className="border-white/10 bg-slate-900 text-slate-50">
                <CardHeader>
                  <CardTitle className="text-xl text-white">Submitted claims</CardTitle>
                  <CardDescription className="text-slate-400">
                    Ordered by each claim&apos;s waiver priority at submission, then its claim preference.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {claimsInProcessingOrder.length === 0 ? (
                    <p className="text-sm text-slate-300">No claims were submitted for week {week}.</p>
                  ) : (
                    <div className="space-y-3">
                      {claimsInProcessingOrder.map((claim) => {
                        const status = claimStatus(claim.status)
                        const StatusIcon = status.icon
                        return (
                          <article key={claim.waiverClaimId} className="rounded-lg border border-white/10 bg-slate-950 p-4">
                            <div className="flex flex-wrap items-start justify-between gap-3">
                              <div>
                                <p className="font-semibold text-white">
                                  #{claim.priorityAtSubmission} priority · <Link to={`/agents?agentId=${encodeURIComponent(claim.agentId)}`} className="text-emerald-300 hover:underline">{claim.agentId}</Link>
                                </p>
                                <p className="mt-1 text-xs text-slate-400">Claim preference #{claim.claimOrder} · Submitted {new Date(claim.submittedAtUtc).toLocaleString()}</p>
                              </div>
                              <span className={`inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs font-medium ${status.className}`}>
                                <StatusIcon className="size-3.5" /> {status.label}
                              </span>
                            </div>
                            <dl className="mt-4 grid gap-3 sm:grid-cols-2">
                              <div><dt className="text-xs uppercase tracking-wider text-slate-500">Add</dt><dd className="mt-1 text-sm font-medium text-emerald-200">{playerLabel(claim.addSleeperPlayerId, playersById)}</dd></div>
                              <div><dt className="text-xs uppercase tracking-wider text-slate-500">Drop</dt><dd className="mt-1 text-sm font-medium text-rose-200">{playerLabel(claim.dropSleeperPlayerId, playersById)}</dd></div>
                            </dl>
                            {claim.failureReason && <p className="mt-3 text-xs text-rose-300">{claim.failureReason}</p>}
                          </article>
                        )
                      })}
                    </div>
                  )}
                </CardContent>
              </Card>
            </section>
          )}
        </>
      )}
    </main>
  )
}

export default WaiverWirePage
