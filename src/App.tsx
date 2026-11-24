import { useEffect, useMemo, useState } from 'react'
import { BookOpen, Heart, Search, Shield, Star, Trophy } from 'lucide-react'
import { Account, Modal } from './Account'
import { Admin } from './Admin'
import { Community } from './Community'
import { Discussion } from './Discussion'
import { Leaderboard } from './Leaderboard'
import { Profile } from './Profile'
import { Reader } from './Reader'
import {
  api,
  errorText,
  isStaff,
  mediaUrl,
  readingStatusLabels,
  roleLabel,
  subscribePoints,
  type ChapterSummary,
  type ReadingListItem,
  type ReadingStatus,
  type User,
} from './api'
import { demoMangas, type Manga } from './data'

type Page = 'all' | 'favorites' | 'reading' | 'leaderboard' | 'community' | 'profile' | 'admin'
const demo = import.meta.env.MODE === 'pages'
const guestKey = 'kage95-favorites'

function readGuestFavorites(): number[] {
  try {
    const saved = JSON.parse(localStorage.getItem(guestKey) ?? '[]')
    return Array.isArray(saved) ? [...new Set(saved.filter((id) => Number.isInteger(id) && id > 0 && id <= 4294967295))] : []
  } catch {
    return []
  }
}

function App() {
  const [page, setPage] = useState<Page>('all')
  const [query, setQuery] = useState('')
  const [genre, setGenre] = useState('Tümü')
  const [readingFilter, setReadingFilter] = useState<'all' | ReadingStatus>('all')
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [activeChapterId, setActiveChapterId] = useState<number | null>(null)
  const [share, setShare] = useState<Manga | null>(null)
  const [mangas, setMangas] = useState<Manga[]>(demo ? demoMangas : [])
  const [readingList, setReadingList] = useState<ReadingListItem[]>([])
  const [selectedChapters, setSelectedChapters] = useState<ChapterSummary[]>([])
  const [loadingChapters, setLoadingChapters] = useState(false)
  const [readingDraftStatus, setReadingDraftStatus] = useState<ReadingStatus>('reading')
  const [readingDraftChapter, setReadingDraftChapter] = useState('1')
  const [savingReading, setSavingReading] = useState(false)

  const [loading, setLoading] = useState(!demo)
  const [catalogError, setCatalogError] = useState('')
  const [error, setError] = useState('')
  const [toastPoints, setToastPoints] = useState<number | null>(null)
  const [showAccount, setShowAccount] = useState(false)
  const [checkingSession, setCheckingSession] = useState(!demo)
  const [savingAccount, setSavingAccount] = useState(false)
  const [savingFavorite, setSavingFavorite] = useState(false)
  const [account, setAccount] = useState<{ user: User; favorites: number[] | null } | null>(null)
  const [guestFavorites, setGuestFavorites] = useState(readGuestFavorites)

  const user = account?.user ?? null
  const favorites = account ? account.favorites ?? [] : guestFavorites
  const selected = mangas.find((manga) => manga.id === selectedId)
  const selectedReading = selected ? readingList.find((r) => r.mangaId === selected.id) : undefined
  const genres = useMemo(() => ['Tümü', ...new Set(mangas.flatMap((manga) => manga.genres))], [mangas])

  useEffect(() => {
    let timer: number | undefined
    return subscribePoints((pts) => {
      setToastPoints(pts)
      if (timer) window.clearTimeout(timer)
      timer = window.setTimeout(() => setToastPoints(null), 3200)
    })
  }, [])

  useEffect(() => {
    try { localStorage.setItem(guestKey, JSON.stringify(guestFavorites)) } catch { /* Storage may be disabled. */ }
  }, [guestFavorites])

  useEffect(() => {
    if (demo) return
    void refreshCatalog()
    let active = true
    api<{ user: User | null }>('/auth/me').then(async ({ user }) => {
      if (active && user) {
        setAccount({ user, favorites: null })
        const [favs, rlist] = await Promise.all([
          fetchAccountFavorites(),
          api<ReadingListItem[]>('/reading-list').catch(() => [] as ReadingListItem[]),
        ])
        if (active) {
          setAccount({ user, favorites: favs })
          setReadingList(rlist)
        }
      }
    }).catch((error) => { if (active) setError(errorText(error)) })
      .finally(() => { if (active) setCheckingSession(false) })
    return () => { active = false }
  }, [])

  useEffect(() => {
    if (!selected || demo) {
      setSelectedChapters([])
      return
    }
    const existing = readingList.find((r) => r.mangaId === selected.id)
    setReadingDraftStatus(existing?.status ?? 'reading')
    setReadingDraftChapter(String(existing?.currentChapter ?? 1))

    let active = true
    setLoadingChapters(true)
    api<ChapterSummary[]>(`/mangas/${selected.id}/chapters`)
      .then((list) => { if (active) setSelectedChapters(list) })
      .catch(() => { if (active) setSelectedChapters([]) })
      .finally(() => { if (active) setLoadingChapters(false) })
    return () => { active = false }
  }, [selected?.id])

  async function refreshCatalog() {
    setCatalogError('')
    try { setMangas(await api<Manga[]>('/mangas')) } catch (error) {
      setCatalogError(errorText(error))
    } finally {
      setLoading(false)
    }
  }

  async function refreshReadingList() {
    if (!user || demo) return
    try { setReadingList(await api<ReadingListItem[]>('/reading-list')) } catch { /* ignore */ }
  }

  async function signedIn(user: User) {
    setShowAccount(false)
    setAccount({ user, favorites: null })
    setSavingAccount(true)
    setError('')
    try {
      const [favorites, rlist] = await Promise.all([
        fetchAccountFavorites(),
        api<ReadingListItem[]>('/reading-list').catch(() => [] as ReadingListItem[]),
      ])
      setAccount({ user, favorites })
      setReadingList(rlist)
    } catch (error) {
      setError(errorText(error))
    } finally {
      setSavingAccount(false)
    }
  }

  async function fetchAccountFavorites() {
    if (!guestFavorites.length) return api<number[]>('/favorites')
    let favorites: number[] = []
    for (let offset = 0; offset < guestFavorites.length; offset += 300) {
      favorites = await api<number[]>('/favorites', 'PUT', { ids: guestFavorites.slice(offset, offset + 300) })
    }
    setGuestFavorites([])
    return favorites
  }

  async function reloadFavorites(user: User) {
    setError('')
    setSavingAccount(true)
    try {
      const favorites = await fetchAccountFavorites()
      setAccount((current) => current?.user.id === user.id ? { user, favorites } : current)
    } catch (error) {
      setError(errorText(error))
    } finally {
      setSavingAccount(false)
    }
  }

  async function logout() {
    setSavingAccount(true)
    setError('')
    try {
      await api('/auth/logout', 'POST')
      setAccount(null)
      setReadingList([])
      setShare(null)
      setPage('all')
    } catch (error) {
      setError(errorText(error))
    } finally {
      setSavingAccount(false)
    }
  }

  async function toggleFavorite(id: number) {
    const toggleLocal = (current: number[]) => current.includes(id)
      ? current.filter((item) => item !== id)
      : [...current, id]
    if (!user) {
      setGuestFavorites(toggleLocal)
      return
    }
    if (account?.favorites === null || savingFavorite || savingAccount) return
    setSavingFavorite(true)
    setError('')
    const saved = favorites.includes(id)
    try {
      await api(`/favorites/${id}`, saved ? 'DELETE' : 'PUT')
      setAccount((current) => current?.user.id === user.id && current.favorites !== null
        ? { user, favorites: toggleLocal(current.favorites) }
        : current)
    } catch (error) {
      setError(errorText(error))
    } finally {
      setSavingFavorite(false)
    }
  }

  async function saveReadingProgress(mangaId: number) {
    if (!user || savingReading) return
    setSavingReading(true)
    setError('')
    try {
      await api(`/reading-list/${mangaId}`, 'PUT', {
        status: readingDraftStatus,
        currentChapter: Math.max(0, Number(readingDraftChapter) || 0),
      })
      await refreshReadingList()
    } catch (err) {
      setError(errorText(err))
    } finally {
      setSavingReading(false)
    }
  }

  async function removeReadingProgress(mangaId: number) {
    if (!user || savingReading) return
    setSavingReading(true)
    setError('')
    try {
      await api(`/reading-list/${mangaId}`, 'DELETE')
      await refreshReadingList()
    } catch (err) {
      setError(errorText(err))
    } finally {
      setSavingReading(false)
    }
  }

  function resetFilters() {
    setQuery('')
    setGenre('Tümü')
  }

  const visibleMangas = mangas.filter((manga) => {
    const matchesText = [manga.title, manga.author, ...manga.genres].join(' ').toLocaleLowerCase('tr-TR')
      .includes(query.trim().toLocaleLowerCase('tr-TR'))
    return matchesText && (genre === 'Tümü' || manga.genres.includes(genre))
      && (page !== 'favorites' || favorites.includes(manga.id))
  })

  const filteredReading = readingFilter === 'all'
    ? readingList
    : readingList.filter((item) => item.status === readingFilter)

  const favoriteDisabled = checkingSession || savingFavorite || savingAccount || account?.favorites === null
  const showSearch = page === 'all' || page === 'favorites'
  const userBadge = user ? roleLabel(user.role) : ''

  return (
    <div className="site">
      <header className="header">
        <button className="logo" onClick={() => { setPage('all'); resetFilters() }}>KAGE<span>95</span></button>
        <nav aria-label="Ana menü">
          <button className={page === 'all' ? 'active' : ''} onClick={() => setPage('all')}>Mangalar</button>
          <button className={page === 'favorites' ? 'active' : ''} onClick={() => setPage('favorites')}>
            Favorilerim ({favorites.length})
          </button>
          {!demo && <button className={page === 'reading' ? 'active' : ''} onClick={() => setPage('reading')}>
            Listem ({readingList.length})
          </button>}
          {!demo && <button className={page === 'leaderboard' ? 'active' : ''} onClick={() => setPage('leaderboard')}>
            <Trophy size={14} /> Liderlik
          </button>}
          {!demo && <button className={page === 'community' ? 'active' : ''} onClick={() => {
            setShare(null)
            setPage('community')
          }}>Arkadaşlar</button>}
          {!demo && isStaff(user) && (
            <button className={page === 'admin' ? 'active' : ''} onClick={() => setPage('admin')}>
              <Shield size={14} /> {user?.role === 'admin' ? 'Admin' : 'Mod'}
            </button>
          )}
        </nav>
        {showSearch && <label className="search">
          <Search size={17} />
          <input aria-label="Manga ara" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Manga ara..." />
        </label>}
        {!demo && <div className="account-controls">
          {user
            ? <>
              <button
                className={`user-pill ${page === 'profile' ? 'active' : ''}`}
                onClick={() => setPage('profile')}
                title="Hesabım sayfasına git">
                <span>@{user.username}</span>
                {userBadge && <span className={`role-badge role-${user.role}`}>{userBadge}</span>}
              </button>
              <button className="secondary" disabled={savingAccount || savingFavorite}
                onClick={() => void logout()}>Çıkış</button>
            </>
            : <button className="primary" disabled={checkingSession} onClick={() => setShowAccount(true)}>Giriş yap</button>}
        </div>}
      </header>

      <main>
        {error && <p role="alert" className="error">{error}</p>}
        {account?.favorites === null && !checkingSession && !savingAccount &&
          <button className="secondary" onClick={() => void reloadFavorites(account.user)}>Favorileri yeniden yükle</button>}

        {page === 'leaderboard' ? (
          <Leaderboard user={user} signIn={() => setShowAccount(true)} />
        ) : page === 'profile' ? (
          user ? (
            <Profile
              user={user}
              onUserUpdated={(updated) => setAccount((cur) => cur ? { ...cur, user: updated } : cur)}
              onNavigate={(target) => setPage(target)}
            />
          ) : (
            <section className="community-panel stack">
              <h2>Hesabım</h2>
              <p className="muted">Hesap bilgilerini ve puan geçmişini görmek için giriş yap.</p>
              <button className="primary align-start" onClick={() => setShowAccount(true)}>Giriş yap</button>
            </section>
          )
        ) : page === 'admin' ? (
          user && isStaff(user) ? (
            <Admin user={user} mangas={mangas} onCatalogChanged={refreshCatalog} />
          ) : (
            <section className="community-panel stack">
              <h2>Yetkisiz Erişim</h2>
              <p className="muted">Bu panel sadece moderatör ve admin hesaplarına açıktır.</p>
            </section>
          )
        ) : page === 'reading' ? (
          user ? (
            <section className="stack">
              <div className="heading">
                <div><p className="label">OKUMA TAKİBİ</p><h2>Okuma Listem</h2></div>
                <div className="filters">
                  <button className={readingFilter === 'all' ? 'selected' : ''} onClick={() => setReadingFilter('all')}>
                    Tümü ({readingList.length})
                  </button>
                  {(['reading', 'completed', 'plan_to_read', 'dropped'] as ReadingStatus[]).map((st) => (
                    <button key={st} className={readingFilter === st ? 'selected' : ''} onClick={() => setReadingFilter(st)}>
                      {readingStatusLabels[st]} ({readingList.filter((i) => i.status === st).length})
                    </button>
                  ))}
                </div>
              </div>
              {filteredReading.length === 0 ? (
                <div className="empty">
                  <h3>Bu listede henüz manga yok</h3>
                  <p>Bir manganın detayını açıp okuma durumunu ve kaldığın bölümü kaydedebilirsin.</p>
                  <button onClick={() => setPage('all')}>Mangaları keşfet</button>
                </div>
              ) : (
                <div className="reading-grid">
                  {filteredReading.map((item) => (
                    <article key={item.mangaId} className="community-panel reading-card">
                      <img src={mediaUrl(item.cover)} alt={item.title} onClick={() => setSelectedId(item.mangaId)} />
                      <div className="stack">
                        <button className="title align-start" onClick={() => setSelectedId(item.mangaId)}>{item.title}</button>
                        <div className="inline-form">
                          <span className="points-pill">{readingStatusLabels[item.status]}</span>
                          <small className="muted">Bölüm {item.currentChapter} / {item.totalChapters}</small>
                        </div>
                        <div className="inline-form">
                          <button className="secondary" onClick={() => setSelectedId(item.mangaId)}>Detay & Bölümler</button>
                          <button className="danger-btn" disabled={savingReading} onClick={() => void removeReadingProgress(item.mangaId)}>Çıkar</button>
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>
          ) : (
            <section className="community-panel stack">
              <h2>Okuma Listem</h2>
              <p className="muted">Okuma durumunu ve kaldığın bölümü takip etmek için giriş yap.</p>
              <button className="primary align-start" onClick={() => setShowAccount(true)}>Giriş yap</button>
            </section>
          )
        ) : page === 'community' ? (
          <Community key={user?.id ?? 'guest'} user={user} mangas={mangas} share={share}
            signIn={() => setShowAccount(true)} openManga={(id) => { setShare(null); setSelectedId(id) }} />
        ) : (
          <>
            <section className="intro">
              <p>KAGE95 MANGA</p>
              <h1>İyi hikâyeler,<br /><span>yeni dünyalar.</span></h1>
              <div className="intro-line">
                {demo
                  ? 'Katalog önizlemesi · Favoriler bu tarayıcıda saklanır.'
                  : `${mangas.map((m) => m.title).slice(0, 4).join(', ') || 'Manga kataloğu'} · Oku, puanla, liderlikte yüksel.`}
              </div>
            </section>

            <section className="catalog">
              <div className="heading">
                <div><p className="label">KÜTÜPHANE</p><h2>{page === 'all' ? 'Mangalar' : 'Favorilerim'}</h2></div>
                {!loading && !catalogError && <span>{visibleMangas.length} manga</span>}
              </div>

              <div className="filters">
                {genres.map((item) => <button key={item} className={genre === item ? 'selected' : ''}
                  onClick={() => setGenre(item)}>{item}</button>)}
              </div>

              {catalogError && <div className="empty">
                <p role="alert">{catalogError}</p>
                <button onClick={() => { setLoading(true); void refreshCatalog() }}>Tekrar dene</button>
              </div>}
              {loading && <p role="status" className="muted">Katalog yükleniyor…</p>}
              {!loading && !catalogError && (visibleMangas.length > 0 ? (
                <div className="manga-list">
                  {visibleMangas.map((manga) => {
                    const isFavorite = favorites.includes(manga.id)
                    const track = readingList.find((r) => r.mangaId === manga.id)
                    return (
                      <article className="card" key={manga.id}>
                        <button className="cover" onClick={() => setSelectedId(manga.id)}>
                          <img src={mediaUrl(manga.cover)} alt={`${manga.title} kapağı`} />
                        </button>
                        <div className="card-info">
                          <button className="title" onClick={() => setSelectedId(manga.id)}>{manga.title}</button>
                          <p>{manga.author}</p>
                          <div>
                            <span title={manga.ratingCount ? 'Topluluk puanı' : 'Katalog puanı'}>
                              <Star size={14} fill="currentColor" /> {manga.rating.toLocaleString('tr-TR')}
                            </span>
                            <small>{manga.status}</small>
                            {track && <small className="track-chip">{readingStatusLabels[track.status]} · B.{track.currentChapter}</small>}
                          </div>
                          <button className={`favorite ${isFavorite ? 'saved' : ''}`}
                            onClick={() => void toggleFavorite(manga.id)} disabled={favoriteDisabled}
                            aria-label={`${manga.title} · ${isFavorite ? 'Favorilerden çıkar' : 'Favoriye ekle'}`}
                            aria-pressed={isFavorite}>
                            <Heart size={17} fill={isFavorite ? 'currentColor' : 'none'} />
                          </button>
                        </div>
                      </article>
                    )
                  })}
                </div>
              ) : (
                <div className="empty">
                  <h3>{page === 'favorites' && !favorites.length ? 'Henüz favorin yok' : 'Manga bulunamadı'}</h3>
                  {page === 'favorites' && !favorites.length
                    ? <button onClick={() => { setPage('all'); resetFilters() }}>Mangaları keşfet</button>
                    : <><p>Arama veya filtre seçimini değiştir.</p><button onClick={resetFilters}>Filtreleri temizle</button></>}
                </div>
              ))}
            </section>
          </>
        )}
      </main>

      {selected && !showAccount && activeChapterId === null && (
        <Modal label={selected.title} close={() => setSelectedId(null)} className="manga-dialog">
          <div className="manga-detail">
            <img src={mediaUrl(selected.cover)} alt={`${selected.title} kapağı`} />
            <div>
              <p className="label">MANGA DETAYI</p>
              <h2>{selected.title}</h2>
              <p className="author">{selected.author} · {selected.year} · {selected.chapters} bölüm</p>
              <p>{selected.summary}</p>
              <div className="tags">{selected.genres.map((item) => <span key={item}>{item}</span>)}</div>
              <div className="inline-form">
                <button className="modal-favorite" disabled={favoriteDisabled} onClick={() => void toggleFavorite(selected.id)}>
                  <Heart size={17} fill={favorites.includes(selected.id) ? 'currentColor' : 'none'} />
                  {favorites.includes(selected.id) ? 'Favorilerden çıkar' : 'Favoriye ekle'}
                </button>
                {!demo && <button className="secondary" onClick={() => {
                  if (!user) { setShowAccount(true); return }
                  setShare(selected)
                  setSelectedId(null)
                  setPage('community')
                }}>Arkadaşına gönder</button>}
              </div>
              {error && <p role="alert" className="error">{error}</p>}
            </div>
          </div>

          {!demo && (
            <section className="discussion stack">
              <h3>Okuma Durumu & İlerleme</h3>
              {user ? (
                <div className="inline-form">
                  <select value={readingDraftStatus} onChange={(e) => setReadingDraftStatus(e.target.value as ReadingStatus)}>
                    {(['reading', 'completed', 'plan_to_read', 'dropped'] as ReadingStatus[]).map((st) => (
                      <option key={st} value={st}>{readingStatusLabels[st]}</option>
                    ))}
                  </select>
                  <label className="inline-form">
                    <span className="muted">Bölüm:</span>
                    <input
                      type="number"
                      min={0}
                      max={99999}
                      style={{ width: 82 }}
                      value={readingDraftChapter}
                      onChange={(e) => setReadingDraftChapter(e.target.value)}
                    />
                  </label>
                  <button className="primary" disabled={savingReading} onClick={() => void saveReadingProgress(selected.id)}>
                    {selectedReading ? 'Güncelle' : 'Listeye ekle'}
                  </button>
                  {selectedReading && (
                    <button className="danger-btn" disabled={savingReading} onClick={() => void removeReadingProgress(selected.id)}>
                      Listeden çıkar
                    </button>
                  )}
                </div>
              ) : (
                <button className="secondary align-start" onClick={() => setShowAccount(true)}>Okuma takibi için giriş yap</button>
              )}
            </section>
          )}

          {!demo && (
            <section className="discussion stack">
              <h3 className="inline-title"><BookOpen size={18} /> Bölümler ({selectedChapters.length})</h3>
              {loadingChapters && <p className="muted">Bölümler yükleniyor…</p>}
              {!loadingChapters && selectedChapters.length === 0 && (
                <p className="muted">Henüz yüklenmiş bölüm yok.</p>
              )}
              {!loadingChapters && selectedChapters.length > 0 && (
                <div className="chapters-list">
                  {selectedChapters.map((ch) => (
                    <button
                      key={ch.id}
                      className="chapter-row"
                      onClick={() => setActiveChapterId(ch.id)}>
                      <span><strong>Bölüm {ch.chapterNumber}</strong> · {ch.title}</span>
                      <small className="muted">{ch.pageCount} sayfa → Oku</small>
                    </button>
                  ))}
                </div>
              )}
            </section>
          )}

          {!demo && <Discussion key={`${selected.id}-${user?.id ?? 'guest'}`} mangaId={selected.id} user={user}
            signIn={() => setShowAccount(true)} ratingChanged={() => void refreshCatalog()} />}
        </Modal>
      )}

      {activeChapterId !== null && (
        <Reader
          chapterId={activeChapterId}
          user={user}
          onSelectChapter={(id) => setActiveChapterId(id)}
          onCompleted={() => void refreshReadingList()}
          onClose={() => setActiveChapterId(null)}
        />
      )}

      {showAccount && <Account close={() => setShowAccount(false)} signedIn={signedIn} />}

      {toastPoints !== null && (
        <div className="points-toast" role="status" aria-live="polite">
          <strong>+{toastPoints}</strong> puan kazandın!
        </div>
      )}
    </div>
  )
}

export default App
