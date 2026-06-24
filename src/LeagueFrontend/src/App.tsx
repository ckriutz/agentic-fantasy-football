import { LayoutDashboard, Server, Trophy } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { apiBaseUrl } from '@/lib/config'

function App() {
  return (
    <div className="flex min-h-screen flex-col bg-slate-950 text-slate-50">
      <header className="border-b border-white/10 bg-slate-950/95">
        <div className="flex w-full items-center justify-between px-6 py-4 xl:px-10">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-full bg-emerald-400/15 text-emerald-300">
              <Trophy className="size-5" />
            </div>
            <div>
              <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-300">
                Agentic Fantasy Football
              </p>
              <h1 className="text-lg font-semibold text-white">Initial frontend scaffold</h1>
            </div>
          </div>
          <nav aria-label="Primary" className="hidden items-center gap-2 md:flex">
            <Button variant="ghost" className="text-slate-200 hover:bg-white/5 hover:text-white">
              Overview
            </Button>
            <Button variant="ghost" className="text-slate-400 hover:bg-white/5 hover:text-white">
              Rosters
            </Button>
            <Button variant="ghost" className="text-slate-400 hover:bg-white/5 hover:text-white">
              League State
            </Button>
          </nav>
        </div>
      </header>

      <main className="flex flex-1 flex-col gap-6 px-6 py-10 xl:px-10">
        <section className="grid flex-1 gap-6 lg:grid-cols-[1.4fr_0.9fr]">
          <Card className="border-white/10 bg-white/5 text-slate-50 shadow-2xl shadow-slate-950/40">
            <CardHeader className="space-y-4">
              <div className="inline-flex w-fit items-center gap-2 rounded-full border border-emerald-400/20 bg-emerald-400/10 px-3 py-1 text-sm text-emerald-200">
                <LayoutDashboard className="size-4" />
                Hello world, ready for future screens
              </div>
              <div className="space-y-3">
                <CardTitle className="text-4xl tracking-tight text-white">
                  Build from a clean starting point.
                </CardTitle>
                <CardDescription className="max-w-2xl text-base leading-7 text-slate-300">
                  This separate frontend project is ready for future league dashboards, roster pages,
                  and admin workflows without taking on feature scope yet.
                </CardDescription>
              </div>
            </CardHeader>
            <CardContent className="flex flex-col gap-4 md:flex-row">
              <Button className="bg-[#BF9264] text-slate-950 hover:bg-[#caa176]">
                Placeholder primary action
              </Button>
              <Button
                variant="outline"
                className="border-white/15 bg-transparent text-slate-100 hover:bg-white/5 hover:text-white"
              >
                Future navigation entry
              </Button>
            </CardContent>
          </Card>

          <Card className="border-white/10 bg-slate-900 text-slate-50">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-xl text-white">
                <Server className="size-5 text-emerald-300" />
                API connection placeholder
              </CardTitle>
              <CardDescription className="text-slate-300">
                The frontend reads its API base URL from environment-backed configuration.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="rounded-lg border border-white/10 bg-slate-950 px-4 py-3">
                <p className="text-xs font-medium uppercase tracking-[0.2em] text-slate-400">
                  Active API base URL
                </p>
                <p className="mt-2 break-all font-mono text-sm text-emerald-200">{apiBaseUrl}</p>
              </div>
              <p className="text-sm leading-6 text-slate-300">
                Use <code className="rounded bg-white/10 px-1.5 py-0.5 text-slate-100">VITE_API_BASE_URL</code> for
                local development or <code className="rounded bg-white/10 px-1.5 py-0.5 text-slate-100">API_BASE_URL</code>{' '}
                in the container runtime.
              </p>
            </CardContent>
          </Card>
        </section>

        <section className="grid gap-4 pb-2 md:grid-cols-3">
          {[
            {
              title: 'Separate frontend project',
              description: 'Vite, React, and TypeScript provide a fast UI foundation.',
            },
            {
              title: 'Reusable UI primitives',
              description: 'shadcn-style button and card components are ready for future pages.',
            },
            {
              title: 'Deployment ready',
              description: 'Docker and compose wiring keep the web app separate from LeagueAPI.',
            },
          ].map((item) => (
            <Card key={item.title} className="border-white/10 bg-white/5 text-slate-50">
              <CardHeader>
                <CardTitle className="text-lg text-white">{item.title}</CardTitle>
                <CardDescription className="text-slate-300">{item.description}</CardDescription>
              </CardHeader>
            </Card>
          ))}
        </section>
      </main>
    </div>
  )
}

export default App
