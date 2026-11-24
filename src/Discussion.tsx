import { useEffect, useState, type FormEvent } from 'react'
import { api, errorText, formatDate, type Discussion as DiscussionData, type User } from './api'

export function Discussion({ mangaId, user, signIn, ratingChanged }: {
  mangaId: number; user: User | null; signIn: () => void; ratingChanged: () => void
}) {
  const [data, setData] = useState<DiscussionData | null>(null)
  const [score, setScore] = useState('')
  const [comment, setComment] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const path = `/mangas/${mangaId}`

  useEffect(() => {
    let active = true
    api<DiscussionData>(path + '/discussion').then((result) => {
      if (active) {
        setData(result)
        setScore(result.rating.mine?.toString() ?? '')
      }
    }).catch((error) => { if (active) setError(errorText(error)) })
    return () => { active = false }
  }, [path])

  async function act(action: () => Promise<void>) {
    setBusy(true)
    setError('')
    try { await action() } catch (error) { setError(errorText(error)) } finally { setBusy(false) }
  }

  function saveRating(event: FormEvent) {
    event.preventDefault()
    void act(async () => {
      await api(path + '/rating', 'PUT', { score: Number(score) })
      ratingChanged()
      setData(await api<DiscussionData>(path + '/discussion'))
    })
  }

  function postComment(event: FormEvent) {
    event.preventDefault()
    void act(async () => {
      await api(path + '/comments', 'POST', { body: comment })
      setComment('')
      setData(await api<DiscussionData>(path + '/discussion'))
    })
  }

  return (
    <section className="discussion stack">
      <h3>Puan ve yorumlar</h3>
      {data && <div className="muted">{data.rating.average === null
        ? 'Henüz topluluk puanı yok.'
        : `${data.rating.average.toLocaleString('tr-TR')} / 10 · ${data.rating.count} oy`}</div>}
      {user ? <>
        <form className="inline-form" onSubmit={saveRating}>
          <label htmlFor="score">Puanın</label>
          <select id="score" required value={score} onChange={(event) => setScore(event.target.value)}>
            <option value="">Seç</option>
            {Array.from({ length: 10 }, (_, index) => index + 1).map((value) => <option key={value}>{value}</option>)}
          </select>
          <button className="secondary" disabled={busy || !data}>Puanı kaydet</button>
        </form>
        <form className="stack" onSubmit={postComment}>
          <label>Yorumun<textarea value={comment} onChange={(event) => setComment(event.target.value)}
            rows={3} maxLength={1000} required /></label>
          <button className="primary align-start" disabled={busy || !comment.trim()}>Yorum gönder</button>
        </form>
      </> : <button className="secondary align-start" onClick={signIn}>Yorum ve puan için giriş yap</button>}
      {error && <p role="alert" className="error">{error}</p>}
      {!data && !error && <p role="status" className="muted">Yorumlar yükleniyor…</p>}
      {data?.comments.length === 0 && <p className="muted">Henüz yorum yok.</p>}
      {data?.comments.map((item) => <article className="comment" key={item.id}>
        <div><strong>@{item.username}</strong><time dateTime={item.createdAt}>{formatDate(item.createdAt)}</time></div>
        <p>{item.body}</p>
      </article>)}
      {data?.hasMore && <button className="secondary align-start" disabled={busy} onClick={() => void act(async () => {
        const older = await api<DiscussionData>(`${path}/discussion?before=${data.comments.at(-1)?.id}`)
        setData({ ...older, comments: [...data.comments, ...older.comments] })
      })}>Önceki yorumları yükle</button>}
    </section>
  )
}
