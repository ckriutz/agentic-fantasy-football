import { useCallback, useEffect, useState } from 'react'
import { Loader2, Menu, Trophy, X } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'

import { cn } from '@/lib/utils'
import { apiBaseUrl } from '@/lib/config'

const navItems = [
  { label: 'Overview', to: '/' },
  { label: 'Playoffs', to: '/playoffs' },
  { label: 'Players', to: '/players' },
  { label: 'Waiver Wire', to: '/waiver-wire' },
  { label: 'Admin', to: '/admin' },
]

type ApiState = 'loading' | 'up' | 'down'

function ApiStatusButton() {
  const [apiState, setApiState] = useState<ApiState>('loading')

  const check = useCallback(async () => {
    setApiState('loading')
    try {
      const response = await fetch(`${apiBaseUrl}/`)
      setApiState(response.ok ? 'up' : 'down')
    } catch {
      setApiState('down')
    }
  }, [])

  useEffect(() => {
    void check()
  }, [check])

  return (
    <button
    type="button"
    onClick={() => void check()}
    className="inline-flex h-9 cursor-pointer items-center gap-2 rounded-md border border-white/10 bg-white/5 px-3 py-2 text-sm font-medium transition-colors hover:bg-white/10"
    aria-label="Check API status"
    >
    {apiState === 'loading' ? (
      <Loader2 className="size-3 animate-spin text-slate-400" />
    ) : (
      <span
        className={cn(
          'size-2 rounded-full',
          apiState === 'up' && 'bg-emerald-400',
          apiState === 'down' && 'bg-red-400',
        )}
      />
    )}
    <span
      className={cn(
        apiState === 'loading' && 'text-slate-400',
        apiState === 'up' && 'text-emerald-300',
        apiState === 'down' && 'text-red-400',
      )}
    >
      {apiState === 'loading' ? 'Checking…' : 'API'}
    </span>
    </button>
  )
}

function Layout() {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false)

  return (
    <div className="flex min-h-screen flex-col bg-slate-950 text-slate-50">
      <header className="border-b border-white/10 bg-slate-950/95">
        <div className="flex w-full items-center justify-between px-6 py-4 xl:px-10">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-full bg-emerald-400/15 text-emerald-300">
              <Trophy className="size-5" />
            </div>
            <div>
              <p className="text-sm font-semibold uppercase tracking-[0.2em] text-[#BF9264]">
                Agentic Fantasy Football
              </p>
              <h1 className="text-lg font-semibold text-white">Initial frontend scaffold</h1>
            </div>
          </div>
          <nav aria-label="Primary" className="hidden items-center gap-2 md:flex">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  cn(
                    'inline-flex h-9 items-center rounded-md px-4 py-2 text-sm font-medium transition-colors hover:bg-white/5 hover:text-white',
                    isActive ? 'text-white' : 'text-slate-400',
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
            <ApiStatusButton />
          </nav>
          <button
            type="button"
            onClick={() => setIsMobileMenuOpen((isOpen) => !isOpen)}
            className="inline-flex size-9 items-center justify-center rounded-md border border-white/10 bg-white/5 text-slate-300 transition-colors hover:bg-white/10 hover:text-white md:hidden"
            aria-expanded={isMobileMenuOpen}
            aria-controls="mobile-primary-navigation"
            aria-label={isMobileMenuOpen ? 'Close navigation menu' : 'Open navigation menu'}
          >
            {isMobileMenuOpen ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>
        </div>
        {isMobileMenuOpen && (
          <nav
            id="mobile-primary-navigation"
            aria-label="Mobile primary"
            className="border-t border-white/10 px-6 py-3 md:hidden"
          >
            <div className="flex flex-col gap-1">
              {navItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/'}
                  onClick={() => setIsMobileMenuOpen(false)}
                  className={({ isActive }) =>
                    cn(
                      'flex h-10 items-center rounded-md px-3 text-sm font-medium transition-colors hover:bg-white/5 hover:text-white',
                      isActive ? 'bg-white/5 text-white' : 'text-slate-400',
                    )
                  }
                >
                  {item.label}
                </NavLink>
              ))}
              <div className="mt-2 border-t border-white/10 pt-3">
                <ApiStatusButton />
              </div>
            </div>
          </nav>
        )}
      </header>

      <Outlet />
    </div>
  )
}

export default Layout
