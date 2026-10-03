import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api, setUnauthorizedHandler, tokenStore } from './client'

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

afterEach(() => {
  vi.restoreAllMocks()
  localStorage.clear()
})

describe('api client', () => {
  it('sends the stored token and the JSON body', async () => {
    tokenStore.set('abc')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(200, { ok: true }))

    await api('/api/claims', { method: 'POST', body: { title: 'Taxi' } })

    const [, init] = fetchMock.mock.calls[0]
    expect((init!.headers as Record<string, string>).Authorization).toBe('Bearer abc')
    expect(init!.body).toBe('{"title":"Taxi"}')
  })

  it('turns a problem document into an ApiError with field errors', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      json(422, { detail: 'Some items exceed the company limits', errors: { 'items[0].amount': 'too high' } }))

    const error = await api<never>('/api/claims').catch((e: ApiError) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(422)
    expect(error.message).toBe('Some items exceed the company limits')
    expect(error.fieldErrors).toEqual({ 'items[0].amount': 'too high' })
  })

  it('keeps a generic message when the error is not JSON', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('Bad gateway', { status: 502 }))

    const error = await api<never>('/api/claims').catch((e: ApiError) => e)

    expect(error.message).toBe('Request failed (502)')
  })

  it('drops the token and signs out on 401', async () => {
    tokenStore.set('expired')
    const signOut = vi.fn()
    setUnauthorizedHandler(signOut)
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(401, { detail: 'expired' }))

    await api('/api/me').catch(() => undefined)

    expect(tokenStore.get()).toBeNull()
    expect(signOut).toHaveBeenCalledOnce()
  })
})
