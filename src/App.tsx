import { useEffect, useMemo, useState } from 'react'
import { Heart, Search, Star, X } from 'lucide-react'
import { genres, mangas, type Manga } from './data'

type Page = 'all' | 'favorites'

function App() {
  const [page, setPage] = useState<Page>('all')
  const [query, setQuery] = useState('')
  const [genre, setGenre] = useState('Tümü')
  const [selected, setSelected] = useState<Manga | null>(null)
  const [favorites, setFavorites] = useState<number[]>(() => {
    const saved = localStorage.getItem('kage95-favorites')
    return saved ? JSON.parse(saved) : []
  })

  useEffect(() => {
    localStorage.setItem('kage95-favorites', JSON.stringify(favorites))
  }, [favorites])

  const visibleMangas = useMemo(() => {
    const text = query.toLocaleLowerCase('tr-TR')
    return mangas.filter((manga) => {
      const matchesText = [manga.title, manga.author, ...manga.genres]
        .join(' ')
        .toLocaleLowerCase('tr-TR')
        .includes(text)
      const matchesGenre = genre === 'Tümü' || manga.genres.includes(genre)
      const matchesPage = page === 'all' || favorites.includes(manga.id)
      return matchesText && matchesGenre && matchesPage
    })
  }, [favorites, genre, page, query])

  function toggleFavorite(id: number) {
    setFavorites((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id],
    )
  }

  function resetFilters() {
    setQuery('')
    setGenre('Tümü')
  }

  return (
    <div className="site">
      <header className="header">
        <button className="logo" onClick={() => setPage('all')}>KAGE<span>95</span></button>
        <nav>
          <button className={page === 'all' ? 'active' : ''} onClick={() => setPage('all')}>Mangalar</button>
          <button className={page === 'favorites' ? 'active' : ''} onClick={() => setPage('favorites')}>
            Favorilerim ({favorites.length})
          </button>
        </nav>
        <label className="search">
          <Search size={17} />
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Manga ara..." />
        </label>
      </header>

      <main>
        <section className="intro">
          <p>KAGE95 MANGA</p>
          <h1>İyi hikâyeler,<br /><span>yeni dünyalar.</span></h1>
          <div className="intro-line">Wind Breaker, Vinland Saga ve Re:ZERO</div>
        </section>

        <section className="catalog">
          <div className="heading">
            <div>
              <p className="label">KÜTÜPHANE</p>
              <h2>{page === 'all' ? 'Mangalar' : 'Favorilerim'}</h2>
            </div>
            <span>{visibleMangas.length} manga</span>
          </div>

          <div className="filters">
            {genres.map((item) => (
              <button key={item} className={genre === item ? 'selected' : ''} onClick={() => setGenre(item)}>
                {item}
              </button>
            ))}
          </div>

          {visibleMangas.length > 0 ? (
            <div className="manga-list">
              {visibleMangas.map((manga) => {
                const isFavorite = favorites.includes(manga.id)
                return (
                  <article className="card" key={manga.id}>
                    <button className="cover" onClick={() => setSelected(manga)}>
                      <img src={manga.cover} alt={`${manga.title} kapağı`} />
                    </button>
                    <div className="card-info">
                      <button className="title" onClick={() => setSelected(manga)}>{manga.title}</button>
                      <p>{manga.author}</p>
                      <div><span><Star size={14} fill="currentColor" /> {manga.rating}</span><small>{manga.status}</small></div>
                      <button className={`favorite ${isFavorite ? 'saved' : ''}`} onClick={() => toggleFavorite(manga.id)} aria-label="Favoriye ekle">
                        <Heart size={17} fill={isFavorite ? 'currentColor' : 'none'} />
                      </button>
                    </div>
                  </article>
                )
              })}
            </div>
          ) : (
            <div className="empty">
              <h3>Manga bulunamadı</h3>
              <p>Arama veya filtre seçimini değiştir.</p>
              <button onClick={resetFilters}>Filtreleri temizle</button>
            </div>
          )}
        </section>
      </main>

      {selected && (
        <div className="modal" onClick={() => setSelected(null)}>
          <section className="modal-content" onClick={(event) => event.stopPropagation()}>
            <button className="close" onClick={() => setSelected(null)} aria-label="Kapat"><X size={20} /></button>
            <img src={selected.cover} alt={`${selected.title} kapağı`} />
            <div>
              <p className="label">MANGA DETAYI</p>
              <h2>{selected.title}</h2>
              <p className="author">{selected.author}</p>
              <p>{selected.summary}</p>
              <div className="tags">{selected.genres.map((item) => <span key={item}>{item}</span>)}</div>
              <button className="modal-favorite" onClick={() => toggleFavorite(selected.id)}>
                <Heart size={17} fill={favorites.includes(selected.id) ? 'currentColor' : 'none'} />
                {favorites.includes(selected.id) ? 'Favorilerde' : 'Favoriye ekle'}
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

export default App
