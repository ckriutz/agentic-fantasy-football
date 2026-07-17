import { useEffect, useMemo, useState } from 'react'
import { AlertCircle, ArrowLeft, Loader2, Swords } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'

import AgentAvatar from '@/components/AgentAvatar'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { apiBaseUrl } from '@/lib/config'

type Matchup = {
  matchupId: number
  week: number
  homeAgentId: string
  awayAgentId: string
  homePoints: number
  awayPoints: number
  isComplete: boolean
}

type AgentProfile = {
  agentId: string
  teamName: string
  modelName: string
}

type LeagueState = {
  season: number
}

type RosterEntry = {
  player: {
    sleeperPlayerId: string
    fullName: string | null
    team: string | null
    position: string | null
    injuryStatus: string | null
  } | null
  slotType: string | null
  weeklyPoints: Record<string, number> | null
}

const STARTER_SLOTS = ['QB1', 'RB1', 'RB2', 'WR1', 'WR2', 'TE1', 'FLEX1', 'K1', 'DEF1']

function getWeeklyPoints(entry: RosterEntry | undefined, week: number) {
  return entry?.weeklyPoints?.[String(week)] ?? 0
}

function scoreColor(points: number, opponentPoints: number | null) {
  if (opponentPoints === null) return 'text-slate-200'
  if (points > opponentPoints) return 'text-emerald-300'
  if (points < opponentPoints) return 'text-rose-300'
  return 'text-slate-200'
}

function formatSlot(slotType: string) {
  return slotType.replace(/\d+$/, '')
}

function getStarterBySlot(roster: RosterEntry[]) {
  return new Map(
    roster
      .filter((entry) => entry.slotType && STARTER_SLOTS.includes(entry.slotType))
      .map((entry) => [entry.slotType!, entry]),
  )
}

function getBench(roster: RosterEntry[]) {
  return roster
    .filter((entry) => !entry.slotType || !STARTER_SLOTS.includes(entry.slotType))
    .sort((left, right) =>
      (left.player?.fullName ?? '').localeCompare(right.player?.fullName ?? ''),
    )
}

function PlayerSlot({ entry, week, opponentPoints, align }: { entry: RosterEntry | undefined; week: number; opponentPoints: number | null; align: 'left' | 'right' }) {
  if (!entry?.player) {
    return <p className={`text-sm text-slate-500 ${align === 'right' ? 'text-right' : ''}`}>No player</p>
  }

  const points = getWeeklyPoints(entry, week)
  const player = entry.player

  return (
    <div className={`min-w-0 ${align === 'right' ? 'text-right' : ''}`}>
      <div className={`grid min-w-0 items-center gap-2 ${align === 'right' ? 'grid-cols-[auto_minmax(0,1fr)]' : 'grid-cols-[minmax(0,1fr)_auto]'}`}>
        {align === 'right' && (
          <span className={`shrink-0 font-mono text-sm font-semibold ${scoreColor(points, opponentPoints)}`}>
            {points.toFixed(2)}
          </span>
        )}
        <Link
          to={`/players/${player.sleeperPlayerId}`}
          className={`min-w-0 truncate text-sm font-semibold text-white hover:text-emerald-300 ${align === 'right' ? 'text-right' : ''}`}
        >
          {player.fullName ?? 'Unknown player'}
        </Link>
        {align === 'left' && (
          <span className={`shrink-0 font-mono text-sm font-semibold ${scoreColor(points, opponentPoints)}`}>
            {points.toFixed(2)}
          </span>
        )}
      </div>
      <p className="text-xs text-slate-400">
        {[player.position, player.team, player.injuryStatus].filter(Boolean).join(' · ')}
      </p>
    </div>
  )
}

function TeamHeader({ agent, points, opponentPoints, align }: { agent: AgentProfile | undefined; points: number; opponentPoints: number; align: 'left' | 'right' }) {
  return (
    <div className={`flex min-w-0 items-center gap-3 ${align === 'right' ? 'flex-row-reverse text-right' : ''}`}>
      {agent && <AgentAvatar agentId={agent.agentId} />}
      <div className="min-w-0 flex-1">
        <p className="truncate text-base font-semibold text-white">{agent?.teamName ?? 'Unknown team'}</p>
        <p className="truncate text-xs text-slate-400">{agent?.modelName ?? ''}</p>
      </div>
      <p className={`font-mono text-2xl font-semibold ${scoreColor(points, opponentPoints)}`}>{points.toFixed(2)}</p>
    </div>
  )
}

function MatchupPage() {
  const { matchupId: matchupIdParameter } = useParams()
  const matchupId = Number(matchupIdParameter)
  const [matchup, setMatchup] = useState<Matchup | null>(null)
  const [profiles, setProfiles] = useState<AgentProfile[]>([])
  const [season, setSeason] = useState<number | null>(null)
  const [homeRoster, setHomeRoster] = useState<RosterEntry[]>([])
  const [awayRoster, setAwayRoster] = useState<RosterEntry[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    if (!Number.isInteger(matchupId) || matchupId <= 0) {
      setError('The matchup ID is invalid.')
      setIsLoading(false)
      return
    }

    const controller = new AbortController()

    async function fetchMatchup() {
      try {
        setIsLoading(true)
        setError(null)
        const [scheduleResponse, profilesResponse, leagueStateResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/league/schedule`, { signal: controller.signal }),
          fetch(`${apiBaseUrl}/api/agent-profiles?enabledOnly=false`, { signal: controller.signal }),
          fetch(`${apiBaseUrl}/api/league/state`, { signal: controller.signal }),
        ])

        if (!scheduleResponse.ok || !profilesResponse.ok || !leagueStateResponse.ok) {
          const response = !scheduleResponse.ok
            ? scheduleResponse
            : !profilesResponse.ok
              ? profilesResponse
              : leagueStateResponse
          throw new Error(`Request failed with status ${response.status}`)
        }

        const schedule = (await scheduleResponse.json()) as Matchup[]
        const selectedMatchup = schedule.find((candidate) => candidate.matchupId === matchupId)
        if (!selectedMatchup) {
          throw new Error('Matchup not found.')
        }

        const [homeRosterResponse, awayRosterResponse] = await Promise.all([
          fetch(`${apiBaseUrl}/api/rosters/${encodeURIComponent(selectedMatchup.homeAgentId)}`, { signal: controller.signal }),
          fetch(`${apiBaseUrl}/api/rosters/${encodeURIComponent(selectedMatchup.awayAgentId)}`, { signal: controller.signal }),
        ])
        if (!homeRosterResponse.ok || !awayRosterResponse.ok) {
          const response = !homeRosterResponse.ok ? homeRosterResponse : awayRosterResponse
          throw new Error(`Request failed with status ${response.status}`)
        }

        const leagueState = (await leagueStateResponse.json()) as LeagueState
        setMatchup(selectedMatchup)
        setProfiles((await profilesResponse.json()) as AgentProfile[])
        setSeason(leagueState.season)
        setHomeRoster((await homeRosterResponse.json()) as RosterEntry[])
        setAwayRoster((await awayRosterResponse.json()) as RosterEntry[])
      } catch (fetchError) {
        if ((fetchError as { name?: string }).name === 'AbortError') {
          return
        }
        setError(fetchError instanceof Error ? fetchError.message : 'Unknown error')
      } finally {
        setIsLoading(false)
      }
    }

    void fetchMatchup()
    return () => controller.abort()
  }, [matchupId])

  const profilesByAgentId = useMemo(
    () => new Map(profiles.map((profile) => [profile.agentId, profile])),
    [profiles],
  )
  const homeStartersBySlot = useMemo(() => getStarterBySlot(homeRoster), [homeRoster])
  const awayStartersBySlot = useMemo(() => getStarterBySlot(awayRoster), [awayRoster])
  const homeBench = useMemo(() => getBench(homeRoster), [homeRoster])
  const awayBench = useMemo(() => getBench(awayRoster), [awayRoster])

  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
      <Link to="/" className="inline-flex w-fit items-center gap-2 text-sm font-medium text-slate-300 hover:text-white">
        <ArrowLeft className="size-4" />
        Back to overview
      </Link>

      {isLoading && (
        <div className="flex items-center gap-2 text-slate-300">
          <Loader2 className="size-5 animate-spin" />
          <span className="text-sm">Loading matchup…</span>
        </div>
      )}

      {error && (
        <div className="flex items-center gap-2 text-rose-300">
          <AlertCircle className="size-5" />
          <span className="text-sm font-medium">Could not load matchup: {error}</span>
        </div>
      )}

      {matchup && !isLoading && !error && (
        <>
          <div>
            <h2 className="flex items-center gap-2 text-3xl font-semibold tracking-tight text-white">
              <Swords className="size-7 text-emerald-300" />
              Matchup
            </h2>
            <p className="mt-1 text-sm text-slate-400">
              Season {season ?? '—'} · Week {matchup.week} · {matchup.isComplete ? 'Final' : 'Live'}
            </p>
          </div>

          <Card className="border-white/10 bg-slate-900 text-slate-50">
            <CardHeader>
              <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-6">
                <TeamHeader
                  agent={profilesByAgentId.get(matchup.homeAgentId)}
                  points={matchup.homePoints}
                  opponentPoints={matchup.awayPoints}
                  align="left"
                />
                <TeamHeader
                  agent={profilesByAgentId.get(matchup.awayAgentId)}
                  points={matchup.awayPoints}
                  opponentPoints={matchup.homePoints}
                  align="right"
                />
              </div>
            </CardHeader>
            <CardContent>
              <div className="divide-y divide-white/10 rounded-lg border border-white/10 bg-slate-950">
                {STARTER_SLOTS.map((slot) => {
                  const homeEntry = homeStartersBySlot.get(slot)
                  const awayEntry = awayStartersBySlot.get(slot)

                  return (
                    <div key={slot} className="grid grid-cols-[minmax(0,1fr)_3.5rem_minmax(0,1fr)] items-center gap-3 px-4 py-3">
                      <PlayerSlot
                        entry={homeEntry}
                        week={matchup.week}
                        opponentPoints={getWeeklyPoints(awayEntry, matchup.week)}
                        align="right"
                      />
                      <span className="text-center text-[10px] font-semibold uppercase tracking-widest text-slate-500">
                        {formatSlot(slot)}
                      </span>
                      <PlayerSlot
                        entry={awayEntry}
                        week={matchup.week}
                        opponentPoints={getWeeklyPoints(homeEntry, matchup.week)}
                        align="left"
                      />
                    </div>
                  )
                })}
              </div>
            </CardContent>
          </Card>

          <section className="grid gap-6 lg:grid-cols-2">
            <Card className="border-white/10 bg-slate-900 text-slate-50">
              <CardHeader>
                <CardTitle className="text-lg text-white">
                  {profilesByAgentId.get(matchup.homeAgentId)?.teamName ?? 'Home team'} bench
                </CardTitle>
              </CardHeader>
              <CardContent>
                {homeBench.length === 0 ? (
                  <p className="text-sm text-slate-400">No bench players.</p>
                ) : (
                  <ul className="divide-y divide-white/10">
                    {homeBench.map((entry) => (
                      <li key={entry.player?.sleeperPlayerId ?? entry.slotType} className="py-3 first:pt-0 last:pb-0">
                        <PlayerSlot entry={entry} week={matchup.week} opponentPoints={null} align="left" />
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <Card className="border-white/10 bg-slate-900 text-slate-50">
              <CardHeader>
                <CardTitle className="text-lg text-white">
                  {profilesByAgentId.get(matchup.awayAgentId)?.teamName ?? 'Away team'} bench
                </CardTitle>
              </CardHeader>
              <CardContent>
                {awayBench.length === 0 ? (
                  <p className="text-sm text-slate-400">No bench players.</p>
                ) : (
                  <ul className="divide-y divide-white/10">
                    {awayBench.map((entry) => (
                      <li key={entry.player?.sleeperPlayerId ?? entry.slotType} className="py-3 first:pt-0 last:pb-0">
                        <PlayerSlot entry={entry} week={matchup.week} opponentPoints={null} align="left" />
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          </section>
        </>
      )}
    </main>
  )
}

export default MatchupPage
