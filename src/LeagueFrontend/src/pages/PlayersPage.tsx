import { useCallback, useEffect, useMemo, useState } from 'react'
import { type ColumnDef } from '@tanstack/react-table'
import { AlertCircle, ChevronLeft, ChevronRight, Loader2, RefreshCw } from 'lucide-react'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { DataTable } from '@/components/ui/data-table'
import { apiBaseUrl } from '@/lib/config'

const PAGE_SIZE = 50

type FetchState = 'loading' | 'success' | 'error'

type Player = {
  sleeperPlayerId: string
  fullName: string
  team: string | null
  position: string | null
  searchRank: number | null
  injuryStatus: string | null
  status: string | null
  byeWeek: number | null
}

const playerColumns: ColumnDef<Player, unknown>[] = [
  {
    accessorKey: 'fullName',
    header: 'Name',
    cell: ({ row }) => (
      <Link
        to={`/players/${row.original.sleeperPlayerId}`}
        className="font-medium text-[#BF9264] hover:text-[#d4a97a] hover:underline"
      >
        {row.original.fullName}
      </Link>
    ),
  },
  { accessorKey: 'team', header: 'Team' },
  { accessorKey: 'position', header: 'Position' },
  { accessorKey: 'searchRank', header: 'Rank' },
  {
    id: 'status',
    header: 'Status',
    cell: ({ row }) => row.original.injuryStatus ?? row.original.status,
  },
  { accessorKey: 'byeWeek', header: 'Bye Week' },
]

function PlayersPage() {
  const [state, setState] = useState<FetchState>('loading')
  const [allPlayers, setAllPlayers] = useState<Player[]>([])
  const [page, setPage] = useState(0)

  const fetchPlayers = useCallback(async () => {
    setState('loading')
    setPage(0)
    try {
      const response = await fetch(`${apiBaseUrl}/api/players/?limit=1000`)
      if (!response.ok) {
        setState('error')
        return
      }
      const data = (await response.json()) as Player[]
      setAllPlayers(data)
      setState('success')
    } catch {
      setState('error')
    }
  }, [])

  useEffect(() => {
    void fetchPlayers()
  }, [fetchPlayers])

  const totalPages = Math.ceil(allPlayers.length / PAGE_SIZE)
  const pageData = useMemo(
    () => allPlayers.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE),
    [allPlayers, page],
  )

  return (
    <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
      <div className="flex items-center justify-between">
        <h2 className="text-3xl font-semibold tracking-tight text-white">Players</h2>
        <Button
          variant="ghost"
          size="icon"
          onClick={() => void fetchPlayers()}
          className="text-slate-300 hover:bg-white/5 hover:text-white"
          aria-label="Refresh players"
        >
          <RefreshCw className="size-4" />
        </Button>
      </div>

      {state === 'loading' && (
        <div className="flex items-center gap-2 text-slate-300">
          <Loader2 className="size-5 animate-spin" />
          <span className="text-sm">Loading players…</span>
        </div>
      )}
      {state === 'error' && (
        <div className="flex items-center gap-2 text-red-400">
          <AlertCircle className="size-5" />
          <span className="text-sm font-medium">Could not load players.</span>
        </div>
      )}
      {state === 'success' && (
        <>
          <DataTable columns={playerColumns} data={pageData} />
          <div className="flex items-center justify-between text-sm text-slate-400">
            <span>
              Showing {page * PAGE_SIZE + 1}–{Math.min((page + 1) * PAGE_SIZE, allPlayers.length)} of {allPlayers.length} players
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setPage((p) => p - 1)}
                disabled={page === 0}
                className="text-slate-300 hover:bg-white/5 hover:text-white disabled:opacity-40"
                aria-label="Previous page"
              >
                <ChevronLeft className="size-4" />
              </Button>
              <span className="text-slate-300">
                Page {page + 1} of {totalPages}
              </span>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setPage((p) => p + 1)}
                disabled={page >= totalPages - 1}
                className="text-slate-300 hover:bg-white/5 hover:text-white disabled:opacity-40"
                aria-label="Next page"
              >
                <ChevronRight className="size-4" />
              </Button>
            </div>
          </div>
        </>
      )}
    </main>
  )
}

export default PlayersPage
