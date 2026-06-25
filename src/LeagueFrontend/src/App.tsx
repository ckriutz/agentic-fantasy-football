import { Route, Routes } from 'react-router-dom'

import Layout from '@/components/Layout'
import AdminPage from '@/pages/AdminPage'
import HomePage from '@/pages/HomePage'

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<HomePage />} />
        <Route path="admin" element={<AdminPage />} />
      </Route>
    </Routes>
  )
}

export default App
