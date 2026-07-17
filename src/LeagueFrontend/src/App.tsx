import { Route, Routes } from 'react-router-dom'

import Layout from '@/components/Layout'
import AdminPage from '@/pages/AdminPage'
import AgentPage from '@/pages/AgentPage'
import HomePage from '@/pages/HomePage'
import MatchupPage from '@/pages/MatchupPage'
import PlayerDetailPage from '@/pages/PlayerDetailPage'
import PlayersPage from '@/pages/PlayersPage'
import WaiverWirePage from '@/pages/WaiverWirePage'

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<HomePage />} />
        <Route path="matchups/:matchupId" element={<MatchupPage />} />
        <Route path="admin" element={<AdminPage />} />
        <Route path="agents" element={<AgentPage />} />
        <Route path="players" element={<PlayersPage />} />
        <Route path="players/:sleeperId" element={<PlayerDetailPage />} />
        <Route path="waiver-wire" element={<WaiverWirePage />} />
      </Route>
    </Routes>
  )
}

export default App
