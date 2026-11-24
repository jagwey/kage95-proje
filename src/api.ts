export type UserRole = 'user' | 'moderator' | 'admin'
export type ReadingStatus = 'reading' | 'completed' | 'plan_to_read' | 'dropped'

export type User = {
  id: number
  username: string
  firstName: string
  lastName: string
  email: string
  role: UserRole
  bio?: string | null
  avatarUrl?: string | null
}

export type ProfileSummary = { id: number; username: string; role: UserRole }
export type Friend = ProfileSummary & { status: 'pending' | 'accepted'; requestedBy: number }
export type BlockedUser = ProfileSummary

export type Comment = {
  id: number
  userId: number
  username: string
  role: UserRole
  body: string
  createdAt: string
}

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

export type ReadingListItem = {
  mangaId: number
  title: string
  cover: string
  totalChapters: number
  status: ReadingStatus
  currentChapter: number
  updatedAt: string
}

export type ChapterSummary = {
  id: number
  mangaId: number
  chapterNumber: number
  title: string
  pageCount: number
  createdAt: string
}

export type ChapterPage = { id: number; pageNumber: number; imageUrl: string }

export type ChapterDetail = {
  id: number
  mangaId: number
  mangaTitle: string
  chapterNumber: number
  title: string
  createdAt: string
  pages: ChapterPage[]
  previousChapterId: number | null
  nextChapterId: number | null
}

export type LeaderboardRow = {
  rank: number
  userId: number
  username: string
  role: UserRole
  avatarUrl: string | null
  points: number
  level: number
  title: string
}

export type LeaderboardMe = {
  rank: number | null
  points: number
  totalPoints: number
  level: number
  title: string
  levelRequired: number
  nextLevelRemaining: number
  nextTitle: string | null
  nextTitleRemaining: number
}

export type PointRule = { action: string; name: string; points: number; rule: string }

export type LeaderboardResponse = {
  period: 'tum' | 'ay' | 'hafta'
  updatedAt: string
  list: LeaderboardRow[]
  me: LeaderboardMe | null
  rules: PointRule[]
  dailyCap: number
}

export type PointHistoryItem = {
  id: number
  action: string
  label: string
  points: number
  note: string | null
  createdAt: string
}

export type ProfileStats = {
  favoritesCount: number
  readingCount: number
  completedCount: number
  ratingsCount: number
  commentsCount: number
  friendsCount: number
}

export type MyProfile = {
  id: number
  username: string
  firstName: string
  lastName: string
  email: string
  role: UserRole
  bio: string | null
  avatarUrl: string | null
  createdAt: string
  rank: LeaderboardMe
  stats: ProfileStats
  recentPoints: PointHistoryItem[]
}

export type AdminStats = {
  users: number
  mangas: number
  chapters: number
  comments: number
  ratings: number
  messages: number
}

export type AdminUser = {
  id: number
  username: string
  firstName: string
  lastName: string
  email: string
  role: UserRole
  totalPoints: number
  level: number
  title: string
  createdAt: string
}

export type ActionResult = { pointsEarned?: number }

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message) }
}

type PointsListener = (points: number) => void
const pointsListeners = new Set<PointsListener>()

export function subscribePoints(listener: PointsListener) {
  pointsListeners.add(listener)
  return () => { pointsListeners.delete(listener) }
}

export function notifyPoints(points?: number) {
  if (typeof points === 'number' && points > 0) {
    pointsListeners.forEach((fn) => fn(points))
  }
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
  const data = await response.json()
  if (data && typeof data === 'object' && 'pointsEarned' in data) {
    notifyPoints((data as ActionResult).pointsEarned)
  }
  return data as T
}

export async function uploadImage(path: string, file: File): Promise<string> {
  const form = new FormData()
  form.append('file', file)
  let response: Response
  try {
    response = await fetch('/api' + path, {
      method: 'POST',
      headers: { 'X-Kage95': '1' },
      body: form,
    })
  } catch {
    throw new ApiError(0, 'Görsel yüklenemedi. Bağlantını kontrol et.')
  }
  const json = response.headers.get('content-type')?.includes('application/json')
  if (!response.ok) {
    const payload = json ? await response.json() : null
    throw new ApiError(response.status, payload?.error || 'Görsel yüklenemedi.')
  }
  const data = (await response.json()) as { url: string }
  return data.url
}

export function mediaUrl(path: string | null | undefined): string {
  if (!path) return ''
  if (/^(https?:)?\/\//.test(path) || path.startsWith('/')) return path
  return `${import.meta.env.BASE_URL}${path}`
}

export function roleLabel(role: UserRole): string {
  if (role === 'admin') return 'ADMIN'
  if (role === 'moderator') return 'MOD'
  return ''
}

export function isStaff(user: User | null): boolean {
  return user?.role === 'admin' || user?.role === 'moderator'
}

export const readingStatusLabels: Record<ReadingStatus, string> = {
  reading: 'Okuyorum',
  completed: 'Tamamladım',
  plan_to_read: 'Planlıyorum',
  dropped: 'Bıraktım',
}

export function errorText(error: unknown) {
  return error instanceof Error ? error.message : 'Bir hata oluştu.'
}

const dateFormat = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' })
export const formatDate = (value: string) => dateFormat.format(new Date(value))
