import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { api } from '../api/client'
import type { Booking, RoomDay } from '../api/types'
import BookingPanel from '../components/BookingPanel'
import { formatDay, formatTime, minutesBetween, nextWorkingDay, offsetOf, SLOT_MINUTES } from '../time'

/** Every room of a day on one timeline, from opening to closing time. */
export default function DayPage() {
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const date = params.get('date') ?? nextWorkingDay()
  const minCapacity = Number(params.get('people') ?? '1')
  const [selection, setSelection] = useState<{ roomId: number; start: string } | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const setParam = (key: string, value: string) =>
    setParams((current) => {
      const next = new URLSearchParams(current)
      next.set(key, value)
      return next
    })

  const { data, isPending, isError, error } = useQuery({
    queryKey: ['day', date, minCapacity],
    queryFn: () => api<RoomDay[]>(`/api/rooms/day?date=${date}&minCapacity=${minCapacity}`),
  })

  const book = useMutation({
    mutationFn: (request: object) => api<Booking>('/api/bookings', { method: 'POST', body: request }),
    onSuccess: (booking) => {
      setSelection(null)
      setNotice(`${booking.roomName} booked from ${formatTime(booking.startsAt)} to ${formatTime(booking.endsAt)}.`)
      queryClient.invalidateQueries({ queryKey: ['day'] })
      queryClient.invalidateQueries({ queryKey: ['mine'] })
    },
    // A 409 means someone was faster: show the fresh state of the day.
    onError: () => queryClient.invalidateQueries({ queryKey: ['day'] }),
  })

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="mr-auto">
          <h1 className="text-2xl font-semibold">Rooms</h1>
          {data?.[0] && <p className="text-sm text-slate-500">{formatDay(data[0].opens)} · office time (Casablanca, UTC{formatOffset(offsetOf(data[0].opens))})</p>}
        </div>
        <label className="text-sm">Day
          <input type="date" value={date} onChange={(e) => e.target.value && setParam('date', e.target.value)}
            className="ml-2 rounded border border-slate-300 px-2 py-1" />
        </label>
        <label className="text-sm">People
          <input type="number" min={1} value={minCapacity} onChange={(e) => setParam('people', String(Math.max(1, e.target.valueAsNumber || 1)))}
            className="ml-2 w-20 rounded border border-slate-300 px-2 py-1" />
        </label>
      </div>

      {notice && <p role="status" className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-800">{notice}</p>}
      {isError && <p role="alert" className="text-rose-700">{(error as Error).message}</p>}
      {isPending && <p className="text-slate-500">Loading…</p>}
      {data?.length === 0 && <p className="text-slate-500">No room for that many people.</p>}

      <div className="space-y-3">
        {data?.map((day) => (
          <div key={day.room.id} className="rounded-lg border border-ink-100 bg-white p-3">
            <div className="mb-2 flex flex-wrap items-baseline gap-2">
              <h2 className="font-semibold">{day.room.name}</h2>
              <span className="text-xs text-slate-500">
                floor {day.room.floor} · {day.room.capacity} people{day.room.equipment.length > 0 && ` · ${day.room.equipment.join(', ')}`}
              </span>
            </div>
            <Timeline day={day} onPick={(start) => { setNotice(null); setSelection({ roomId: day.room.id, start }) }} />
            {selection?.roomId === day.room.id && (
              <div className="mt-3">
                <BookingPanel key={selection.start} day={day} start={selection.start}
                  onBook={(request) => book.mutateAsync(request)} onClose={() => setSelection(null)} />
              </div>
            )}
          </div>
        ))}
      </div>
    </section>
  )
}

function Timeline({ day, onPick }: { day: RoomDay; onPick: (start: string) => void }) {
  const total = minutesBetween(day.opens, day.closes)
  const percent = (iso: string) => (minutesBetween(day.opens, iso) / total) * 100
  const hours = Array.from({ length: total / 60 + 1 }, (_, i) => i)

  return (
    <div>
      <div className="relative h-12 overflow-hidden rounded-md bg-ink-50">
        {day.free.map((slot) => (
          <button key={slot.from} type="button" title={`Free ${formatTime(slot.from)}–${formatTime(slot.to)}`}
            aria-label={`Book ${day.room.name} from ${formatTime(slot.from)}`}
            onClick={(e) => {
              // Start at the quarter of an hour under the click.
              const box = e.currentTarget.getBoundingClientRect()
              const offset = ((e.clientX - box.left) / box.width) * minutesBetween(slot.from, slot.to)
              const rounded = Math.floor(offset / SLOT_MINUTES) * SLOT_MINUTES
              onPick(new Date(new Date(slot.from).getTime() + rounded * 60000).toISOString())
            }}
            className="absolute inset-y-0 hover:bg-emerald-100"
            style={{ left: `${percent(slot.from)}%`, width: `${percent(slot.to) - percent(slot.from)}%` }} />
        ))}
        {day.bookings.map((b) => (
          <div key={b.id} title={`${b.title} · ${b.bookedBy}`}
            className="absolute inset-y-1 overflow-hidden rounded bg-ink-600 px-1.5 py-0.5 text-[11px] leading-tight text-white"
            style={{ left: `${percent(b.startsAt)}%`, width: `${percent(b.endsAt) - percent(b.startsAt)}%` }}>
            <div className="truncate font-medium">{b.title}</div>
            <div className="truncate opacity-80">{formatTime(b.startsAt)}–{formatTime(b.endsAt)} · {b.bookedBy}</div>
          </div>
        ))}
      </div>
      <div className="relative mt-1 h-4 text-[10px] text-slate-400">
        {hours.map((h) => (
          <span key={h} className="absolute -translate-x-1/2" style={{ left: `${(h * 60 / total) * 100}%` }}>
            {formatTime(new Date(new Date(day.opens).getTime() + h * 3600000).toISOString(), offsetOf(day.opens))}
          </span>
        ))}
      </div>
    </div>
  )
}

function formatOffset(minutes: number) {
  const sign = minutes < 0 ? '-' : '+'
  const abs = Math.abs(minutes)
  return `${sign}${String(Math.floor(abs / 60)).padStart(2, '0')}:${String(abs % 60).padStart(2, '0')}`
}
