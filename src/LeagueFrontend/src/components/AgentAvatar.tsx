import { useEffect, useState } from 'react'
import { UserCircle2 } from 'lucide-react'

import { getAgentLogoUrls } from '@/lib/config'

function AgentAvatar({ agentId, sizeClassName = 'size-10', iconClassName = 'size-6' }: { agentId: string; sizeClassName?: string; iconClassName?: string }) {
  const logoUrls = getAgentLogoUrls(agentId)
  const [candidateIndex, setCandidateIndex] = useState(0)

  // Reset when the agent changes so a recycled component doesn't keep the previous agent's failures.
  useEffect(() => setCandidateIndex(0), [agentId])

  const logoUrl = logoUrls[candidateIndex]

  if (!logoUrl) {
    return (
      <div className={`flex ${sizeClassName} shrink-0 items-center justify-center rounded-full border border-white/10 bg-slate-950 text-slate-400`}>
        <UserCircle2 className={iconClassName} />
      </div>
    )
  }

  return (
    <img
      key={logoUrl}
      src={logoUrl}
      alt={`${agentId} logo`}
      onError={() => setCandidateIndex((index) => index + 1)}
      className={`${sizeClassName} shrink-0 rounded-full border border-white/10 bg-slate-950 object-cover`}
    />
  )
}

export default AgentAvatar
