import { useCallback, useEffect, useState } from 'react'
import { AlertCircle, ArrowLeft, Loader2 } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { apiBaseUrl } from '@/lib/config'

const CURRENT_SEASON = 2025

type FetchState = 'loading' | 'success' | 'error'

type PlayerData = {
  age: number | null
  birth_date: string | null
  college: string | null
  depth_chart_order: number | null
  depth_chart_position: string | null
  height: string | null
  weight: string | null
  number: number | null
  years_exp: number | null
  fantasy_positions: string[] | null
  high_school: string | null
  injury_body_part: string | null
  injury_notes: string | null
  injury_start_date: string | null
  injury_status: string | null
  practice_description: string | null
  practice_participation: string | null
}

type Player = {
  sleeperPlayerId: string
  yahooId: number | null
  fantasyDataId: number | null
  fullName: string
  firstName: string
  lastName: string
  team: string | null
  teamAbbr: string | null
  position: string | null
  searchRank: number | null
  injuryStatus: string | null
  injuryNotes: string | null
  status: string | null
  active: boolean
  averageDraftPosition: number | null
  byeWeek: number | null
  lastSeasonFantasyPoints: number | null
  projectedFantasyPoints: number | null
  auctionValue: number | null
  playerOwnedAverage: number | null
  rankAverage: string | null
  positionRank: string | null
  tier: number | null
  data: PlayerData | null
}

type Availability = {
  ownerAgentId: string | null
  isAvailable: boolean
  acquiredAtUtc: string | null
  slotType: string | null
  isStarter: boolean
  weeklyPoints: Record<string, number> | null
}

type WeekPoint = {
  week: number
  fantasyPoints: number
}

type SeasonPoints = {
  templateKey: string
  season: number
  gamesCount: number
  totalFantasyPoints: number
  averageFantasyPoints: number
  weeklyPoints: WeekPoint[] | null
}

function positionBadgeClass(position: string | null): string {
  switch (position?.toUpperCase()) {
    case 'QB': return 'bg-blue-500/20 text-blue-300 border border-blue-500/40'
    case 'RB': return 'bg-green-500/20 text-green-300 border border-green-500/40'
    case 'WR': return 'bg-amber-500/20 text-amber-300 border border-amber-500/40'
    case 'TE': return 'bg-orange-500/20 text-orange-300 border border-orange-500/40'
    case 'K':  return 'bg-purple-500/20 text-purple-300 border border-purple-500/40'
    default:   return 'bg-slate-500/20 text-slate-300 border border-slate-500/40'
  }
}

function injuryBadgeClass(status: string | null): string | null {
  if (!status) return null
  const s = status.toUpperCase()
  if (['OUT', 'IR', 'SUSPENDED', 'PUP'].includes(s)) return 'bg-red-500/20 text-red-300 border border-red-500/40'
  if (s === 'DOUBTFUL') return 'bg-orange-500/20 text-orange-300 border border-orange-500/40'
  if (s === 'QUESTIONABLE') return 'bg-yellow-500/20 text-yellow-300 border border-yellow-500/40'
  return 'bg-slate-500/20 text-slate-300 border border-slate-500/40'
}

function Badge({ children, className }: { children: React.ReactNode; className: string }) {
  return (
    <span className={`inline-flex items-center rounded-md px-2.5 py-0.5 text-xs font-semibold tracking-wide ${className}`}>
      {children}
    </span>
  )
}

function StatRow({ label, value }: { label: string; value: string | number | null | undefined }) {
  if (value === null || value === undefined || value === '') return null
  return (
    <div className="flex justify-between gap-4 py-2 border-b border-white/5 last:border-0">
      <span className="text-xs font-medium uppercase tracking-widest text-slate-400">{label}</span>
      <span className="text-sm text-slate-200 text-right">{String(value)}</span>
    </div>
  )
}

function WeekBar({ week, points, maxPoints }: { week: number; points: number; maxPoints: number }) {
  const pct = maxPoints > 0 ? Math.round((points / maxPoints) * 100) : 0
  return (
    <div className="flex items-center gap-2 py-1">
      <span className="w-10 text-right text-xs text-slate-400">Wk {week}</span>
      <div className="flex-1 h-2 rounded-full bg-white/5 overflow-hidden">
        <div className="h-full rounded-full bg-[#BF9264]/70" style={{ width: `${pct}%` }} />
      </div>
      <span className="w-12 text-right text-xs font-medium text-slate-200">{points.toFixed(1)}</span>
    </div>
  )
}

function PlayerDetailPage() {
  const { sleeperId } = useParams<{ sleeperId: string }>()
  const [state, setState] = useState<FetchState>('loading')
  const [player, setPlayer] = useState<Player | null>(null)
  const [availability, setAvailability] = useState<Availability | null>(null)
  const [seasonPoints, setSeasonPoints] = useState<SeasonPoints | null>(null)

  const fetchAll = useCallback(async () => {
    if (!sleeperId) return
    setState('loading')
    try {
      const [playerRes, availRes, pointsRes] = await Promise.all([
        fetch(`${apiBaseUrl}/api/players/${sleeperId}`),
        fetch(`${apiBaseUrl}/api/players/${sleeperId}/availability`),
        fetch(`${apiBaseUrl}/api/yahoo/points/player/${sleeperId}/${CURRENT_SEASON}`),
      ])

      if (!playerRes.ok) { setState('error'); return }
      setPlayer((await playerRes.json()) as Player)

      if (availRes.ok) setAvailability((await availRes.json()) as Availability)
      if (pointsRes.ok) setSeasonPoints((await pointsRes.json()) as SeasonPoints)

      setState('success')
    } catch {
      setState('error')
    }
  }, [sleeperId])

  useEffect(() => { void fetchAll() }, [fetchAll])

  const hasInjury = !!(
    player?.injuryStatus ??
    player?.injuryNotes ??
    player?.data?.injury_body_part ??
    player?.data?.practice_participation
  )

  const weeklyPoints = seasonPoints?.weeklyPoints ?? []
  const maxWeekPoints = weeklyPoints.length > 0
    ? Math.max(...weeklyPoints.map(w => w.fantasyPoints), 0)
    : 0

  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">

      {/* Header */}
      <div className="flex items-center gap-3 flex-wrap">
        <Link
          to="/players"
          aria-label="Back to players"
          className="inline-flex size-9 items-center justify-center rounded-md text-slate-300 hover:bg-white/5 hover:text-white transition-colors"
        >
          <ArrowLeft className="size-4" />
        </Link>
        <h2 className="text-3xl font-semibold tracking-tight text-white">
          {state === 'success' && player ? player.fullName : 'Player'}
        </h2>
        {player?.position && (
          <Badge className={positionBadgeClass(player.position)}>{player.position}</Badge>
        )}
        {player?.injuryStatus && (() => {
          const cls = injuryBadgeClass(player.injuryStatus)
          return cls ? <Badge className={cls}>{player.injuryStatus}</Badge> : null
        })()}
      </div>

      {state === 'loading' && (
        <div className="flex items-center gap-2 text-slate-300">
          <Loader2 className="size-5 animate-spin" />
          <span className="text-sm">Loading player…</span>
        </div>
      )}
      {state === 'error' && (
        <div className="flex items-center gap-2 text-red-400">
          <AlertCircle className="size-5" />
          <span className="text-sm font-medium">Could not load player.</span>
        </div>
      )}

      {state === 'success' && player && (
        <div className="flex flex-col gap-6">

          {/* Quick-stats hero strip */}
          <div className="flex flex-wrap gap-x-6 gap-y-2 rounded-xl border border-white/10 bg-slate-900/60 px-6 py-4 text-sm text-slate-300">
            {player.team && (
              <span>
                <span className="text-slate-500">Team</span>{' '}
                <span className="font-medium text-white">{player.team}</span>
                {player.data?.number != null && (
                  <span className="text-slate-400"> #{player.data.number}</span>
                )}
              </span>
            )}
            {player.byeWeek != null && (
              <span>
                <span className="text-slate-500">Bye</span>{' '}
                <span className="font-medium text-white">Week {player.byeWeek}</span>
              </span>
            )}
            {player.data?.depth_chart_order != null && (
              <span>
                <span className="text-slate-500">Depth</span>{' '}
                <span className="font-medium text-white">
                  #{player.data.depth_chart_order}{player.data.depth_chart_position ? ` ${player.data.depth_chart_position}` : ''}
                </span>
              </span>
            )}
            {player.data?.height && (
              <span>
                <span className="text-slate-500">Height</span>{' '}
                <span className="font-medium text-white">{player.data.height}"</span>
              </span>
            )}
            {player.data?.weight && (
              <span>
                <span className="text-slate-500">Weight</span>{' '}
                <span className="font-medium text-white">{player.data.weight} lbs</span>
              </span>
            )}
            {player.data?.age != null && (
              <span>
                <span className="text-slate-500">Age</span>{' '}
                <span className="font-medium text-white">{player.data.age}</span>
              </span>
            )}
            {player.data?.years_exp != null && (
              <span>
                <span className="text-slate-500">Exp</span>{' '}
                <span className="font-medium text-white">
                  {player.data.years_exp === 0 ? 'Rookie' : `${player.data.years_exp} yr`}
                </span>
              </span>
            )}
            {player.status && (
              <span>
                <span className="text-slate-500">Status</span>{' '}
                <span className={`font-medium ${player.active ? 'text-green-400' : 'text-red-400'}`}>{player.status}</span>
              </span>
            )}
          </div>

          {/* Injury alert — shown inline if relevant */}
          {hasInjury && (
            <div className="flex flex-col gap-1 rounded-xl border border-red-500/30 bg-red-500/10 px-5 py-4">
              <div className="flex items-center gap-2">
                <AlertCircle className="size-4 text-red-400 shrink-0" />
                <span className="text-sm font-semibold text-red-300">
                  {player.injuryStatus ?? 'Injury Report'}
                  {player.data?.injury_body_part ? ` — ${player.data.injury_body_part}` : ''}
                </span>
              </div>
              {(player.injuryNotes ?? player.data?.injury_notes) && (
                <p className="ml-6 text-xs text-red-200/80">
                  {player.injuryNotes ?? player.data?.injury_notes}
                </p>
              )}
              {player.data?.practice_participation && (
                <p className="ml-6 text-xs text-slate-400">
                  Practice: {player.data.practice_participation}
                  {player.data.practice_description ? ` — ${player.data.practice_description}` : ''}
                </p>
              )}
            </div>
          )}

          {/* Main cards */}
          <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">

            {/* Fantasy card */}
            <Card className="border-white/10 bg-slate-900 text-slate-50">
              <CardHeader>
                <CardTitle className="text-lg text-white">Fantasy</CardTitle>
              </CardHeader>
              <CardContent>
                {availability && (
                  <>
                    <StatRow
                      label="Fantasy Team"
                      value={availability.isAvailable ? 'Available (Free Agent)' : (availability.ownerAgentId ?? 'Unknown')}
                    />
                    {!availability.isAvailable && (
                      <>
                        <StatRow label="Slot" value={availability.slotType} />
                        <StatRow label="Role" value={availability.isStarter ? 'Starter' : 'Bench'} />
                        {availability.acquiredAtUtc && (
                          <StatRow
                            label="Acquired"
                            value={new Date(availability.acquiredAtUtc).toLocaleDateString()}
                          />
                        )}
                      </>
                    )}
                    <div className="my-2 border-t border-white/5" />
                  </>
                )}
                <StatRow label="ADP" value={player.averageDraftPosition} />
                <StatRow label="Auction Value" value={player.auctionValue != null ? `$${player.auctionValue}` : null} />
                <StatRow label="Projected Pts" value={player.projectedFantasyPoints} />
                <StatRow label="Last Season Pts" value={player.lastSeasonFantasyPoints} />
                <StatRow label="Search Rank" value={player.searchRank} />
                <div className="my-2 border-t border-white/5" />
                <StatRow label="FP Owned %" value={player.playerOwnedAverage} />
                <StatRow label="FP Rank Avg" value={player.rankAverage} />
                <StatRow label="FP Pos Rank" value={player.positionRank} />
                <StatRow label="FP Tier" value={player.tier} />
              </CardContent>
            </Card>

            {/* Yahoo points card */}
            <Card className="border-white/10 bg-slate-900 text-slate-50">
              <CardHeader>
                <CardTitle className="text-lg text-white">
                  {CURRENT_SEASON} Points
                  {seasonPoints && (
                    <span className="ml-2 text-xs font-normal text-slate-400">({seasonPoints.templateKey})</span>
                  )}
                </CardTitle>
              </CardHeader>
              <CardContent>
                {seasonPoints ? (
                  <>
                    <div className="mb-4 flex gap-6">
                      <div className="text-center">
                        <p className="text-2xl font-bold text-[#BF9264]">{seasonPoints.totalFantasyPoints.toFixed(1)}</p>
                        <p className="text-xs text-slate-400 uppercase tracking-widest mt-0.5">Total</p>
                      </div>
                      <div className="text-center">
                        <p className="text-2xl font-bold text-white">{seasonPoints.averageFantasyPoints.toFixed(1)}</p>
                        <p className="text-xs text-slate-400 uppercase tracking-widest mt-0.5">Avg/Wk</p>
                      </div>
                      <div className="text-center">
                        <p className="text-2xl font-bold text-white">{seasonPoints.gamesCount}</p>
                        <p className="text-xs text-slate-400 uppercase tracking-widest mt-0.5">Games</p>
                      </div>
                    </div>
                    {weeklyPoints.length > 0 ? (
                      <div className="border-t border-white/5 pt-3">
                        {weeklyPoints.map(w => (
                          <WeekBar key={w.week} week={w.week} points={w.fantasyPoints} maxPoints={maxWeekPoints} />
                        ))}
                      </div>
                    ) : (
                      <p className="border-t border-white/5 pt-3 text-sm text-slate-500 italic">
                        No weekly point breakdown available.
                      </p>
                    )}
                  </>
                ) : (
                  <p className="text-sm text-slate-500 italic">No {CURRENT_SEASON} stats available.</p>
                )}
              </CardContent>
            </Card>

            {/* Bio card */}
            <Card className="border-white/10 bg-slate-900 text-slate-50">
              <CardHeader>
                <CardTitle className="text-lg text-white">Bio</CardTitle>
              </CardHeader>
              <CardContent>
                <StatRow label="Birth Date" value={player.data?.birth_date} />
                <StatRow label="College" value={player.data?.college} />
                <StatRow label="High School" value={player.data?.high_school} />
                <StatRow label="Fantasy Positions" value={player.data?.fantasy_positions?.join(', ')} />
                <StatRow label="Sleeper ID" value={player.sleeperPlayerId} />
                <StatRow label="Yahoo ID" value={player.yahooId} />
              </CardContent>
            </Card>

          </div>
        </div>
      )}
    </main>
  )
}

export default PlayerDetailPage
