import { useEffect, useState } from 'react'
import { CheckCircle, ChevronLeft, ChevronRight, X } from 'lucide-react'
import { api, errorText, mediaUrl, type ChapterDetail, type User } from './api'

export function Reader({
  chapterId,
  user,
  onSelectChapter,
  onCompleted,
  onClose,
}: {
  chapterId: number
  user: User | null
  onSelectChapter: (id: number) => void
  onCompleted: () => void
  onClose: () => void
}) {
  const [chapter, setChapter] = useState<ChapterDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [completing, setCompleting] = useState(false)
  const [completed, setCompleted] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    setLoading(true)
    setCompleted(false)
    setError('')
    api<ChapterDetail>(`/chapters/${chapterId}`)
      .then((res) => {
        if (active) {
          setChapter(res)
          window.scrollTo({ top: 0, behavior: 'smooth' })
        }
      })
      .catch((err) => { if (active) setError(errorText(err)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [chapterId])

  async function markComplete() {
    if (!chapter || !user || completing) return
    setCompleting(true)
    setError('')
    try {
      await api(`/chapters/${chapter.id}/complete`, 'POST')
      setCompleted(true)
      onCompleted()
    } catch (err) {
      setError(errorText(err))
    } finally {
      setCompleting(false)
    }
  }

  return (
    <div className="reader-overlay" role="dialog" aria-label="Manga okuyucu">
      <div className="reader-bar">
        <div>
          <p className="label">{chapter?.mangaTitle ?? 'MANGA OKUYUCU'}</p>
          <strong>{chapter ? `Bölüm ${chapter.chapterNumber} · ${chapter.title}` : 'Yükleniyor…'}</strong>
        </div>
        <div className="inline-form">
          {chapter?.previousChapterId && (
            <button className="secondary" onClick={() => onSelectChapter(chapter.previousChapterId!)}>
              <ChevronLeft size={16} /> Önceki
            </button>
          )}
          {chapter?.nextChapterId && (
            <button className="secondary" onClick={() => onSelectChapter(chapter.nextChapterId!)}>
              Sonraki <ChevronRight size={16} />
            </button>
          )}
          {user && chapter && (
            <button className="primary" disabled={completing || completed} onClick={() => void markComplete()}>
              <CheckCircle size={16} /> {completed ? 'Tamamlandı' : completing ? 'Kaydediliyor…' : 'Bölümü tamamla (+10 puan)'}
            </button>
          )}
          <button className="secondary" onClick={onClose} aria-label="Okuyucuyu kapat">
            <X size={16} /> Kapat
          </button>
        </div>
      </div>

      <div className="reader-body">
        {error && <p role="alert" className="error">{error}</p>}
        {loading && <p role="status" className="muted">Bölüm sayfaları yükleniyor…</p>}
        {!loading && chapter && chapter.pages.length === 0 && (
          <p className="muted">Bu bölümde henüz sayfa görseli yok.</p>
        )}
        {!loading && chapter && chapter.pages.map((page) => (
          <figure key={page.id} className="reader-page">
            <img src={mediaUrl(page.imageUrl)} alt={`${chapter.title} · Sayfa ${page.pageNumber}`} />
            <figcaption className="muted">Sayfa {page.pageNumber} / {chapter.pages.length}</figcaption>
          </figure>
        ))}

        {!loading && chapter && (
          <div className="reader-footer inline-form">
            {chapter.previousChapterId && (
              <button className="secondary" onClick={() => onSelectChapter(chapter.previousChapterId!)}>
                <ChevronLeft size={16} /> Önceki Bölüm
              </button>
            )}
            {user && (
              <button className="primary" disabled={completing || completed} onClick={() => void markComplete()}>
                <CheckCircle size={16} /> {completed ? 'Okundu olarak işaretlendi' : 'Bölümü Bitirdim (+10 puan)'}
              </button>
            )}
            {chapter.nextChapterId && (
              <button className="secondary" onClick={() => onSelectChapter(chapter.nextChapterId!)}>
                Sonraki Bölüm <ChevronRight size={16} />
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
