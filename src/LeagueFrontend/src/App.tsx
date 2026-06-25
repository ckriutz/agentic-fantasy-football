import { Route, Routes } from 'react-router-dom'

import Layout from '@/components/Layout'
import AdminPage from '@/pages/AdminPage'
import HomePage from '@/pages/HomePage'
import PlayerDetailPage from '@/pages/PlayerDetailPage'
import PlayersPage from '@/pages/PlayersPage'

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<HomePage />} />
        <Route path="admin" element={<AdminPage />} />
        <Route path="players" element={<PlayersPage />} />
        <Route path="players/:sleeperId" element={<PlayerDetailPage />} />
      </Route>
    </Routes>
  )
}

export default App
