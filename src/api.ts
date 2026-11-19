export type User = { id: number; username: string; firstName: string; lastName: string; email: string }
export type Profile = Pick<User, 'id' | 'username'>
export type Friend = Profile & { status: 'pending' | 'accepted'; requestedBy: number }
export type Comment = { id: number; username: string; body: string; createdAt: string }
export type Discussion = {
  rating: { average: number | null; count: number; mine: number | null }
  comments: Comment[]
  hasMore: boolean
}
export type Message = {
  id: number
  senderId: number
  body: string | null
  mangaId: number | null
  mangaTitle: string | null
  createdAt: string
}
export type History = { messages: Message[]; hasMore: boolean }

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message) }
}

export async function api<T = void>(path: string, method = 'GET', body?: object): Promise<T> {
  let response: Response
  try {
    response = await fetch('/api' + path, {
      method,
      headers: {
        ...(method === 'GET' ? {} : { 'X-Kage95': '1' }),
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError(0, 'Sunucuya bağlanılamadı. Bağlantını kontrol edip tekrar dene.')
  }
  const json = response.headers.get('content-type')?.includes('application/json')
  if (!response.ok) {
    const payload = json ? await response.json() : null
    throw new ApiError(response.status, payload?.error || 'Sunucu işlemi tamamlayamadı.')
  }
  if (response.status === 204 || (response.status === 201 && !json)) return undefined as T
  if (!json) throw new ApiError(0, 'Sunucu beklenen yanıtı vermedi.')
  return response.json()
}

export function errorText(error: unknown) {
  return error instanceof Error ? error.message : 'Bir hata oluştu.'
}

const dateFormat = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' })
export const formatDate = (value: string) => dateFormat.format(new Date(value))
