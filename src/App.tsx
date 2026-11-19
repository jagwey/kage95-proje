import { useEffect, useMemo, useState } from 'react'
import { Heart, Search, Star } from 'lucide-react'
import { Account, Modal } from './Account'
import { Community } from './Community'
import { Discussion } from './Discussion'
import { api, errorText, type User } from './api'
import { demoMangas, type Manga } from './data'

type Page = 'all' | 'favorites' | 'community'
const demo = import.meta.env.MODE === 'pages'
const guestKey = 'kage95-favorites'
const coverUrl = (cover: string) => `${import.meta.env.BASE_URL}${cover}`

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
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [share, setShare] = useState<Manga | null>(null)
  const [mangas, setMangas] = useState<Manga[]>(demo ? demoMangas : [])
  const [loading, setLoading] = useState(!demo)
  const [catalogError, setCatalogError] = useState('')
  const [error, setError] = useState('')
  const [showAccount, setShowAccount] = useState(false)
  const [checkingSession, setCheckingSession] = useState(!demo)
  const [savingAccount, setSavingAccount] = useState(false)
  const [savingFavorite, setSavingFavorite] = useState(false)
  const [account, setAccount] = useState<{ user: User; favorites: number[] | null } | null>(null)
  const [guestFavorites, setGuestFavorites] = useState(readGuestFavorites)
  const user = account?.user ?? null
  const favorites = account ? account.favorites ?? [] : guestFavorites
  const selected = mangas.find((manga) => manga.id === selectedId)
  const genres = useMemo(() => ['Tümü', ...new Set(mangas.flatMap((manga) => manga.genres))], [mangas])

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
        const favorites = await fetchAccountFavorites()
        if (active) setAccount({ user, favorites })
      }
    }).catch((error) => { if (active) setError(errorText(error)) })
      .finally(() => { if (active) setCheckingSession(false) })
    return () => { active = false }
  }, [])

  async function refreshCatalog() {
    setCatalogError('')
    try { setMangas(await api<Manga[]>('/mangas')) } catch (error) {
      setCatalogError(errorText(error))
    } finally {
      setLoading(false)
    }
  }

  async function signedIn(user: User) {
    setShowAccount(false)
    setAccount({ user, favorites: null })
    setSavingAccount(true)
    setError('')
    try {
      const favorites = await fetchAccountFavorites()
      setAccount({ user, favorites })
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
  const favoriteDisabled = checkingSession || savingFavorite || savingAccount || account?.favorites === null

  return (
    <div className="site">
      <header className="header">
        <button className="logo" onClick={() => { setPage('all'); resetFilters() }}>KAGE<span>95</span></button>
        <nav aria-label="Ana menü">
          <button className={page === 'all' ? 'active' : ''} onClick={() => setPage('all')}>Mangalar</button>
          <button className={page === 'favorites' ? 'active' : ''} onClick={() => setPage('favorites')}>
            Favorilerim ({favorites.length})
          </button>
          {!demo && <button className={page === 'community' ? 'active' : ''} onClick={() => {
            setShare(null)
            setPage('community')
          }}>Arkadaşlar</button>}
        </nav>
        {page !== 'community' && <label className="search">
          <Search size={17} />
          <input aria-label="Manga ara" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Manga ara..." />
        </label>}
        {!demo && <div className="account-controls">
          {user
            ? <><span>@{user.username}</span><button className="secondary" disabled={savingAccount || savingFavorite}
                onClick={() => void logout()}>Çıkış</button></>
            : <button className="primary" disabled={checkingSession} onClick={() => setShowAccount(true)}>Giriş yap</button>}
        </div>}
      </header>

      <main>
        {error && <p role="alert" className="error">{error}</p>}
        {account?.favorites === null && !checkingSession && !savingAccount &&
          <button className="secondary" onClick={() => void reloadFavorites(account.user)}>Favorileri yeniden yükle</button>}
        {page === 'community' ? <Community key={user?.id ?? 'guest'} user={user} mangas={mangas} share={share}
          signIn={() => setShowAccount(true)} openManga={(id) => { setShare(null); setSelectedId(id) }} /> : <>
          <section className="intro">
            <p>KAGE95 MANGA</p>
            <h1>İyi hikâyeler,<br /><span>yeni dünyalar.</span></h1>
            <div className="intro-line">{demo ? 'Katalog önizlemesi · Favoriler bu tarayıcıda saklanır.' : 'Wind Breaker, Vinland Saga ve Re:ZERO'}</div>
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
                  return (
                    <article className="card" key={manga.id}>
                      <button className="cover" onClick={() => setSelectedId(manga.id)}>
                        <img src={coverUrl(manga.cover)} alt={`${manga.title} kapağı`} />
                      </button>
                      <div className="card-info">
                        <button className="title" onClick={() => setSelectedId(manga.id)}>{manga.title}</button>
                        <p>{manga.author}</p>
                        <div><span title={manga.ratingCount ? 'Topluluk puanı' : 'Katalog puanı'}>
                          <Star size={14} fill="currentColor" /> {manga.rating.toLocaleString('tr-TR')}
                        </span><small>{manga.status}</small></div>
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
        </>}
      </main>

      {selected && !showAccount && (
        <Modal label={selected.title} close={() => setSelectedId(null)} className="manga-dialog">
          <div className="manga-detail">
            <img src={coverUrl(selected.cover)} alt={`${selected.title} kapağı`} />
            <div>
              <p className="label">MANGA DETAYI</p>
              <h2>{selected.title}</h2>
              <p className="author">{selected.author} · {selected.year}</p>
              <p>{selected.summary}</p>
              <div className="tags">{selected.genres.map((item) => <span key={item}>{item}</span>)}</div>
              <div className="inline-form">
                <button className="modal-favorite" disabled={favoriteDisabled} onClick={() => void toggleFavorite(selected.id)}>
                  <Heart size={17} fill={favorites.includes(selected.id) ? 'currentColor' : 'none'} />
                  {favorites.includes(selected.id) ? 'Favorilerden çıkar' : 'Favoriye ekle'}
                </button>
                {!demo && <button className="secondary" onClick={() => {
                  if (!user) {
                    setShowAccount(true)
                    return
                  }
                  setShare(selected)
                  setSelectedId(null)
                  setPage('community')
                }}>Arkadaşına gönder</button>}
              </div>
              {error && <p role="alert" className="error">{error}</p>}
            </div>
          </div>
          {!demo && <Discussion key={`${selected.id}-${user?.id ?? 'guest'}`} mangaId={selected.id} user={user}
            signIn={() => setShowAccount(true)} ratingChanged={() => void refreshCatalog()} />}
        </Modal>
      )}
      {showAccount && <Account close={() => setShowAccount(false)} signedIn={signedIn} />}
    </div>
  )
}

export default App
