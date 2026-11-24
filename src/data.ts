import seed from '../shared/mangas.json'

// TypeScript: Bir manga kaydında hangi bilgilerin bulunacağını tanımlar.
export type Manga = {
  id: number
  title: string
  japaneseTitle: string
  author: string
  genres: string[]
  summary: string
  cover: string
  status: string
  rating: number
  ratingCount?: number
  chapters: number
  uploadedChapters?: number
  year: number
  views: string
  accent: string
}

export const demoMangas = seed as Manga[]
