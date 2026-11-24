import { createHash, randomBytes, scrypt, timingSafeEqual } from 'node:crypto'
import { promisify } from 'node:util'
import { Router } from 'express'
import { rateLimit } from 'express-rate-limit'
import { clean, fail, query } from './db.mjs'

const deriveKey = promisify(scrypt)
const cookie = 'kage95_session'
const sessionMs = 7 * 24 * 60 * 60 * 1000
const cookieOptions = {
  httpOnly: true,
  sameSite: 'lax',
  secure: process.env.NODE_ENV === 'production',
  path: '/',
}
const scryptOptions = { N: 131072, r: 8, p: 1, maxmem: 192 * 1024 * 1024 }
const columns = 'id, username, first_name AS firstName, last_name AS lastName, email'
const tokenHash = (token) => createHash('sha256').update(token).digest()
const validToken = (token) => typeof token === 'string' && /^[A-Za-z0-9_-]{43}$/.test(token)

let activeDerivations = 0

async function passwordKey(password, salt) {
  if (activeDerivations >= 2) fail(503, 'Lütfen biraz sonra tekrar dene.')
  activeDerivations++
  try {
    return await deriveKey(password, salt, 32, scryptOptions)
  } finally {
    activeDerivations--
  }
}

async function startSession(req, res, userId) {
  const token = randomBytes(32).toString('base64url')
  const previousToken = req.cookies[cookie]
  await query(`INSERT INTO sessions (token_hash, user_id, expires_at)
    VALUES (?, ?, UTC_TIMESTAMP() + INTERVAL 7 DAY)`, [tokenHash(token), userId])
  if (validToken(previousToken)) {
    await query('DELETE FROM sessions WHERE token_hash = ?', [tokenHash(previousToken)])
  }
  res.cookie(cookie, token, { ...cookieOptions, maxAge: sessionMs })
}

export async function readSession(req, _res, next) {
  const token = req.cookies[cookie]
  if (validToken(token)) {
    const [user] = await query(`SELECT u.id, u.username, u.first_name AS firstName, u.last_name AS lastName, u.email
      FROM sessions s JOIN users u ON u.id = s.user_id
      WHERE s.token_hash = ? AND s.expires_at > UTC_TIMESTAMP()`, [tokenHash(token)])
    req.user = user
  }
  next()
}

export function requireUser(req, _res, next) {
  if (!req.user) fail(401, 'Bu işlem için giriş yap.')
  next()
}

const auth = Router()
const limit = rateLimit({
  windowMs: 15 * 60 * 1000,
  limit: 25,
  message: { error: 'Çok fazla deneme yaptın. Biraz sonra tekrar dene.' },
})

auth.get('/me', (req, res) => res.json({ user: req.user ?? null }))

auth.post('/register', limit, async (req, res) => {
  const input = req.body ?? {}
  const firstName = clean(input.firstName)
  const lastName = clean(input.lastName)
  const email = clean(input.email).toLowerCase()
  const username = clean(input.username).toLowerCase()
  const password = input.password
  if (!firstName || firstName.length > 60 || !lastName || lastName.length > 60) {
    fail(400, 'Ad ve soyad 1–60 karakter olmalı.')
  }
  if (email.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) fail(400, 'Geçerli bir e-posta gir.')
  if (!/^[a-z0-9_]{3,24}$/.test(username)) fail(400, 'Kullanıcı adı 3–24 harf, rakam veya alt çizgi içermeli.')
  if (typeof password !== 'string' || password.length < 10 || password.length > 128) {
    fail(400, 'Şifre 10–128 karakter olmalı.')
  }
  const salt = randomBytes(16).toString('hex')
  const hash = await passwordKey(password, salt)
  let result
  try {
    result = await query(`INSERT INTO users (first_name, last_name, email, username, password_hash)
      VALUES (?, ?, ?, ?, ?)`, [firstName, lastName, email, username, `${salt}:${hash.toString('hex')}`])
  } catch (error) {
    if (error.code === 'ER_DUP_ENTRY') fail(409, 'Bu e-posta veya kullanıcı adı zaten kullanılıyor.')
    throw error
  }
  const user = { id: result.insertId, firstName, lastName, email, username }
  await startSession(req, res, user.id)
  res.status(201).json({ user })
})

auth.post('/login', limit, async (req, res) => {
  const login = clean(req.body?.login).toLowerCase()
  const password = req.body?.password
  if (!login || login.length > 254 || typeof password !== 'string' || password.length > 128) {
    fail(400, 'Giriş bilgilerini kontrol et.')
  }
  const field = login.includes('@') ? 'email' : 'username'
  const [record] = await query(`SELECT ${columns}, password_hash FROM users WHERE ${field} = ?`, [login])
  const [salt, hash] = record?.password_hash.split(':') ?? ['0'.repeat(32), '0'.repeat(64)]
  const key = await passwordKey(password, salt)
  if (!timingSafeEqual(key, Buffer.from(hash, 'hex')) || !record) fail(401, 'Giriş bilgileri hatalı.')
  const { password_hash: _hash, ...user } = record
  await startSession(req, res, user.id)
  res.json({ user })
})

auth.post('/logout', async (req, res) => {
  const token = req.cookies[cookie]
  if (validToken(token)) await query('DELETE FROM sessions WHERE token_hash = ?', [tokenHash(token)])
  res.clearCookie(cookie, cookieOptions).status(204).end()
})

export default auth
