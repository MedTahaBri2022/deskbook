export type Role = 'Employee' | 'Admin'
export type BookingStatus = 'Active' | 'Cancelled'

export interface Me {
  id: number
  fullName: string
  email: string
  role: Role
}

export interface Room {
  id: number
  name: string
  floor: number
  capacity: number
  equipment: string[]
  isActive: boolean
}

export interface Booking {
  id: number
  roomId: number
  roomName: string
  userId: number
  bookedBy: string
  title: string
  attendees: number
  startsAt: string
  endsAt: string
  status: BookingStatus
  canCancel: boolean
}

export interface FreeSlot {
  from: string
  to: string
}

export interface RoomDay {
  room: Room
  date: string
  opens: string
  closes: string
  bookings: Booking[]
  free: FreeSlot[]
}
