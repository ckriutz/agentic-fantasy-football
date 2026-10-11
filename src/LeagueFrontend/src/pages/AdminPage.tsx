import { useCallback, useEffect, useState } from 'react'
import { type ColumnDef } from '@tanstack/react-table'
import { Activity, AlertCircle, CalendarDays, CheckCircle2, Database, Loader2, RefreshCw, XCircle, Zap } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { DataTable } from '@/components/ui/data-table'
import { apiBaseUrl } from '@/lib/config'

type FetchState = 'loading' | 'success' | 'error'

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
  updatedAtUtc: string
  updatedBy: string
}

const leagueStatePhaseOptions = [
  { value: 'drafting', label: 'Drafting' },
  { value: 'games_locked', label: 'Games Locked' },
  { value: 'waiver_window', label: 'Waiver Window' },
  { value: 'free_agency', label: 'Free Agency' },
  { value: 'complete', label: 'Complete' },
]

type SleeperSyncState = {
  syncRunId: string | null
  status: string
  lastAttemptedAtUtc: string | null
  lastSuccessfulSyncAtUtc: string | null
  recordCount: number | null
  errorMessage: string | null
}

type SportsDataSyncRun = {
  syncRunId: string
  startedAtUtc: string
  completedAtUtc: string | null
  status: string
  recordCount: number | null
  errorMessage: string | null
}

type FantasyProsSyncRun = SportsDataSyncRun

type LeagueRunnerAgentResult = {
  agentId: string
  succeeded: boolean
  errorMessage: string | null
  recordedAtUtc: string
}

type LeagueRunnerRun = {
  runnerRunId: string
  mode: string
  status: string
  season: number | null
  week: number | null
  phase: string | null
  summary: string | null
  errorMessage: string | null
  startedAtUtc: string
  completedAtUtc: string | null
  agents: LeagueRunnerAgentResult[]
}

type FantasyProsPointsSyncRun = {
  season: number
  endWeek: number
  startedAtUtc: string
  completedAtUtc: string | null
  status: string
  recordCount: number | null
  matchedPlayerCount: number | null
  unmatchedPlayerCount: number | null
  unmatchedDstCount: number | null
  servedScoring: string | null
  errorMessage: string | null
}

function timeAgo(utc: string | null) {
  if (!utc) return null
  const ms = Date.now() - new Date(utc).getTime()
  if (ms < 0) return 'just now'
  const minutes = Math.floor(ms / 60000)
  if (minutes < 1) return 'less than a minute ago'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

const agentColumns: ColumnDef<AgentProfile, unknown>[] = [
  {
    accessorKey: 'agentId',
    header: 'Agent ID',
    cell: ({ row, getValue }) => (
      <Link
        to={`/agents?agentId=${encodeURIComponent(row.original.agentId)}`}
        className="font-mono text-emerald-300 hover:underline"
      >
        {getValue<string>()}
      </Link>
    ),
  },
  { accessorKey: 'teamName', header: 'Team Name' },
  { accessorKey: 'modelName', header: 'Model', meta: { className: 'hidden md:table-cell' } },
  { accessorKey: 'connection', header: 'Connection', meta: { className: 'hidden lg:table-cell' } },
  {
    accessorKey: 'isEnabled',
    header: 'Enabled',
    cell: ({ getValue }) =>
      getValue<boolean>() ? (
        <CheckCircle2 className="size-4 text-emerald-300" />
      ) : (
        <XCircle className="size-4 text-slate-500" />
      ),
  },
  {
    accessorKey: 'isBootstrapped',
    header: 'Bootstrapped',
    meta: { className: 'hidden sm:table-cell' },
    cell: ({ getValue }) =>
      getValue<boolean>() ? (
        <CheckCircle2 className="size-4 text-emerald-300" />
      ) : (
        <XCircle className="size-4 text-slate-500" />
      ),
  },
  {
    accessorKey: 'lastUpdatedAt',
    header: 'Last Updated',
    meta: { className: 'hidden lg:table-cell' },
    cell: ({ getValue }) => new Date(getValue<string>()).toLocaleString(),
  },
]

function AgentsTable() {
  const [state, setState] = useState<FetchState>('loading')
  const [agents, setAgents] = useState<AgentProfile[]>([])

  const fetchAgents = useCallback(async () => {
    setState('loading')
    try {
      const response = await fetch(`${apiBaseUrl}/api/agent-profiles?enabledOnly=false`)
      if (!response.ok) {
        setState('error')
        return
      }

      const data = (await response.json()) as AgentProfile[]
      setAgents(data)
      setState('success')
    } catch {
      setState('error')
    }
  }, [])

  useEffect(() => {
    void fetchAgents()
  }, [fetchAgents])

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="text-xl font-semibold text-white">Agents</h3>
        <Button
          variant="ghost"
          size="icon"
          onClick={() => void fetchAgents()}
          className="text-slate-300 hover:bg-white/5 hover:text-white"
          aria-label="Refresh agents"
        >
          <RefreshCw className="size-4" />
        </Button>
      </div>

      {state === 'loading' && (
        <div className="flex items-center gap-2 text-slate-300">
          <Loader2 className="size-5 animate-spin" />
          <span className="text-sm">Loading agents…</span>
        </div>
      )}
      {state === 'error' && (
        <div className="flex items-center gap-2 text-red-400">
          <AlertCircle className="size-5" />
          <span className="text-sm font-medium">Could not load agent profiles.</span>
        </div>
      )}
      {state === 'success' && <DataTable columns={agentColumns} data={agents} />}
    </div>
  )
}

function LeagueStateCard() {
  const [state, setState] = useState<FetchState>('loading')
  const [leagueState, setLeagueState] = useState<LeagueState | null>(null)
  const [savingPhase, setSavingPhase] = useState(false)
  const [processingWaivers, setProcessingWaivers] = useState(false)
  const [waiverResult, setWaiverResult] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const fetchLeagueState = useCallback(async () => {
    setState('loading')
    setError(null)
    try {
      const response = await fetch(`${apiBaseUrl}/api/league/state`)
      if (!response.ok) {
        throw new Error(`Request failed with status ${response.status}`)
      }
      setLeagueState((await response.json()) as LeagueState)
      setState('success')
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Unknown error')
      setState('error')
    }
  }, [])

  useEffect(() => {
    void fetchLeagueState()
  }, [fetchLeagueState])

  const updatePhase = useCallback(async (phase: string) => {
    if (!leagueState || phase === leagueState.phase) return

    const previousState = leagueState
    setSavingPhase(true)
    setError(null)
    setLeagueState({ ...leagueState, phase })

    try {
      const response = await fetch(`${apiBaseUrl}/api/league/state`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          season: previousState.season,
          week: previousState.week,
          phase,
          updatedBy: 'manual',
        }),
      })

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Request failed with status ${response.status}`)
      }

      setLeagueState((await response.json()) as LeagueState)
      setState('success')
    } catch (ex) {
      setLeagueState(previousState)
      setError(ex instanceof Error ? ex.message : 'Unknown error')
      setState('error')
    } finally {
      setSavingPhase(false)
    }
  }, [leagueState])

  const processWaivers = useCallback(async () => {
    if (!leagueState) return

    setProcessingWaivers(true)
    setWaiverResult(null)
    setError(null)
    try {
      const response = await fetch(
        `${apiBaseUrl}/api/league/waivers/${leagueState.season}/${leagueState.week}/process`,
        { method: 'POST' },
      )
      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Request failed with status ${response.status}`)
      }

      const result = (await response.json()) as {
        claimsProcessed: number
        claimsSucceeded: number
        claimsFailed: number
      }
      setWaiverResult(
        `Processed ${result.claimsProcessed} claims: ${result.claimsSucceeded} successful, ${result.claimsFailed} unsuccessful.`,
      )
      await fetchLeagueState()
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Unknown error')
    } finally {
      setProcessingWaivers(false)
    }
  }, [fetchLeagueState, leagueState])

  return (
    <Card className="border-white/10 bg-slate-900 text-slate-50">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-xl text-white">
            <CalendarDays className="size-5 text-emerald-300" />
            League State
          </CardTitle>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => void fetchLeagueState()}
            className="text-slate-300 hover:bg-white/5 hover:text-white"
            aria-label="Refresh league state"
          >
            <RefreshCw className="size-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        {state === 'loading' && (
          <div className="flex items-center gap-2 text-slate-300">
            <Loader2 className="size-5 animate-spin" />
            <span className="text-sm">Loading league state…</span>
          </div>
        )}
        {state === 'error' && !leagueState && (
          <div className="flex items-center gap-2 text-red-400">
            <AlertCircle className="size-5" />
            <span className="text-sm font-medium">Could not load league state: {error}</span>
          </div>
        )}
        {leagueState && (
          <div className="flex flex-wrap items-center gap-x-6 gap-y-3 rounded-lg border border-white/10 bg-slate-950 px-4 py-3">
            <div className="flex items-baseline gap-2">
              <span className="text-xs font-medium uppercase tracking-[0.2em] text-slate-400">Season</span>
              <span className="text-lg font-semibold text-white">{leagueState.season}</span>
            </div>
            <div className="flex items-baseline gap-2">
              <span className="text-xs font-medium uppercase tracking-[0.2em] text-slate-400">Week</span>
              <span className="text-lg font-semibold text-white">{leagueState.week}</span>
            </div>
            <label className="flex items-center gap-2">
              <span className="text-xs font-medium uppercase tracking-[0.2em] text-slate-400">Phase</span>
              <select
                value={leagueState.phase}
                disabled={savingPhase}
                onChange={(event) => void updatePhase(event.target.value)}
                className="rounded-md border border-white/10 bg-slate-900 px-3 py-1.5 text-sm font-semibold text-emerald-200 outline-none hover:border-white/20 disabled:opacity-50"
              >
                {leagueStatePhaseOptions.map((option) => (
                  <option key={option.value} value={option.value} className="bg-slate-900 text-slate-100">
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={savingPhase || processingWaivers}
              onClick={() => void processWaivers()}
              className="border-emerald-400/40 bg-emerald-400/10 text-emerald-200 hover:bg-emerald-400/20 hover:text-emerald-100"
            >
              {processingWaivers ? <Loader2 className="animate-spin" /> : <Zap />}
              Process Waiver Wire
            </Button>
            {savingPhase && <Loader2 className="size-4 animate-spin text-slate-400" />}
            {waiverResult && <p className="basis-full text-xs text-emerald-300">{waiverResult}</p>}
            {error && (
              <p className="basis-full text-xs text-red-400">
                League state update failed: {error}
              </p>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}


function SyncRow({ label, status, completedAt, recordCount, errorMessage, action }: {
  label: string
  status: string | null
  completedAt: string | null
  recordCount: number | null
  errorMessage: string | null
  action?: React.ReactNode
}) {
  const isOk = status?.toLowerCase() === 'succeeded' || status?.toLowerCase() === 'skipped'
  const isError = !!errorMessage || status?.toLowerCase() === 'failed' || status?.toLowerCase() === 'error'

  return (
    <div className="flex items-start justify-between gap-4 rounded-lg border border-white/10 bg-slate-950 px-4 py-3">
      <div className="space-y-1">
        <p className="text-xs font-medium uppercase tracking-widest text-slate-400">{label}</p>
        <div className="flex items-center gap-2">
          {isOk ? (
            <CheckCircle2 className="size-4 shrink-0 text-emerald-300" />
          ) : isError ? (
            <AlertCircle className="size-4 shrink-0 text-red-400" />
          ) : (
            <AlertCircle className="size-4 shrink-0 text-amber-400" />
          )}
          <span className={`text-sm font-medium ${isOk ? 'text-emerald-300' : isError ? 'text-red-400' : 'text-amber-300'}`}>
            {status ?? 'Never run'}
          </span>
        </div>
        {errorMessage && <p className="text-xs text-red-400">{errorMessage}</p>}
      </div>
      <div className="flex items-center gap-3 shrink-0">
        <div className="text-right text-xs text-slate-400">
          {recordCount !== null && <p>{recordCount.toLocaleString()} records</p>}
          {completedAt && <p>{timeAgo(completedAt)}</p>}
        </div>
        {action}
      </div>
    </div>
  )
}

function DataStatusCard() {
  const [state, setState] = useState<FetchState>('loading')
  const [sleeper, setSleeper] = useState<SleeperSyncState | null>(null)
  const [sportsData, setSportsData] = useState<SportsDataSyncRun | null>(null)
  const [fantasyPros, setFantasyPros] = useState<FantasyProsSyncRun | null>(null)

  const checkData = useCallback(async () => {
    setState('loading')
    try {
      const [sleeperRes, sportsDataRes, fantasyProsRes] = await Promise.all([
        fetch(`${apiBaseUrl}/api/sync/sleeper/latest`),
        fetch(`${apiBaseUrl}/api/sync/sportsdata/latest`),
        fetch(`${apiBaseUrl}/api/sync/fantasypros/latest`),
      ])

      const sleeperData = sleeperRes.ok ? (await sleeperRes.json()) as SleeperSyncState : null
      const sportsDataData = sportsDataRes.ok ? (await sportsDataRes.json()) as SportsDataSyncRun : null
      const fantasyProsData = fantasyProsRes.ok ? (await fantasyProsRes.json()) as FantasyProsSyncRun : null

      setSleeper(sleeperData)
      setSportsData(sportsDataData)
      setFantasyPros(fantasyProsData)
      setState('success')
    } catch {
      setState('error')
    }
  }, [])

  useEffect(() => {
    void checkData()
  }, [checkData])

  return (
    <Card className="border-white/10 bg-slate-900 text-slate-50">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-xl text-white">
            <Database className="size-5 text-emerald-300" />
            Data Status
          </CardTitle>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => void checkData()}
            className="text-slate-300 hover:bg-white/5 hover:text-white"
            aria-label="Re-check data status"
          >
            <RefreshCw className="size-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {state === 'loading' && (
          <div className="flex items-center gap-2 text-slate-300">
            <Loader2 className="size-5 animate-spin" />
            <span className="text-sm">Checking…</span>
          </div>
        )}
        {state === 'error' && (
          <div className="flex items-center gap-2 text-red-400">
            <AlertCircle className="size-5" />
            <span className="text-sm font-medium">Could not reach data sync endpoints.</span>
          </div>
        )}
        {state === 'success' && (
          <>
            <SyncRow
              label="Sleeper"
              status={sleeper?.status ?? null}
              completedAt={sleeper?.lastSuccessfulSyncAtUtc ?? null}
              recordCount={sleeper?.recordCount ?? null}
              errorMessage={sleeper?.errorMessage ?? null}
            />
            <SyncRow
              label="SportsData"
              status={sportsData?.status ?? null}
              completedAt={sportsData?.completedAtUtc ?? null}
              recordCount={sportsData?.recordCount ?? null}
              errorMessage={sportsData?.errorMessage ?? null}
            />
            <SyncRow
              label="FantasyPros"
              status={fantasyPros?.status ?? null}
              completedAt={fantasyPros?.completedAtUtc ?? null}
              recordCount={fantasyPros?.recordCount ?? null}
              errorMessage={fantasyPros?.errorMessage ?? null}
            />
          </>
        )}
      </CardContent>
    </Card>
  )
}

function WeeklyScoresCard() {
  const [state, setState] = useState<FetchState>('loading')
  const [season, setSeason] = useState<number | null>(null)
  const [syncRun, setSyncRun] = useState<FantasyProsPointsSyncRun | null>(null)
  const [error, setError] = useState<string | null>(null)

  const checkWeeklyScores = useCallback(async () => {
    setState('loading')
    setError(null)
    try {
      const leagueResponse = await fetch(`${apiBaseUrl}/api/league/state`)
      if (!leagueResponse.ok) {
        throw new Error(`Could not load league state (${leagueResponse.status})`)
      }

      const leagueState = (await leagueResponse.json()) as LeagueState
      setSeason(leagueState.season)

      const syncResponse = await fetch(`${apiBaseUrl}/api/sync/fantasypros/points/latest?season=${leagueState.season}`)
      if (syncResponse.status === 404) {
        setSyncRun(null)
        setState('success')
        return
      }
      if (!syncResponse.ok) {
        throw new Error(`Could not load weekly scores status (${syncResponse.status})`)
      }

      setSyncRun((await syncResponse.json()) as FantasyProsPointsSyncRun)
      setState('success')
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Unknown error')
      setState('error')
    }
  }, [])

  useEffect(() => {
    void checkWeeklyScores()
  }, [checkWeeklyScores])

  const status = syncRun?.status ?? null
  const succeeded = status?.toLowerCase() === 'succeeded'
  const happenedAt = syncRun?.completedAtUtc ?? syncRun?.startedAtUtc ?? null

  return (
    <Card className="border-white/10 bg-slate-900 text-slate-50">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-xl text-white">
            <Database className="size-5 text-emerald-300" />
            Weekly Scores
          </CardTitle>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => void checkWeeklyScores()}
            className="text-slate-300 hover:bg-white/5 hover:text-white"
            aria-label="Refresh weekly scores status"
          >
            <RefreshCw className="size-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        {state === 'loading' && (
          <div className="flex items-center gap-2 text-slate-300">
            <Loader2 className="size-5 animate-spin" />
            <span className="text-sm">Checking weekly scores…</span>
          </div>
        )}
        {state === 'error' && (
          <div className="flex items-center gap-2 text-red-400">
            <AlertCircle className="size-5" />
            <span className="text-sm font-medium">{error ?? 'Could not load weekly scores status.'}</span>
          </div>
        )}
        {state === 'success' && !syncRun && (
          <p className="text-sm text-slate-400">No weekly scores sync has run for season {season ?? '—'}.</p>
        )}
        {state === 'success' && syncRun && (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
              <div className="flex items-center gap-2">
                {succeeded ? (
                  <CheckCircle2 className="size-5 text-emerald-300" />
                ) : (
                  <AlertCircle className="size-5 text-red-400" />
                )}
                <span className={`font-medium ${succeeded ? 'text-emerald-300' : 'text-red-400'}`}>
                  {syncRun.status}
                </span>
              </div>
              <span className="text-sm text-slate-400">
                Season {syncRun.season}, through Week {syncRun.endWeek}
                {syncRun.servedScoring ? ` (${syncRun.servedScoring})` : ''}
              </span>
              {happenedAt && <span className="text-sm text-slate-400">{timeAgo(happenedAt)}</span>}
            </div>
            {succeeded ? (
              <div className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
                <div><p className="text-slate-400">Records</p><p className="font-semibold text-white">{syncRun.recordCount?.toLocaleString() ?? '—'}</p></div>
                <div><p className="text-slate-400">Matched</p><p className="font-semibold text-emerald-300">{syncRun.matchedPlayerCount?.toLocaleString() ?? '—'}</p></div>
                <div><p className="text-slate-400">Unmatched</p><p className="font-semibold text-amber-300">{syncRun.unmatchedPlayerCount?.toLocaleString() ?? '—'}</p></div>
                <div><p className="text-slate-400">Unmatched DST</p><p className="font-semibold text-amber-300">{syncRun.unmatchedDstCount?.toLocaleString() ?? '—'}</p></div>
              </div>
            ) : (
              <p className="text-sm text-red-400">{syncRun.errorMessage ?? 'Weekly scores sync did not succeed.'}</p>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function SeasonRunnerCard() {
  const [state, setState] = useState<FetchState>('loading')
  const [run, setRun] = useState<LeagueRunnerRun | null>(null)
  const [error, setError] = useState<string | null>(null)

  const checkRun = useCallback(async () => {
    setState('loading')
    setError(null)
    try {
      const response = await fetch(`${apiBaseUrl}/api/runner/runs/latest`)
      if (response.status === 404) {
        setRun(null)
        setState('success')
        return
      }
      if (!response.ok) {
        throw new Error(`Could not load season runner status (${response.status})`)
      }

      setRun((await response.json()) as LeagueRunnerRun)
      setState('success')
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Unknown error')
      setState('error')
    }
  }, [])

  useEffect(() => {
    void checkRun()
  }, [checkRun])

  const status = run?.status.toLowerCase()
  const isOk = status === 'succeeded' || status === 'skipped'
  const isRunning = status === 'running'
  const succeededAgents = run?.agents.filter((agent) => agent.succeeded).length ?? 0
  const failedAgents = run?.agents.filter((agent) => !agent.succeeded) ?? []
  const happenedAt = run?.completedAtUtc ?? run?.startedAtUtc ?? null

  return (
    <Card className="border-white/10 bg-slate-900 text-slate-50">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-xl text-white">
            <Activity className="size-5 text-emerald-300" />
            Season Runner
          </CardTitle>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => void checkRun()}
            className="text-slate-300 hover:bg-white/5 hover:text-white"
            aria-label="Refresh season runner status"
          >
            <RefreshCw className="size-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        {state === 'loading' && (
          <div className="flex items-center gap-2 text-slate-300">
            <Loader2 className="size-5 animate-spin" />
            <span className="text-sm">Checking season runner…</span>
          </div>
        )}
        {state === 'error' && (
          <div className="flex items-center gap-2 text-red-400">
            <AlertCircle className="size-5" />
            <span className="text-sm font-medium">{error ?? 'Could not load season runner status.'}</span>
          </div>
        )}
        {state === 'success' && !run && (
          <p className="text-sm text-slate-400">The season runner has not recorded a run yet.</p>
        )}
        {state === 'success' && run && (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
              <div className="flex items-center gap-2">
                {isOk ? (
                  <CheckCircle2 className="size-5 text-emerald-300" />
                ) : isRunning ? (
                  <Loader2 className="size-5 animate-spin text-amber-300" />
                ) : (
                  <AlertCircle className="size-5 text-red-400" />
                )}
                <span className={`font-medium ${isOk ? 'text-emerald-300' : isRunning ? 'text-amber-300' : 'text-red-400'}`}>
                  {run.status}
                </span>
              </div>
              <span className="text-sm text-slate-400">
                {run.mode}
                {run.season !== null ? `, season ${run.season}` : ''}
                {run.week !== null ? `, week ${run.week}` : ''}
                {run.phase ? `, ${run.phase}` : ''}
              </span>
              {happenedAt && <span className="text-sm text-slate-400">{timeAgo(happenedAt)}</span>}
            </div>
            <p className="text-sm text-slate-300">
              {succeededAgents}/{run.agents.length} agents succeeded
            </p>
            {run.summary && <p className="text-sm text-slate-300">{run.summary}</p>}
            {run.errorMessage && <p className="text-sm text-red-400">{run.errorMessage}</p>}
            {failedAgents.length > 0 && (
              <div className="space-y-1">
                {failedAgents.map((agent) => (
                  <p key={agent.agentId} className="text-sm text-red-400">
                    {agent.agentId}: {agent.errorMessage ?? 'failed'}
                  </p>
                ))}
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function AdminPage() {
  return (
    <main className="flex flex-1 flex-col gap-6 px-4 py-6 sm:px-6 sm:py-10 xl:px-10">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-semibold tracking-tight text-white sm:text-3xl">Admin</h2>
      </div>

      <LeagueStateCard />
      <SeasonRunnerCard />

      <section className="grid gap-6 md:grid-cols-[1fr_0.85fr]">
        <DataStatusCard />
        <WeeklyScoresCard />
      </section>

      <AgentsTable />
    </main>
  )
}

export default AdminPage
