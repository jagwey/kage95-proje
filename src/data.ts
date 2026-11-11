// TypeScript: Bir manga kaydında hangi bilgilerin bulunacağını tanımlar.
export type Manga = {
  id: number
  title: string
  japaneseTitle: string
  author: string
  genres: string[]
  summary: string
  cover: string
  status: 'Devam Ediyor' | 'Tamamlandı'
  rating: number
  chapters: number
  year: number
  views: string
  accent: string
}

// Şimdilik gerçek veritabanı yerine bu örnek verileri kullanıyoruz.
// Daha sonra ASP.NET API + MS SQL'den gelen bilgiler burada kullanılabilir.
export const mangas: Manga[] = [
  {
    id: 1,
    title: 'Wind Breaker', japaneseTitle: 'WIND BREAKER', author: 'Satoru Nii',
    genres: ['Aksiyon', 'Dram'],
    summary: 'Haruka Sakura, en güçlülerin toplandığı Furin Lisesi’ne zirveye ulaşmak için gelir. Ancak okulun öğrencileri artık şehri koruyan Bofurin adlı bir topluluğa dönüşmüştür.',
    cover: '/covers/wind-breaker.png', status: 'Devam Ediyor', rating: 9.6, chapters: 214, year: 2021, views: '1.2M', accent: '#68c58a',
  },
  {
    id: 2,
    title: 'Vinland Saga', japaneseTitle: 'ヴィンランド・サガ', author: 'Makoto Yukimura',
    genres: ['Aksiyon', 'Dram', 'Tarih'],
    summary: 'Thorfinn, çocukluğundan beri intikam peşinde yaşayan genç bir savaşçıdır. Vikingler çağında geçen bu hikâye, savaşın içinden barış ve özgürlük arayışına uzanır.',
    cover: '/covers/vinland-saga.png', status: 'Tamamlandı', rating: 9.8, chapters: 217, year: 2005, views: '980K', accent: '#b7a56a',
  },
  {
    id: 3,
    title: 'Re:ZERO', japaneseTitle: 'Re:ゼロから始める異世界生活', author: 'Tappei Nagatsuki',
    genres: ['Fantastik', 'Dram', 'Aksiyon'],
    summary: 'Subaru Natsuki başka bir dünyaya çağrılır ve ölümden sonra zamanı geri alma gücüyle sevdiklerini korumaya çalışır.',
    cover: '/covers/re-zero.png', status: 'Devam Ediyor', rating: 9.4, chapters: 64, year: 2014, views: '860K', accent: '#a99be8',
  },
]

export const genres = ['Tümü', 'Aksiyon', 'Dram', 'Tarih', 'Fantastik']
