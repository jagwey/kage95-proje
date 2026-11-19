import { Router } from 'express'
import { requireUser } from './auth.mjs'
import { clean, db, fail, id, query } from './db.mjs'

const social = Router()
social.use(requireUser)

function pairFor(req) {
  const other = id(req.params.id)
  const me = req.user.id
  if (me === other) fail(400, 'Kendi hesabınla bu işlemi yapamazsın.')
  return me < other ? [me, other] : [other, me]
}

async function acceptedPair(req) {
  const pair = pairFor(req)
  const [friendship] = await query(`SELECT status FROM friendships
    WHERE user_low = ? AND user_high = ?`, pair)
  if (friendship?.status !== 'accepted') fail(403, 'Bu sohbet için arkadaşlık isteğinin kabul edilmesi gerekiyor.')
  return pair
}

social.get('/users', async (req, res) => {
  const username = clean(req.query.username).toLowerCase()
  if (!/^[a-z0-9_]{3,24}$/.test(username)) return res.json([])
  res.json(await query('SELECT id, username FROM users WHERE username = ? AND id <> ?', [username, req.user.id]))
})

social.get('/friends', async (req, res) => {
  const me = req.user.id
  res.json(await query(`SELECT u.id, u.username, f.status, f.requested_by AS requestedBy
    FROM friendships f JOIN users u ON u.id = CASE WHEN f.user_low = ? THEN f.user_high ELSE f.user_low END
    WHERE f.user_low = ? OR f.user_high = ? ORDER BY u.username`, [me, me, me]))
})

social.post('/friends/:id', async (req, res) => {
  const pair = pairFor(req)
  const [user] = await query('SELECT id FROM users WHERE id = ?', [id(req.params.id)])
  if (!user) fail(404, 'Kullanıcı bulunamadı.')
  await query(`INSERT INTO friendships (user_low, user_high, requested_by)
    VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE status = status`, [...pair, req.user.id])
  res.status(204).end()
})

social.patch('/friends/:id', async (req, res) => {
  const pair = pairFor(req)
  const result = await query(`UPDATE friendships SET status = 'accepted'
    WHERE user_low = ? AND user_high = ? AND requested_by <> ? AND status = 'pending'`, [...pair, req.user.id])
  if (!result.affectedRows) {
    const [existing] = await query('SELECT status FROM friendships WHERE user_low = ? AND user_high = ?', pair)
    if (existing?.status !== 'accepted') fail(409, 'Kabul edilebilecek bir istek bulunamadı.')
  }
  res.status(204).end()
})

social.delete('/friends/:id', async (req, res) => {
  await query("DELETE FROM friendships WHERE user_low = ? AND user_high = ? AND status = 'pending'", pairFor(req))
  res.status(204).end()
})

social.get('/friends/:id/messages', async (req, res) => {
  const pair = await acceptedPair(req)
  const before = req.query.before === undefined ? null : id(req.query.before)
  const after = req.query.after === undefined ? null : id(req.query.after)
  if (before !== null && after !== null) fail(400, 'Tek bir mesaj geçmişi konumu belirt.')
  const rows = await query(`SELECT m.id, m.sender_id AS senderId, m.body, m.manga_id AS mangaId,
    a.title AS mangaTitle, m.created_at AS createdAt
    FROM messages m LEFT JOIN mangas a ON a.id = m.manga_id
    WHERE m.user_low = ? AND m.user_high = ?
    ${before !== null ? 'AND m.id < ?' : ''} ${after !== null ? 'AND m.id > ?' : ''}
    ORDER BY m.id ${after === null ? 'DESC' : 'ASC'} LIMIT 51`,
  before === null && after === null ? pair : [...pair, before ?? after])
  const messages = rows.slice(0, 50)
  res.json({ messages: after === null ? messages.reverse() : messages, hasMore: rows.length > 50 })
})

social.post('/friends/:id/messages', async (req, res) => {
  const pair = pairFor(req)
  const body = clean(req.body?.body)
  const mangaId = req.body?.mangaId == null ? null : id(req.body.mangaId)
  if ((!body && mangaId === null) || body.length > 2000) fail(400, 'Mesaj metni veya manga seç. Metin en fazla 2000 karakter olabilir.')
  if (mangaId !== null) {
    const [manga] = await query('SELECT id FROM mangas WHERE id = ?', [mangaId])
    if (!manga) fail(404, 'Paylaşılacak manga bulunamadı.')
  }
  const connection = await db.getConnection()
  try {
    await connection.beginTransaction()
    // Serialize sends within a conversation so polling cannot skip an earlier message still committing.
    const [[friendship]] = await connection.execute(
      'SELECT status FROM friendships WHERE user_low = ? AND user_high = ? FOR UPDATE', pair)
    if (friendship?.status !== 'accepted') fail(403, 'Bu sohbet için arkadaşlık isteğinin kabul edilmesi gerekiyor.')
    await connection.execute(
      'INSERT INTO messages (user_low, user_high, sender_id, body, manga_id) VALUES (?, ?, ?, ?, ?)',
      [...pair, req.user.id, body || null, mangaId])
    await connection.commit()
  } catch (error) {
    await connection.rollback()
    throw error
  } finally {
    connection.release()
  }
  res.status(201).end()
})

export default social
