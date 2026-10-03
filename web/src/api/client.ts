/** Thin fetch wrapper: adds the token, turns problem documents (RFC 9457) into ApiError. */

const TOKEN_KEY = 'deskbook.token'

export class ApiError extends Error {
  readonly status: number
  /** Field name → message, from the "errors" member of the problem document. */
  readonly fieldErrors: Record<string, string>

  constructor(status: number, message: string, fieldErrors: Record<string, string> = {}) {
    super(message)
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

let onUnauthorized: () => void = () => {}

export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

export async function api<T>(path: string, options: { method?: string; body?: unknown } = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = tokenStore.get()
  if (token) headers.Authorization = `Bearer ${token}`
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  const response = await fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  if (response.status === 401 && token) {
    // The session expired or the user no longer exists.
    tokenStore.clear()
    onUnauthorized()
  }
  if (!response.ok) {
    throw await toApiError(response)
  }
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export async function toApiError(response: Response): Promise<ApiError> {
  let detail = `Request failed (${response.status})`
  let errors: Record<string, string> = {}
  try {
    const problem = await response.json()
    if (typeof problem.detail === 'string') detail = problem.detail
    if (problem.errors && typeof problem.errors === 'object') errors = problem.errors
  } catch {
    // not JSON: keep the generic message
  }
  return new ApiError(response.status, detail, errors)
}
