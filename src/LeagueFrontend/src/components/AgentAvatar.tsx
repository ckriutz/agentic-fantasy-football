import { useState } from 'react'
import { UserCircle2 } from 'lucide-react'

import { getAgentLogoUrl } from '@/lib/config'

function AgentAvatar({ agentId, sizeClassName = 'size-10', iconClassName = 'size-6' }: { agentId: string; sizeClassName?: string; iconClassName?: string }) {
  const logoUrl = getAgentLogoUrl(agentId)
  const [failed, setFailed] = useState(false)

  if (!logoUrl || failed) {
    return (
      <div className={`flex ${sizeClassName} shrink-0 items-center justify-center rounded-full border border-white/10 bg-slate-950 text-slate-400`}>
        <UserCircle2 className={iconClassName} />
      </div>
    )
  }

  return (
    <img
      src={logoUrl}
      alt={`${agentId} logo`}
      onError={() => setFailed(true)}
      className={`${sizeClassName} shrink-0 rounded-full border border-white/10 bg-slate-950 object-cover`}
    />
  )
}

export default AgentAvatar
