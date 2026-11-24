import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { BookOpen, Plus, Shield, Trash2, Upload, Users } from 'lucide-react'
import {
  api,
  errorText,
  formatDate,
  roleLabel,
  uploadImage,
  type AdminStats,
  type AdminUser,
  type ChapterSummary,
  type User,
  type UserRole,
} from './api'
import type { Manga } from './data'

type AdminTab = 'mangas' | 'chapters' | 'users'

const emptyMangaForm = {
  title: '',
  japaneseTitle: '',
  author: '',
  genresText: 'Aksiyon, Dram',
  summary: '',
  cover: 'covers/wind-breaker.png',
  status: 'Devam Ediyor',
  rating: '9.0',
  chapters: '1',
  year: String(new Date().getFullYear()),
  views: '0',
  accent: '#68c58a',
}

export function Admin({
  user,
  mangas,
  onCatalogChanged,
}: {
  user: User
  mangas: Manga[]
  onCatalogChanged: () => Promise<void>
}) {
  const isFullAdmin = user.role === 'admin'
  const [tab, setTab] = useState<AdminTab>(isFullAdmin ? 'users' : 'mangas')
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [users, setUsers] = useState<AdminUser[]>([])
  const [roleDrafts, setRoleDrafts] = useState<Record<number, UserRole>>({})
  const [pointsDrafts, setPointsDrafts] = useState<Record<number, { amount: string; note: string }>>({})

  const [editingMangaId, setEditingMangaId] = useState<number | null>(null)
  const [mangaForm, setMangaForm] = useState(emptyMangaForm)

  const [chapterMangaId, setChapterMangaId] = useState<number>(mangas[0]?.id ?? 0)
  const [chapters, setChapters] = useState<ChapterSummary[]>([])
  const [chapterNum, setChapterNum] = useState('2')
  const [chapterTitle, setChapterTitle] = useState('')
  const [pageUrlsText, setPageUrlsText] = useState('')

  const [busy, setBusy] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  async function loadStats() {
    setStats(await api<AdminStats>('/admin/stats'))
  }

  async function loadUsers() {
    if (!isFullAdmin) return
    const list = await api<AdminUser[]>('/admin/users')
    setUsers(list)
    const drafts: Record<number, UserRole> = {}
    list.forEach((u) => { drafts[u.id] = u.role })
    setRoleDrafts(drafts)
  }

  async function loadChapters(mangaId: number) {
    if (!mangaId) { setChapters([]); return }
    setChapters(await api<ChapterSummary[]>(`/mangas/${mangaId}/chapters`))
  }

  useEffect(() => {
    void loadStats().catch((e) => setError(errorText(e)))
    if (isFullAdmin) void loadUsers().catch((e) => setError(errorText(e)))
  }, [isFullAdmin])

  useEffect(() => {
    if (!chapterMangaId && mangas.length > 0) setChapterMangaId(mangas[0].id)
  }, [mangas, chapterMangaId])

  useEffect(() => {
    if (chapterMangaId) void loadChapters(chapterMangaId).catch((e) => setError(errorText(e)))
  }, [chapterMangaId])

  async function run(action: () => Promise<void>, successText?: string) {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await action()
      if (successText) setMessage(successText)
      await loadStats()
    } catch (err) {
      setError(errorText(err))
    } finally {
      setBusy(false)
    }
  }

  function startEditManga(manga: Manga) {
    setEditingMangaId(manga.id)
    setMangaForm({
      title: manga.title,
      japaneseTitle: manga.japaneseTitle,
      author: manga.author,
      genresText: manga.genres.join(', '),
      summary: manga.summary,
      cover: manga.cover,
      status: manga.status,
      rating: String(manga.rating),
      chapters: String(manga.chapters),
      year: String(manga.year),
      views: manga.views,
      accent: manga.accent,
    })
  }

  function resetMangaForm() {
    setEditingMangaId(null)
    setMangaForm(emptyMangaForm)
  }

  async function handleSaveManga(event: FormEvent) {
    event.preventDefault()
    const payload = {
      title: mangaForm.title,
      japaneseTitle: mangaForm.japaneseTitle || mangaForm.title,
      author: mangaForm.author,
      genres: mangaForm.genresText.split(',').map((s) => s.trim()).filter(Boolean),
      summary: mangaForm.summary,
      cover: mangaForm.cover,
      status: mangaForm.status,
      rating: Number(mangaForm.rating) || 8.5,
      chapters: Number(mangaForm.chapters) || 1,
      year: Number(mangaForm.year) || new Date().getFullYear(),
      views: mangaForm.views || '0',
      accent: mangaForm.accent || '#68c58a',
    }
    await run(async () => {
      if (editingMangaId) {
        await api(`/admin/mangas/${editingMangaId}`, 'PUT', payload)
      } else {
        await api('/admin/mangas', 'POST', payload)
      }
      resetMangaForm()
      await onCatalogChanged()
    }, editingMangaId ? 'Manga güncellendi.' : 'Yeni manga eklendi.')
  }

  async function handleCoverUpload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) return
    setUploading(true)
    setError('')
    try {
      const url = await uploadImage('/admin/upload', file)
      setMangaForm((prev) => ({ ...prev, cover: url }))
      setMessage('Kapak görseli yüklendi.')
    } catch (err) {
      setError(errorText(err))
    } finally {
      setUploading(false)
      event.target.value = ''
    }
  }

  async function handlePageFilesUpload(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? [])
    if (files.length === 0) return
    setUploading(true)
    setError('')
    try {
      const urls: string[] = []
      for (const file of files) {
        urls.push(await uploadImage('/admin/upload', file))
      }
      setPageUrlsText((prev) => [prev.trim(), ...urls].filter(Boolean).join('\n'))
      setMessage(`${urls.length} sayfa görseli yüklendi.`)
    } catch (err) {
      setError(errorText(err))
    } finally {
      setUploading(false)
      event.target.value = ''
    }
  }

  async function handleCreateChapter(event: FormEvent) {
    event.preventDefault()
    if (!chapterMangaId) return
    const urls = pageUrlsText.split(/\r?\n/).map((u) => u.trim()).filter(Boolean)
    await run(async () => {
      await api(`/admin/mangas/${chapterMangaId}/chapters`, 'POST', {
        chapterNumber: Number(chapterNum),
        title: chapterTitle.trim() || `${chapterNum}. Bölüm`,
        pageUrls: urls,
      })
      setChapterTitle('')
      setPageUrlsText('')
      setChapterNum(String((Number(chapterNum) || 0) + 1))
      await loadChapters(chapterMangaId)
      await onCatalogChanged()
    }, 'Yeni bölüm eklendi.')
  }

  async function handleSaveRole(targetId: number) {
    const role = roleDrafts[targetId]
    if (!role) return
    await run(async () => {
      await api(`/admin/users/${targetId}/role`, 'PATCH', { role })
      await loadUsers()
    }, 'Kullanıcı rolü güncellendi.')
  }

  async function handleAdjustPoints(targetId: number) {
    const draft = pointsDrafts[targetId]
    const amount = Number(draft?.amount ?? 0)
    if (!amount) {
      setError('Sıfırdan farklı bir puan değeri gir (ör. 50 veya -20).')
      return
    }
    await run(async () => {
      await api(`/admin/users/${targetId}/points`, 'POST', {
        points: amount,
        note: draft?.note?.trim() || null,
      })
      setPointsDrafts((prev) => ({ ...prev, [targetId]: { amount: '', note: '' } }))
      await loadUsers()
    }, 'Kullanıcı puanı güncellendi.')
  }

  return (
    <section className="stack">
      <div className="heading">
        <div>
          <p className="label">{isFullAdmin ? 'TAM YETKİLİ YÖNETİM' : 'MODERATÖR PANELİ'}</p>
          <h2 className="inline-title"><Shield size={26} /> {isFullAdmin ? 'Admin Paneli' : 'Mod Paneli'}</h2>
        </div>
        <div className="filters">
          {isFullAdmin && (
            <button className={tab === 'users' ? 'selected' : ''} onClick={() => setTab('users')}>
              <Users size={14} /> Kullanıcılar & Yetkiler
            </button>
          )}
          <button className={tab === 'mangas' ? 'selected' : ''} onClick={() => setTab('mangas')}>
            <Plus size={14} /> Mangalar
          </button>
          <button className={tab === 'chapters' ? 'selected' : ''} onClick={() => setTab('chapters')}>
            <BookOpen size={14} /> Bölümler
          </button>
        </div>
      </div>

      {stats && (
        <div className="stats-grid">
          <div className="stat-box"><strong>{stats.users}</strong><span>Kullanıcı</span></div>
          <div className="stat-box"><strong>{stats.mangas}</strong><span>Manga</span></div>
          <div className="stat-box"><strong>{stats.chapters}</strong><span>Bölüm</span></div>
          <div className="stat-box"><strong>{stats.comments}</strong><span>Yorum</span></div>
          <div className="stat-box"><strong>{stats.ratings}</strong><span>Puan</span></div>
          <div className="stat-box"><strong>{stats.messages}</strong><span>Mesaj</span></div>
        </div>
      )}

      {error && <p role="alert" className="error">{error}</p>}
      {message && <p role="status" className="success-text">{message}</p>}

      {tab === 'users' && isFullAdmin && (
        <div className="community-panel stack">
          <div className="friend-row">
            <h3>Kullanıcılar, Mod/Admin Yetki Yönetimi & Puan</h3>
            <button className="secondary" disabled={busy} onClick={() => void run(loadUsers)}>Yenile</button>
          </div>
          <div className="admin-users-list">
            {users.map((u) => {
              const badge = roleLabel(u.role)
              const pDraft = pointsDrafts[u.id] ?? { amount: '', note: '' }
              return (
                <div key={u.id} className="admin-user-card">
                  <div className="friend-row">
                    <div>
                      <strong>@{u.username}</strong>{' '}
                      {badge && <span className={`role-badge role-${u.role}`}>{badge}</span>}
                      <span className="muted"> · {u.firstName} {u.lastName} ({u.email})</span>
                      <div>
                        <small className="muted">
                          {u.title} · Sv. {u.level} · {u.totalPoints} puan · Katılım: {formatDate(u.createdAt)}
                        </small>
                      </div>
                    </div>
                    {u.id !== user.id && (
                      <button
                        className="danger-btn"
                        disabled={busy}
                        onClick={() => {
                          if (window.confirm(`@${u.username} kullanıcısını kalıcı olarak silmek istediğine emin misin?`)) {
                            void run(async () => {
                              await api(`/admin/users/${u.id}`, 'DELETE')
                              await loadUsers()
                            }, `@${u.username} silindi.`)
                          }
                        }}>
                        <Trash2 size={14} /> Sil
                      </button>
                    )}
                  </div>

                  <div className="admin-user-actions">
                    <div className="inline-form">
                      <select
                        aria-label={`${u.username} rolü`}
                        value={roleDrafts[u.id] ?? u.role}
                        disabled={busy || u.id === user.id}
                        onChange={(e) => setRoleDrafts((prev) => ({ ...prev, [u.id]: e.target.value as UserRole }))}>
                        <option value="user">Üye (user)</option>
                        <option value="moderator">Moderatör (moderator)</option>
                        <option value="admin">Admin (tam yetki)</option>
                      </select>
                      <button
                        className="primary"
                        disabled={busy || u.id === user.id || (roleDrafts[u.id] ?? u.role) === u.role}
                        onClick={() => void handleSaveRole(u.id)}>
                        Rolü kaydet
                      </button>
                    </div>

                    <div className="inline-form">
                      <input
                        type="number"
                        style={{ width: 95 }}
                        placeholder="+50 / -20"
                        value={pDraft.amount}
                        onChange={(e) => setPointsDrafts((prev) => ({ ...prev, [u.id]: { ...pDraft, amount: e.target.value } }))}
                      />
                      <input
                        style={{ width: 170 }}
                        placeholder="Not (isteğe bağlı)"
                        value={pDraft.note}
                        onChange={(e) => setPointsDrafts((prev) => ({ ...prev, [u.id]: { ...pDraft, note: e.target.value } }))}
                      />
                      <button className="secondary" disabled={busy || !pDraft.amount} onClick={() => void handleAdjustPoints(u.id)}>
                        Puan uygula
                      </button>
                    </div>
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}

      {tab === 'mangas' && (
        <div className="leaderboard-grid">
          <form className="community-panel stack" onSubmit={handleSaveManga}>
            <div className="friend-row">
              <h3>{editingMangaId ? `Mangayı Düzenle (#${editingMangaId})` : 'Yeni Manga Ekle'}</h3>
              {editingMangaId && <button type="button" className="secondary" onClick={resetMangaForm}>İptal</button>}
            </div>
            <div className="form-grid">
              <label>Başlık<input value={mangaForm.title} onChange={(e) => setMangaForm({ ...mangaForm, title: e.target.value })} required /></label>
              <label>Japonca Başlık<input value={mangaForm.japaneseTitle} onChange={(e) => setMangaForm({ ...mangaForm, japaneseTitle: e.target.value })} /></label>
            </div>
            <div className="form-grid">
              <label>Yazar<input value={mangaForm.author} onChange={(e) => setMangaForm({ ...mangaForm, author: e.target.value })} required /></label>
              <label>Türler (virgülle)<input value={mangaForm.genresText} onChange={(e) => setMangaForm({ ...mangaForm, genresText: e.target.value })} required /></label>
            </div>
            <label>Özet<textarea rows={3} value={mangaForm.summary} onChange={(e) => setMangaForm({ ...mangaForm, summary: e.target.value })} required /></label>
            <div className="inline-form">
              <label style={{ flex: 1 }}>
                Kapak Yolu / URL
                <input value={mangaForm.cover} onChange={(e) => setMangaForm({ ...mangaForm, cover: e.target.value })} required />
              </label>
              <label className="secondary upload-btn" style={{ marginTop: 22 }}>
                <Upload size={14} /> {uploading ? 'Yükleniyor…' : 'Kapak yükle'}
                <input type="file" accept="image/*" hidden disabled={uploading} onChange={(e) => void handleCoverUpload(e)} />
              </label>
            </div>
            <div className="form-grid">
              <label>
                Durum
                <select value={mangaForm.status} onChange={(e) => setMangaForm({ ...mangaForm, status: e.target.value })}>
                  <option value="Devam Ediyor">Devam Ediyor</option>
                  <option value="Tamamlandı">Tamamlandı</option>
                </select>
              </label>
              <label>Başlangıç Puanı<input type="number" step="0.1" min="1" max="10" value={mangaForm.rating} onChange={(e) => setMangaForm({ ...mangaForm, rating: e.target.value })} /></label>
              <label>Toplam Bölüm<input type="number" min="0" value={mangaForm.chapters} onChange={(e) => setMangaForm({ ...mangaForm, chapters: e.target.value })} /></label>
              <label>Yıl<input type="number" min="1900" max="2100" value={mangaForm.year} onChange={(e) => setMangaForm({ ...mangaForm, year: e.target.value })} /></label>
            </div>
            <button className="primary align-start" disabled={busy}>{editingMangaId ? 'Güncelle' : 'Manga ekle'}</button>
          </form>

          <div className="community-panel stack">
            <h3>Mevcut Katalog ({mangas.length})</h3>
            {mangas.map((m) => (
              <div key={m.id} className="friend-row rule-item">
                <div>
                  <strong>{m.title}</strong>
                  <div><small className="muted">{m.author} · {m.status} · {m.uploadedChapters ?? 0} yüklü bölüm</small></div>
                </div>
                <div className="inline-form">
                  <button className="secondary" onClick={() => startEditManga(m)}>Düzenle</button>
                  <button
                    className="danger-btn"
                    disabled={busy}
                    onClick={() => {
                      if (window.confirm(`"${m.title}" mangasını silmek istediğine emin misin?`)) {
                        void run(async () => {
                          await api(`/admin/mangas/${m.id}`, 'DELETE')
                          if (editingMangaId === m.id) resetMangaForm()
                          await onCatalogChanged()
                        }, `"${m.title}" silindi.`)
                      }
                    }}>
                    <Trash2 size={14} /> Sil
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {tab === 'chapters' && (
        <div className="leaderboard-grid">
          <form className="community-panel stack" onSubmit={handleCreateChapter}>
            <h3>Mangaya Bölüm Ekle</h3>
            <label>
              Manga Seç
              <select value={chapterMangaId} onChange={(e) => setChapterMangaId(Number(e.target.value))}>
                {mangas.map((m) => <option key={m.id} value={m.id}>{m.title}</option>)}
              </select>
            </label>
            <div className="form-grid">
              <label>Bölüm No<input type="number" step="0.5" min="0.5" value={chapterNum} onChange={(e) => setChapterNum(e.target.value)} required /></label>
              <label>Bölüm Başlığı<input value={chapterTitle} onChange={(e) => setChapterTitle(e.target.value)} placeholder="Örn: Başlangıç" /></label>
            </div>
            <div className="friend-row">
              <span className="muted">Sayfa Görselleri (her satıra 1 URL veya dosya yükle)</span>
              <label className="secondary upload-btn">
                <Upload size={14} /> {uploading ? 'Yükleniyor…' : 'Sayfa görseli yükle'}
                <input type="file" accept="image/*" multiple hidden disabled={uploading} onChange={(e) => void handlePageFilesUpload(e)} />
              </label>
            </div>
            <textarea
              rows={5}
              value={pageUrlsText}
              onChange={(e) => setPageUrlsText(e.target.value)}
              placeholder={'/uploads/sayfa1.jpg\n/uploads/sayfa2.jpg'}
              required
            />
            <button className="primary align-start" disabled={busy || !chapterMangaId}>Bölümü yayınla</button>
          </form>

          <div className="community-panel stack">
            <h3>Yüklü Bölümler ({chapters.length})</h3>
            {chapters.length === 0 ? (
              <p className="muted">Bu manga için henüz bölüm yüklenmemiş.</p>
            ) : (
              chapters.map((ch) => (
                <div key={ch.id} className="friend-row rule-item">
                  <div>
                    <strong>Bölüm {ch.chapterNumber} · {ch.title}</strong>
                    <div><small className="muted">{ch.pageCount} sayfa · {formatDate(ch.createdAt)}</small></div>
                  </div>
                  <button
                    className="danger-btn"
                    disabled={busy}
                    onClick={() => void run(async () => {
                      await api(`/admin/chapters/${ch.id}`, 'DELETE')
                      await loadChapters(chapterMangaId)
                      await onCatalogChanged()
                    }, `Bölüm ${ch.chapterNumber} silindi.`)}>
                    <Trash2 size={14} /> Sil
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </section>
  )
}
