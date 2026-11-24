import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Ban, UserMinus } from 'lucide-react'
import {
  api,
  errorText,
  formatDate,
  roleLabel,
  type BlockedUser,
  type Friend,
  type History,
  type Message,
  type ProfileSummary,
  type User,
} from './api'
import type { Manga } from './data'

function Chat({ friend, user, mangas, preselected, openManga }: {
  friend: Friend; user: User; mangas: Manga[]; preselected: number | null; openManga: (id: number) => void
}) {
  const [messages, setMessages] = useState<Message[]>([])
  const [older, setOlder] = useState(false)
  const [body, setBody] = useState('')
  const [attachment, setAttachment] = useState(preselected?.toString() ?? '')
  const [loaded, setLoaded] = useState(false)
  const [loadingOlder, setLoadingOlder] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [historyError, setHistoryError] = useState('')
  const refresh = useRef<() => Promise<void>>(async () => {})
  const path = `/social/friends/${friend.id}/messages`

  useEffect(() => {
    if (preselected !== null) setAttachment(String(preselected))
  }, [preselected])

  useEffect(() => {
    let active = true
    let running = false
    let cursor: number | undefined
    async function poll() {
      if (running) return
      running = true
      try {
        const history = await api<History>(cursor ? `${path}?after=${cursor}` : path)
        if (!active) return
        if (!cursor) {
          setOlder(history.hasMore)
          setMessages(history.messages)
        } else if (history.messages.length) {
          setMessages((current) => [...current, ...history.messages])
        }
        cursor = history.messages.at(-1)?.id ?? cursor
        setLoaded(true)
        setHistoryError('')
      } catch (error) {
        if (active) setHistoryError(errorText(error))
      } finally {
        running = false
      }
    }
    refresh.current = poll
    void poll()
    const timer = window.setInterval(poll, 4000)
    return () => {
      active = false
      window.clearInterval(timer)
    }
  }, [path])

  async function loadOlder() {
    setLoadingOlder(true)
    setError('')
    try {
      const history = await api<History>(`${path}?before=${messages[0].id}`)
      setMessages((current) => [...history.messages, ...current])
      setOlder(history.hasMore)
    } catch (error) {
      setError(errorText(error))
    } finally {
      setLoadingOlder(false)
    }
  }

  async function send(event: FormEvent) {
    event.preventDefault()
    if (busy) return
    setBusy(true)
    setError('')
    try {
      await api(path, 'POST', { body, mangaId: attachment ? Number(attachment) : null })
      setBody('')
      setAttachment('')
      await refresh.current()
    } catch (error) {
      setError(errorText(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="community-panel stack">
      <h3>@{friend.username} ile sohbet</h3>
      {older && <button className="secondary align-start" disabled={loadingOlder}
        onClick={() => void loadOlder()}>Önceki mesajları yükle</button>}
      {!loaded && !historyError && <p role="status" className="muted">Sohbet yükleniyor…</p>}
      {loaded && messages.length === 0 && <p className="muted">Henüz mesaj yok.</p>}
      {messages.length > 0 && <ol className="messages" aria-label="Sohbet geçmişi">
        {messages.map((message) => <li key={message.id} className={message.senderId === user.id ? 'own-message' : ''}>
          <strong>@{message.senderId === user.id ? user.username : friend.username}</strong>
          <time dateTime={message.createdAt}>{formatDate(message.createdAt)}</time>
          {message.body && <p>{message.body}</p>}
          {message.mangaId !== null && <button className="secondary"
            onClick={() => { if (message.mangaId !== null) openManga(message.mangaId) }}>Manga · {message.mangaTitle}</button>}
        </li>)}
      </ol>}
      <form className="stack" onSubmit={send}>
        <label>Mesaj<textarea value={body} onChange={(event) => setBody(event.target.value)} maxLength={2000} rows={3} /></label>
        <label>Manga ekle<select value={attachment} onChange={(event) => setAttachment(event.target.value)}>
          <option value="">Manga seç</option>
          {mangas.map((manga) => <option key={manga.id} value={manga.id}>{manga.title}</option>)}
        </select></label>
        <button className="primary align-start" disabled={busy || (!body.trim() && !attachment)}>Gönder</button>
      </form>
      {historyError && <p role="alert" className="error">{historyError}</p>}
      {error && <p role="alert" className="error">{error}</p>}
    </section>
  )
}

export function Community({ user, mangas, share, signIn, openManga }: {
  user: User | null; mangas: Manga[]; share: Manga | null; signIn: () => void; openManga: (id: number) => void
}) {
  const [friends, setFriends] = useState<Friend[]>([])
  const [blocked, setBlocked] = useState<BlockedUser[]>([])
  const [results, setResults] = useState<ProfileSummary[] | null>(null)
  const [activeId, setActiveId] = useState<number | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const active = friends.find((friend) => friend.id === activeId && friend.status === 'accepted')

  async function loadAll() {
    const [friendList, blockList] = await Promise.all([
      api<Friend[]>('/social/friends'),
      api<BlockedUser[]>('/social/blocks'),
    ])
    setFriends(friendList)
    setBlocked(blockList)
  }

  useEffect(() => {
    if (!user) return
    let live = true
    Promise.all([api<Friend[]>('/social/friends'), api<BlockedUser[]>('/social/blocks')])
      .then(([f, b]) => { if (live) { setFriends(f); setBlocked(b) } })
      .catch((error) => { if (live) setError(errorText(error)) })
    return () => { live = false }
  }, [user])

  if (!user) return <section className="community-panel stack">
    <h2>Arkadaşlar ve sohbet</h2>
    <p className="muted">Arkadaş eklemek ve mesajlaşmak için giriş yap.</p>
    <button className="primary align-start" onClick={signIn}>Giriş yap</button>
  </section>

  async function act(action: () => Promise<void>) {
    if (busy) return
    setBusy(true)
    setError('')
    try { await action() } catch (error) { setError(errorText(error)) } finally { setBusy(false) }
  }

  function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const username = new FormData(event.currentTarget).get('username')?.toString().trim() ?? ''
    void act(async () => setResults(await api<ProfileSummary[]>(`/social/users?username=${encodeURIComponent(username)}`)))
  }

  function friendAction(id: number, method: 'POST' | 'PATCH' | 'DELETE') {
    void act(async () => {
      await api(`/social/friends/${id}`, method)
      if (method === 'DELETE' && activeId === id) setActiveId(null)
      await loadAll()
    })
  }

  function blockAction(id: number, method: 'POST' | 'DELETE') {
    void act(async () => {
      await api(`/social/blocks/${id}`, method)
      if (method === 'POST' && activeId === id) setActiveId(null)
      setResults((prev) => prev ? prev.filter((p) => p.id !== id) : prev)
      await loadAll()
    })
  }

  return (
    <section className="community stack">
      <h2>Arkadaşlar ve sohbet</h2>
      <div className="community-layout">
        <section className="community-panel stack">
          <h3>Arkadaş bul</h3>
          <form className="inline-form" onSubmit={search}>
            <input name="username" aria-label="Kullanıcı adı" placeholder="Kullanıcı adı" minLength={3} maxLength={24} required />
            <button className="primary" disabled={busy}>Ara</button>
          </form>
          {results?.length === 0 && <p className="muted">Kullanıcı bulunamadı.</p>}
          {results?.map((person) => {
            const badge = roleLabel(person.role)
            return (
              <div className="friend-row" key={person.id}>
                <div>
                  <strong>@{person.username}</strong>
                  {badge && <span className={`role-badge role-${person.role}`}>{badge}</span>}
                </div>
                <div className="inline-form">
                  {friends.some((friend) => friend.id === person.id)
                    ? <span className="muted">Listende</span>
                    : <button className="secondary" disabled={busy} onClick={() => friendAction(person.id, 'POST')}>İstek gönder</button>}
                  <button className="danger-btn" disabled={busy} onClick={() => blockAction(person.id, 'POST')} title="Engelle">
                    <Ban size={13} />
                  </button>
                </div>
              </div>
            )
          })}

          <div className="friend-row">
            <h3>Arkadaşların ve istekler</h3>
            <button className="secondary" disabled={busy} onClick={() => void act(loadAll)}>Yenile</button>
          </div>
          {friends.length === 0 && <p className="muted">Henüz arkadaş veya istek yok.</p>}
          {friends.map((friend) => {
            const badge = roleLabel(friend.role)
            return (
              <div key={friend.id} className="friend-row">
                <div>
                  <strong>@{friend.username}</strong>
                  {badge && <span className={`role-badge role-${friend.role}`}>{badge}</span>}
                </div>
                {friend.status === 'accepted'
                  ? <div className="inline-form">
                    <button className="secondary" aria-pressed={activeId === friend.id}
                      onClick={() => setActiveId(friend.id)}>Sohbet</button>
                    <button className="secondary" disabled={busy} title="Arkadaşlıktan çıkar"
                      onClick={() => {
                        if (window.confirm(`@${friend.username} kişisini arkadaşlıktan çıkarmak istiyor musun?`))
                          friendAction(friend.id, 'DELETE')
                      }}><UserMinus size={14} /></button>
                    <button className="danger-btn" disabled={busy} title="Engelle"
                      onClick={() => {
                        if (window.confirm(`@${friend.username} kişisini engellemek istiyor musun?`))
                          blockAction(friend.id, 'POST')
                      }}><Ban size={14} /></button>
                  </div>
                  : friend.requestedBy === user.id
                    ? <button className="secondary" disabled={busy} onClick={() => friendAction(friend.id, 'DELETE')}>İsteği iptal et</button>
                    : <div className="inline-form">
                      <button className="primary" disabled={busy} onClick={() => friendAction(friend.id, 'PATCH')}>Kabul et</button>
                      <button className="secondary" disabled={busy} onClick={() => friendAction(friend.id, 'DELETE')}>Reddet</button>
                      <button className="danger-btn" disabled={busy} title="Engelle" onClick={() => blockAction(friend.id, 'POST')}><Ban size={13} /></button>
                    </div>}
              </div>
            )
          })}

          {blocked.length > 0 && (
            <div className="blocked-section stack">
              <h3>Engellenenler ({blocked.length})</h3>
              {blocked.map((person) => (
                <div key={person.id} className="friend-row">
                  <span className="muted">@{person.username}</span>
                  <button className="secondary" disabled={busy} onClick={() => blockAction(person.id, 'DELETE')}>
                    Engeli kaldır
                  </button>
                </div>
              ))}
            </div>
          )}

          {error && <p role="alert" className="error">{error}</p>}
        </section>

        {active
          ? <Chat key={active.id} friend={active} user={user} mangas={mangas} preselected={share?.id ?? null} openManga={openManga} />
          : <section className="community-panel"><p className="muted">
            {share ? `«${share.title}» mangasını paylaşmak için bir arkadaşının sohbetini aç.` : 'Mesajlaşmak için bir arkadaşının sohbetini aç.'}
          </p></section>}
      </div>
    </section>
  )
}
