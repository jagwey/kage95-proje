import { fileURLToPath } from 'node:url'
import express from 'express'
import cookieParser from 'cookie-parser'
import { rateLimit } from 'express-rate-limit'
import auth, { readSession } from './auth.mjs'
import catalog from './catalog.mjs'
import social from './social.mjs'
import { query } from './db.mjs'

const app = express()
if (process.env.TRUST_PROXY) app.set('trust proxy', process.env.TRUST_PROXY)
app.disable('x-powered-by')
app.use((_req, res, next) => {
  res.set('X-Content-Type-Options', 'nosniff')
  res.set('X-Frame-Options', 'DENY')
  next()
})

app.use('/api', (req, res, next) => {
  res.set('Cache-Control', 'no-store')
  const write = !['GET', 'HEAD', 'OPTIONS'].includes(req.method)
  if (write && req.get('X-Kage95') !== '1') return res.status(403).json({ error: 'İstek doğrulanamadı.' })
  next()
})
app.use('/api', express.json({ limit: '12kb' }), cookieParser(), readSession)
app.use('/api/auth', auth)
const writeLimit = rateLimit({
  windowMs: 60_000,
  limit: 90,
  skip: (req) => req.method === 'GET' || req.method === 'HEAD',
  message: { error: 'Çok fazla işlem yaptın. Biraz sonra tekrar dene.' },
})
app.use('/api', writeLimit, catalog)
app.use('/api/social', social)
app.use('/api', (_req, res) => res.status(404).json({ error: 'API yolu bulunamadı.' }))
app.use(express.static(fileURLToPath(new URL('../dist/', import.meta.url))))
app.use((error, _req, res, _next) => {
  if (error.type === 'entity.parse.failed') return res.status(400).json({ error: 'İstek okunamadı.' })
  if (error.type === 'entity.too.large') return res.status(413).json({ error: 'İstek çok büyük.' })
  if (error.status >= 400 && error.status < 600) return res.status(error.status).json({ error: error.message })
  console.error('API error:', error.code || error.name)
  res.status(500).json({ error: 'Sunucu işlemi tamamlayamadı.' })
})

const cleanup = () => query('DELETE FROM sessions WHERE expires_at <= UTC_TIMESTAMP()')
  .catch((error) => console.error('Session cleanup:', error.code || error.name))
const port = Number(process.env.PORT || 3001)
app.listen(port, process.env.HOST || '127.0.0.1', () => console.log(`KAGE95 ${port} portunda çalışıyor.`))
void cleanup()
setInterval(cleanup, 60 * 60 * 1000).unref()
