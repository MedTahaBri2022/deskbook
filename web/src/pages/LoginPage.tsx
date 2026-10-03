import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export default function LoginPage() {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await login(email, password)
    } catch (e) {
      setError(e instanceof ApiError && e.status === 401 ? 'Wrong e-mail or password.' : 'The server cannot be reached.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-ink-900 px-4">
      <form onSubmit={submit} className="w-full max-w-sm space-y-4 rounded-xl bg-white p-6 shadow-xl">
        <div>
          <h1 className="text-2xl font-semibold text-ink-900">DeskBook</h1>
          <p className="text-sm text-slate-500">Meeting rooms, without double bookings</p>
        </div>
        <label className="block text-sm font-medium">
          E-mail
          <input type="email" required autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" />
        </label>
        <label className="block text-sm font-medium">
          Password
          <input type="password" required autoComplete="current-password" value={password}
            onChange={(e) => setPassword(e.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" />
        </label>
        {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
        <button disabled={busy} className="w-full rounded-md bg-ink-700 py-2 font-medium text-white hover:bg-ink-900 disabled:opacity-60">
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
