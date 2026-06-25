import { useCallback, useEffect, useState } from 'react'
import { type ColumnDef } from '@tanstack/react-table'
import { AlertCircle, CheckCircle2, Database, KeyRound, Loader2, RefreshCw, XCircle } from 'lucide-react'

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

type YahooAuthStatus = {
  isConfigured: boolean
  hasAccessToken: boolean
  hasRefreshToken: boolean
  accessTokenExpiresAtUtc: string | null
  lastRefreshedAtUtc: string | null
  hasPendingAuthorizationState: boolean
}

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

type YahooSyncRun = {
  syncRunId: string
  gameKey: string
  season: number
  week: number | null
  startedAtUtc: string
  completedAtUtc: string | null
  status: string
  recordCount: number | null
  errorMessage: string | null
}

function hoursUntil(utc: string | null) {
  if (!utc) {
    return null
  }

  const expiresMs = new Date(utc).getTime()
  if (Number.isNaN(expiresMs)) {
    return null
  }

  return (expiresMs - Date.now()) / (1000 * 60 * 60)
}

function formatHoursUntil(utc: string | null) {
  const hours = hoursUntil(utc)
  if (hours === null) return 'No expiration available.'
  if (hours <= 0) return 'Token is expired or due for refresh.'
  return `refreshes in ${hours.toFixed(1)}h`
}

const agentColumns: ColumnDef<AgentProfile, unknown>[] = [
  { accessorKey: 'agentId', header: 'Agent ID' },
  { accessorKey: 'teamName', header: 'Team Name' },
  { accessorKey: 'modelName', header: 'Model' },
  { accessorKey: 'connection', header: 'Connection' },
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


function SyncRow({ label, status, completedAt, recordCount, errorMessage }: {
  label: string
  status: string | null
  completedAt: string | null
  recordCount: number | null
  errorMessage: string | null
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
      <div className="text-right text-xs text-slate-400 shrink-0">
        {recordCount !== null && <p>{recordCount.toLocaleString()} records</p>}
        {completedAt && <p>{timeAgo(completedAt)}</p>}
      </div>
    </div>
  )
}

function DataStatusCard() {
  const [state, setState] = useState<FetchState>('loading')
  const [sleeper, setSleeper] = useState<SleeperSyncState | null>(null)
  const [sportsData, setSportsData] = useState<SportsDataSyncRun | null>(null)

  const checkData = useCallback(async () => {
    setState('loading')
    try {
      const [sleeperRes, sportsDataRes] = await Promise.all([
        fetch(`${apiBaseUrl}/api/sync/sleeper/latest`),
        fetch(`${apiBaseUrl}/api/sync/sportsdata/latest`),
      ])

      const sleeperData = sleeperRes.ok ? (await sleeperRes.json()) as SleeperSyncState : null
      const sportsDataData = sportsDataRes.ok ? (await sportsDataRes.json()) as SportsDataSyncRun : null

      setSleeper(sleeperData)
      setSportsData(sportsDataData)
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
          </>
        )}
      </CardContent>
    </Card>
  )
}


function YahooStatusCard() {
  const [state, setState] = useState<FetchState>('loading')
  const [status, setStatus] = useState<YahooAuthStatus | null>(null)
  const [connectionOk, setConnectionOk] = useState<boolean | null>(null)
  const [syncRun, setSyncRun] = useState<YahooSyncRun | null>(null)

  const checkStatus = useCallback(async () => {
    setState('loading')
    setConnectionOk(null)
    try {
      const [statusResponse, connectionResponse, syncResponse] = await Promise.all([
        fetch(`${apiBaseUrl}/api/yahoo/auth/status`),
        fetch(`${apiBaseUrl}/api/yahoo/auth/test-connection`),
        fetch(`${apiBaseUrl}/api/sync/yahoo/latest`),
      ])

      if (!statusResponse.ok) {
        setState('error')
        return
      }

      setStatus((await statusResponse.json()) as YahooAuthStatus)
      setConnectionOk(connectionResponse.ok)
      setSyncRun(syncResponse.ok ? (await syncResponse.json()) as YahooSyncRun : null)
      setState('success')
    } catch {
      setState('error')
    }
  }, [])

  useEffect(() => {
    void checkStatus()
  }, [checkStatus])


  return (
    <Card className="border-white/10 bg-slate-900 text-slate-50">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-xl text-white">
            <KeyRound className="size-5 text-emerald-300" />
            Yahoo Status
          </CardTitle>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => void checkStatus()}
            className="text-slate-300 hover:bg-white/5 hover:text-white"
            aria-label="Re-check Yahoo status"
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
            <span className="text-sm font-medium">Could not reach the Yahoo auth status endpoint.</span>
          </div>
        )}
        {state === 'success' && status && (
          <>
            {status.hasAccessToken ? (
              <div className="flex items-center gap-2 text-emerald-300">
                <CheckCircle2 className="size-5" />
                <span className="text-sm font-medium">
                  Access token present — <span className="text-slate-300">{formatHoursUntil(status.accessTokenExpiresAtUtc)}</span>
                </span>
              </div>
            ) : (
              <div className="flex items-center gap-2 text-red-400">
                <AlertCircle className="size-5" />
                <span className="text-sm font-medium">No access token.</span>
              </div>
            )}
            {connectionOk === true ? (
              <div className="flex items-center gap-2 text-emerald-300">
                <CheckCircle2 className="size-5" />
                <span className="text-sm font-medium">Yahoo API connection successful.</span>
              </div>
            ) : (
              <div className="flex items-center gap-2 text-red-400">
                <AlertCircle className="size-5" />
                <span className="text-sm font-medium">Yahoo API connection failed.</span>
              </div>
            )}
            <SyncRow
              label={syncRun ? `Yahoo Sync — S${syncRun.season} W${syncRun.week ?? '—'}` : 'Yahoo Sync'}
              status={syncRun?.status ?? null}
              completedAt={syncRun?.completedAtUtc ?? null}
              recordCount={syncRun?.recordCount ?? null}
              errorMessage={syncRun?.errorMessage ?? null}
            />
          </>
        )}
      </CardContent>
    </Card>
  )
}

function AdminPage() {
  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
      <h2 className="text-3xl font-semibold tracking-tight text-white">Admin</h2>

      <section className="grid gap-6 md:grid-cols-2">
        <DataStatusCard />
        <YahooStatusCard />
      </section>

      <AgentsTable />
    </main>
  )
}

export default AdminPage
