import { describe, expect, it } from 'vitest'
import { check } from './components/BookingPanel'
import { addMinutes, formatTime, minutesBetween, nextWorkingDay, offsetOf, slots } from './time'

describe('time helpers', () => {
  it('lists slot boundaries every 15 minutes', () => {
    expect(slots('2026-10-06T07:00:00Z', '2026-10-06T08:00:00Z')).toEqual([
      '2026-10-06T07:00:00.000Z', '2026-10-06T07:15:00.000Z', '2026-10-06T07:30:00.000Z',
      '2026-10-06T07:45:00.000Z', '2026-10-06T08:00:00.000Z',
    ])
  })

  it('works on instants, whatever offset the API used', () => {
    expect(minutesBetween('2026-10-06T08:00:00+01:00', '2026-10-06T07:30:00Z')).toBe(30)
    expect(addMinutes('2026-10-06T07:00:00Z', 90)).toBe('2026-10-06T08:30:00.000Z')
  })

  it('shows office time from the offset sent by the API, not the browser zone', () => {
    expect(offsetOf('2026-10-06T07:00:00+01:00')).toBe(60)
    expect(offsetOf('2026-10-06T07:00:00+00:00')).toBe(0)
    expect(offsetOf('2026-10-06T07:00:00Z')).toBe(0)
    expect(formatTime('2026-10-06T07:00:00+00:00')).toBe('07:00')
    expect(formatTime('2026-10-06T08:15:00+01:00')).toBe('08:15')
    // A time built by the client (UTC) shown with the day's offset.
    expect(formatTime('2026-10-06T07:15:00.000Z', 60)).toBe('08:15')
  })

  it('skips the weekend for the default day', () => {
    expect(nextWorkingDay(new Date(2026, 9, 9))).toBe('2026-10-12') // Friday → Monday
    expect(nextWorkingDay(new Date(2026, 9, 5))).toBe('2026-10-06') // Monday → Tuesday
  })
})

describe('booking checks before sending', () => {
  const start = '2026-10-06T09:00:00Z'
  it('accepts a regular meeting', () => {
    expect(check('Sync', 4, 6, start, addMinutes(start, 60))).toBeNull()
  })
  it('explains what to fix', () => {
    expect(check(' ', 4, 6, start, addMinutes(start, 60))).toMatch(/title/)
    expect(check('Sync', 7, 6, start, addMinutes(start, 60))).toBe('This room holds 6 people.')
    expect(check('Sync', 4, 6, start, start)).toMatch(/after the start/)
    expect(check('Sync', 4, 6, start, addMinutes(start, 8 * 60 + 15))).toMatch(/8 hours/)
  })
})
