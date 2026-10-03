import { Navigate, NavLink, Outlet, Route, Routes } from 'react-router'
import { useAuth } from './auth/AuthContext'
import DayPage from './pages/DayPage'
import LoginPage from './pages/LoginPage'
import MyBookingsPage from './pages/MyBookingsPage'
import RoomsAdminPage from './pages/RoomsAdminPage'

export default function App() {
  const { user, loading } = useAuth()
  if (loading) return <p className="p-8 text-slate-500">Loading…</p>
  if (!user) return <LoginPage />

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<DayPage />} />
        <Route path="/mine" element={<MyBookingsPage />} />
        {user.role === 'Admin' && <Route path="/rooms" element={<RoomsAdminPage />} />}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

function Layout() {
  const { user, logout } = useAuth()
  const link = ({ isActive }: { isActive: boolean }) =>
    `rounded-md px-3 py-2 text-sm font-medium ${isActive ? 'bg-white/15 text-white' : 'text-ink-100 hover:bg-white/10'}`
  return (
    <div className="min-h-screen">
      <header className="bg-ink-900 text-white">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-4 px-4 py-3">
          <span className="text-lg font-semibold">DeskBook</span>
          <nav className="flex gap-1">
            <NavLink to="/" end className={link}>Rooms</NavLink>
            <NavLink to="/mine" className={link}>My bookings</NavLink>
            {user?.role === 'Admin' && <NavLink to="/rooms" className={link}>Manage rooms</NavLink>}
          </nav>
          <div className="ml-auto flex items-center gap-3 text-sm">
            <span>{user?.fullName}{user?.role === 'Admin' && <span className="ml-2 rounded bg-sand-500 px-2 py-0.5 text-xs text-ink-900">Admin</span>}</span>
            <button onClick={logout} className="rounded-md border border-white/30 px-3 py-1 hover:bg-white/10">Sign out</button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
