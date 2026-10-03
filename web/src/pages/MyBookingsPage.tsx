import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../api/client'
import type { Booking } from '../api/types'
import { formatDay, formatTime } from '../time'

export default function MyBookingsPage() {
  const queryClient = useQueryClient()
  const [includePast, setIncludePast] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { data, isPending } = useQuery({
    queryKey: ['mine', includePast],
    queryFn: () => api<Booking[]>(`/api/bookings/mine?includePast=${includePast}`),
  })
  const cancel = useMutation({
    mutationFn: (id: number) => api<Booking>(`/api/bookings/${id}/cancel`, { method: 'POST' }),
    onSuccess: () => {
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['mine'] })
      queryClient.invalidateQueries({ queryKey: ['day'] })
    },
    onError: (e) => setError(e.message),
  })

  return (
    <section className="space-y-4">
      <div className="flex items-end">
        <h1 className="mr-auto text-2xl font-semibold">My bookings</h1>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={includePast} onChange={(e) => setIncludePast(e.target.checked)} /> Show past bookings
        </label>
      </div>
      {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
      {isPending && <p className="text-slate-500">Loading…</p>}
      {data?.length === 0 && <p className="rounded-lg border border-dashed border-slate-300 p-8 text-center text-slate-500">No booking.</p>}
      <ul className="space-y-2">
        {data?.map((b) => (
          <li key={b.id} className="flex flex-wrap items-center gap-3 rounded-lg border border-ink-100 bg-white px-4 py-3">
            <div className="mr-auto">
              <p className="font-medium">{b.title}</p>
              <p className="text-sm text-slate-500">
                {b.roomName} · {formatDay(b.startsAt)} · {formatTime(b.startsAt)}–{formatTime(b.endsAt)} · {b.attendees} people
              </p>
            </div>
            {b.canCancel && (
              <button onClick={() => cancel.mutate(b.id)} disabled={cancel.isPending}
                className="rounded-md border border-rose-300 px-3 py-1 text-sm text-rose-700 hover:bg-rose-50">
                Cancel
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  )
}
