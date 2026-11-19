import { Router } from 'express'
import { requireUser } from './auth.mjs'
import { clean, fail, id, query } from './db.mjs'

const catalog = Router()

async function requireManga(mangaId) {
  const [manga] = await query('SELECT id FROM mangas WHERE id = ?', [mangaId])
  if (!manga) fail(404, 'Manga bulunamadı.')
}

catalog.get('/mangas', async (_req, res) => {
  const mangas = await query(`SELECT m.id, m.title, m.japanese_title AS japaneseTitle, m.author, m.genres, m.summary,
    m.cover, m.status, COALESCE(r.average, m.catalog_rating) AS rating,
    COALESCE(r.total, 0) AS ratingCount, m.chapters, m.year, m.views, m.accent
    FROM mangas m LEFT JOIN (
      SELECT manga_id, ROUND(AVG(score), 1) AS average, COUNT(*) AS total FROM ratings GROUP BY manga_id
    ) r ON r.manga_id = m.id
    ORDER BY m.id`)
  res.json(mangas.map((manga) => ({
    ...manga,
    genres: typeof manga.genres === 'string' ? JSON.parse(manga.genres) : manga.genres,
    rating: Number(manga.rating),
    ratingCount: Number(manga.ratingCount),
  })))
})

async function favoriteIds(userId) {
  const rows = await query('SELECT manga_id FROM favorites WHERE user_id = ? ORDER BY created_at DESC', [userId])
  return rows.map((row) => row.manga_id)
}

catalog.get('/favorites', requireUser, async (req, res) => {
  res.json(await favoriteIds(req.user.id))
})

catalog.put('/favorites', requireUser, async (req, res) => {
  const inputs = req.body?.ids
  if (!Array.isArray(inputs) || inputs.length === 0 || inputs.length > 300) fail(400, '1–300 favori gönder.')
  const mangaIds = [...new Set(inputs.map(id))]
  await query(`INSERT INTO favorites (user_id, manga_id)
    SELECT ?, id FROM mangas WHERE id IN (${mangaIds.map(() => '?').join(', ')})
    ON DUPLICATE KEY UPDATE created_at = favorites.created_at`, [req.user.id, ...mangaIds])
  res.json(await favoriteIds(req.user.id))
})

catalog.put('/favorites/:id', requireUser, async (req, res) => {
  const mangaId = id(req.params.id)
  await requireManga(mangaId)
  await query(`INSERT INTO favorites (user_id, manga_id) VALUES (?, ?)
    ON DUPLICATE KEY UPDATE created_at = created_at`, [req.user.id, mangaId])
  res.status(204).end()
})

catalog.delete('/favorites/:id', requireUser, async (req, res) => {
  await query('DELETE FROM favorites WHERE user_id = ? AND manga_id = ?', [req.user.id, id(req.params.id)])
  res.status(204).end()
})

catalog.get('/mangas/:id/discussion', async (req, res) => {
  const mangaId = id(req.params.id)
  const before = req.query.before === undefined ? null : id(req.query.before)
  await requireManga(mangaId)
  const [ratings, comments] = await Promise.all([
    query(`SELECT ROUND(AVG(score), 1) AS average, COUNT(*) AS total,
      MAX(CASE WHEN user_id = ? THEN score END) AS mine FROM ratings WHERE manga_id = ?`,
    [req.user?.id ?? null, mangaId]),
    query(`SELECT c.id, c.body, c.created_at AS createdAt, u.username
      FROM comments c JOIN users u ON u.id = c.user_id
      WHERE c.manga_id = ? ${before === null ? '' : 'AND c.id < ?'}
      ORDER BY c.id DESC LIMIT 31`, before === null ? [mangaId] : [mangaId, before]),
  ])
  const { average, total, mine } = ratings[0]
  res.json({
    rating: { average: average === null ? null : Number(average), count: Number(total), mine },
    comments: comments.slice(0, 30),
    hasMore: comments.length > 30,
  })
})

catalog.put('/mangas/:id/rating', requireUser, async (req, res) => {
  const mangaId = id(req.params.id)
  const score = req.body?.score
  if (!Number.isInteger(score) || score < 1 || score > 10) fail(400, 'Puan 1 ile 10 arasında olmalı.')
  await requireManga(mangaId)
  await query('INSERT INTO ratings (manga_id, user_id, score) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE score = ?',
    [mangaId, req.user.id, score, score])
  res.status(204).end()
})

catalog.post('/mangas/:id/comments', requireUser, async (req, res) => {
  const mangaId = id(req.params.id)
  const body = clean(req.body?.body)
  if (!body || body.length > 1000) fail(400, 'Yorum 1–1000 karakter olmalı.')
  await requireManga(mangaId)
  await query('INSERT INTO comments (manga_id, user_id, body) VALUES (?, ?, ?)', [mangaId, req.user.id, body])
  res.status(201).end()
})

export default catalog
