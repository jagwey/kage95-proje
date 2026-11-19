import { readFile } from 'node:fs/promises'
import { db, query } from './db.mjs'

try {
  const schema = await readFile(new URL('./schema.sql', import.meta.url), 'utf8')
  for (const statement of schema.split(';').map((line) => line.trim()).filter(Boolean)) {
    await db.query(statement)
  }
  const mangas = JSON.parse(await readFile(new URL('../shared/mangas.json', import.meta.url), 'utf8'))
  for (const manga of mangas) {
    await query(`INSERT INTO mangas
      (id, title, japanese_title, author, genres, summary, cover, status, catalog_rating, chapters, year, views, accent)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
      ON DUPLICATE KEY UPDATE id = id`, [
      manga.id, manga.title, manga.japaneseTitle, manga.author, JSON.stringify(manga.genres),
      manga.summary, manga.cover, manga.status, manga.rating, manga.chapters, manga.year, manga.views, manga.accent,
    ])
  }
  console.log('MySQL tabloları ve başlangıç kataloğu hazır.')
} finally {
  await db.end()
}
