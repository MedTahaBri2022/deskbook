import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { RoomDay } from '../api/types'
import { addMinutes, formatTime, minutesBetween, offsetOf, slots } from '../time'

interface Props {
  day: RoomDay
  /** Proposed start, an instant on a slot boundary. */
  start: string
  onBook: (request: { roomId: number; title: string; attendees: number; startsAt: string; endsAt: string }) => Promise<unknown>
  onClose: () => void
}

/** Books one room on one day. Proposes one hour, within the free interval clicked. */
export default function BookingPanel({ day, start, onBook, onClose }: Props) {
  // Compared as instants: the API and the browser do not write ISO dates the same way.
  const at = (iso: string) => new Date(iso).getTime()
  const free = day.free.find((slot) => at(slot.from) <= at(start) && at(start) < at(slot.to))
  const latestEnd = new Date(free?.to ?? day.closes).toISOString()
  const [startsAt, setStartsAt] = useState(start)
  const [endsAt, setEndsAt] = useState(() => {
    const oneHour = addMinutes(start, 60)
    return new Date(oneHour) <= new Date(latestEnd) ? oneHour : latestEnd
  })
  const [title, setTitle] = useState('')
  const [attendees, setAttendees] = useState(Math.min(2, day.room.capacity))
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const offset = offsetOf(day.opens) // the day's office offset, for the times built here
  const startOptions = slots(day.opens, day.closes).slice(0, -1)
  const endOptions = slots(day.opens, day.closes).filter((t) => new Date(t) > new Date(startsAt))
  const problem = check(title, attendees, day.room.capacity, startsAt, endsAt)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (problem) return setError(problem)
    setBusy(true)
    setError(null)
    try {
      await onBook({ roomId: day.room.id, title: title.trim(), attendees, startsAt, endsAt })
    } catch (e) {
      if (e instanceof ApiError) {
        const fields = Object.values(e.fieldErrors)
        setError(fields.length ? `${e.message}: ${fields.join('; ')}` : e.message)
      } else {
        setError('Something went wrong; try again.')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} aria-label={`Book ${day.room.name}`}
      className="space-y-3 rounded-lg border border-ink-100 bg-white p-4 shadow-sm">
      <div className="flex items-start">
        <h3 className="font-semibold">Book {day.room.name}</h3>
        <button type="button" onClick={onClose} className="ml-auto text-sm text-slate-500 hover:underline">Close</button>
      </div>
      <div className="grid gap-3 sm:grid-cols-4">
        <label className="text-sm sm:col-span-2">Title
          <input value={title} maxLength={120} onChange={(e) => setTitle(e.target.value)} placeholder="Weekly sync"
            className="mt-1 w-full rounded border border-slate-300 px-2 py-1.5" />
        </label>
        <label className="text-sm">From
          <select value={startsAt} onChange={(e) => {
            setStartsAt(e.target.value)
            if (new Date(endsAt) <= new Date(e.target.value)) setEndsAt(addMinutes(e.target.value, 30))
          }} className="mt-1 w-full rounded border border-slate-300 px-2 py-1.5">
            {startOptions.map((t) => <option key={t} value={t}>{formatTime(t, offset)}</option>)}
          </select>
        </label>
        <label className="text-sm">To
          <select value={endsAt} onChange={(e) => setEndsAt(e.target.value)}
            className="mt-1 w-full rounded border border-slate-300 px-2 py-1.5">
            {endOptions.map((t) => <option key={t} value={t}>{formatTime(t, offset)}</option>)}
          </select>
        </label>
        <label className="text-sm">People
          <input type="number" min={1} max={day.room.capacity} value={attendees}
            onChange={(e) => setAttendees(e.target.valueAsNumber)} className="mt-1 w-full rounded border border-slate-300 px-2 py-1.5" />
        </label>
      </div>
      <p className="text-xs text-slate-500">
        {minutesBetween(startsAt, endsAt)} minutes · room for {day.room.capacity} people
      </p>
      {error && <p role="alert" className="rounded bg-rose-50 px-3 py-2 text-sm text-rose-800">{error}</p>}
      <button disabled={busy} className="rounded-md bg-ink-700 px-4 py-2 text-sm font-medium text-white hover:bg-ink-900 disabled:opacity-60">
        {busy ? 'Booking…' : 'Book'}
      </button>
    </form>
  )
}

/** What the user can fix before sending; the API checks everything again. */
export function check(title: string, attendees: number, capacity: number, startsAt: string, endsAt: string): string | null {
  if (!title.trim()) return 'Give the meeting a title.'
  if (!Number.isInteger(attendees) || attendees < 1) return 'At least one person.'
  if (attendees > capacity) return `This room holds ${capacity} people.`
  const minutes = minutesBetween(startsAt, endsAt)
  if (minutes <= 0) return 'The end must be after the start.'
  if (minutes > 8 * 60) return 'A booking lasts 8 hours at most.'
  return null
}
