/**
 * Times are shown in office time. The API sends every instant with the office
 * UTC offset (from the server's time zone database), so the client reads the
 * offset from the value instead of using the browser's own zone data, which
 * can be older than the server's. Values the client creates (slot boundaries)
 * are formatted with the offset of the day they belong to.
 */
export const SLOT_MINUTES = 15

/** UTC offset in minutes written at the end of an ISO date ("+01:00", "Z"). */
export function offsetOf(iso: string): number {
  const match = iso.match(/([+-])(\d{2}):?(\d{2})$/)
  if (!match) return 0
  const minutes = Number(match[2]) * 60 + Number(match[3])
  return match[1] === '-' ? -minutes : minutes
}

/** The instant as office wall-clock time, in a Date whose UTC fields hold that time. */
const wallClock = (iso: string, offset: number) => new Date(new Date(iso).getTime() + offset * 60000)

export const formatTime = (iso: string, offset = offsetOf(iso)) => wallClock(iso, offset).toISOString().slice(11, 16)

const dayFormat = new Intl.DateTimeFormat('en-GB', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC' })
export const formatDay = (iso: string, offset = offsetOf(iso)) => dayFormat.format(wallClock(iso, offset))

export const minutesBetween = (from: string, to: string) =>
  Math.round((new Date(to).getTime() - new Date(from).getTime()) / 60000)

export const addMinutes = (iso: string, minutes: number) =>
  new Date(new Date(iso).getTime() + minutes * 60000).toISOString()

/** Every slot boundary between two instants, both included. */
export function slots(from: string, to: string): string[] {
  const result: string[] = []
  for (let m = 0; m <= minutesBetween(from, to); m += SLOT_MINUTES) result.push(addMinutes(from, m))
  return result
}

/** The next working day after a date, as YYYY-MM-DD. */
export function nextWorkingDay(from = new Date()): string {
  const day = new Date(Date.UTC(from.getFullYear(), from.getMonth(), from.getDate() + 1))
  while (day.getUTCDay() === 0 || day.getUTCDay() === 6) day.setUTCDate(day.getUTCDate() + 1)
  return day.toISOString().slice(0, 10)
}
