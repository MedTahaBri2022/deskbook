import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { ApiError, api } from '../api/client'
import type { Room } from '../api/types'

export default function RoomsAdminPage() {
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['rooms', 'all'], queryFn: () => api<Room[]>('/api/rooms?includeInactive=true') })
  const [name, setName] = useState('')
  const [floor, setFloor] = useState(0)
  const [capacity, setCapacity] = useState(6)
  const [equipment, setEquipment] = useState('')
  const [error, setError] = useState<string | null>(null)

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['rooms'] })
    queryClient.invalidateQueries({ queryKey: ['day'] })
  }
  const save = useMutation({
    mutationFn: (room: Partial<Room> & { id?: number }) =>
      room.id ? api<Room>(`/api/rooms/${room.id}`, { method: 'PUT', body: room }) : api<Room>('/api/rooms', { method: 'POST', body: room }),
    onSuccess: () => { setError(null); refresh() },
    onError: (e) => setError(e instanceof ApiError && Object.keys(e.fieldErrors).length
      ? Object.values(e.fieldErrors).join('; ') : e.message),
  })

  function create(event: FormEvent) {
    event.preventDefault()
    save.mutate({ name, floor, capacity, isActive: true, equipment: equipment.split(',').map((e) => e.trim()).filter(Boolean) },
      { onSuccess: () => { setName(''); setEquipment('') } })
  }

  return (
    <section className="space-y-6">
      <h1 className="text-2xl font-semibold">Manage rooms</h1>
      <form onSubmit={create} className="flex flex-wrap items-end gap-3 rounded-lg border border-ink-100 bg-white p-4">
        <label className="text-sm">Name<input required value={name} maxLength={80} onChange={(e) => setName(e.target.value)} className="ml-2 rounded border px-2 py-1" /></label>
        <label className="text-sm">Floor<input type="number" value={floor} onChange={(e) => setFloor(e.target.valueAsNumber || 0)} className="ml-2 w-16 rounded border px-2 py-1" /></label>
        <label className="text-sm">Capacity<input type="number" min={1} max={500} value={capacity} onChange={(e) => setCapacity(e.target.valueAsNumber || 1)} className="ml-2 w-20 rounded border px-2 py-1" /></label>
        <label className="text-sm">Equipment<input value={equipment} placeholder="screen, whiteboard" onChange={(e) => setEquipment(e.target.value)} className="ml-2 rounded border px-2 py-1" /></label>
        <button className="rounded-md bg-ink-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-ink-900">Add room</button>
      </form>
      {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
      <table className="w-full overflow-hidden rounded-lg border border-ink-100 bg-white text-sm">
        <thead className="bg-ink-50 text-left text-slate-600">
          <tr><th className="px-4 py-2">Room</th><th className="px-4 py-2">Floor</th><th className="px-4 py-2">Capacity</th><th className="px-4 py-2">Equipment</th><th className="px-4 py-2" /></tr>
        </thead>
        <tbody>
          {data?.map((room) => (
            <tr key={room.id} className={`border-t border-ink-50 ${room.isActive ? '' : 'text-slate-400'}`}>
              <td className="px-4 py-2 font-medium">{room.name}</td>
              <td className="px-4 py-2">{room.floor}</td>
              <td className="px-4 py-2">{room.capacity}</td>
              <td className="px-4 py-2">{room.equipment.join(', ')}</td>
              <td className="px-4 py-2 text-right">
                <button onClick={() => save.mutate({ ...room, isActive: !room.isActive })} className="text-ink-700 hover:underline">
                  {room.isActive ? 'Deactivate' : 'Reactivate'}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
