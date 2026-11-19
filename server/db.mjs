import mysql from 'mysql2/promise'

export const db = mysql.createPool({
  host: process.env.DB_HOST || '127.0.0.1',
  port: Number(process.env.DB_PORT || 3306),
  database: process.env.DB_NAME || 'kage95',
  user: process.env.DB_USER || 'kage95',
  password: process.env.DB_PASSWORD,
  charset: 'utf8mb4',
  timezone: 'Z',
  connectionLimit: 8,
  queueLimit: 64,
})

db.on('connection', (connection) => {
  connection.query("SET time_zone = '+00:00'", (error) => {
    if (error) connection.destroy()
  })
})

export async function query(sql, params = []) {
  const [result] = await db.execute(sql, params)
  return result
}

export function fail(status, message) {
  throw Object.assign(new Error(message), { status })
}

export function id(value) {
  if ((typeof value !== 'number' && typeof value !== 'string')
    || !/^[1-9][0-9]{0,9}$/.test(String(value)) || Number(value) > 4294967295) {
    fail(400, 'Geçersiz kayıt numarası.')
  }
  return Number(value)
}

export function clean(value) {
  return typeof value === 'string' ? value.trim() : ''
}
