# KAGE95 Manga

KAGE95, React ve TypeScript ile hazırlanmış bir manga kataloğu. Node.js ve Express API’si hesapları, katalog kayıtlarını, favorileri, yorumları, puanları, arkadaşlıkları ve sohbet geçmişini MySQL’de saklar.

## Yerelde çalıştırma

Node.js 22.12+ ve MySQL 8.4 kullan. MySQL’de yönetici hesabıyla uygulamaya özel bir veritabanı ve kullanıcı oluştur. Örnek yerel kurulumda parola yer tutucusunu kendi değerine değiştir.

```sql
CREATE DATABASE kage95 CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'kage95'@'127.0.0.1' IDENTIFIED BY 'BURAYA_YEREL_PAROLANI_YAZ';
GRANT ALL PRIVILEGES ON kage95.* TO 'kage95'@'127.0.0.1';
```

`.env.example` dosyasını `.env` olarak kopyala. MySQL bağlantı bilgilerini doldur ve kayıtları kur.

```sh
npm ci
npm run db:setup
```

İlk terminalde `npm run dev:api`, ikinci terminalde `npm run dev` çalıştır. Tarayıcıda Vite’ın gösterdiği adresi aç. Vite, `/api` isteklerini varsayılan olarak `127.0.0.1:3001` adresine iletir. API farklı porttaysa Vite’ı başlatırken `KAGE95_API_TARGET` ortam değişkenini ayarla.

Kurulum komutu eksik tabloları ve başlangıçtaki üç mangayı ekler. Yeniden çalıştırıldığında mevcut katalog kayıtlarını ve kullanıcı verilerini korur.

## Kodun akışı

| Dosya | Görevi |
| --- | --- |
| `src/App.tsx`, `src/Account.tsx`, `src/Discussion.tsx`, `src/Community.tsx` | Katalog ve hesap ekranları, yorumlar ve sohbet |
| `src/api.ts` | Tarayıcıdan API’ye JSON isteği gönderir ve hata yanıtlarını okur |
| `server/index.mjs` | HTTP sunucusu, ortak kontroller ve üretim derlemesinin sunulması |
| `server/auth.mjs` | Kayıt, giriş ve çıkış. Şifreler tuzlu scrypt özeti olarak saklanır. Çerezdeki oturum belirtecinin özeti veritabanında 7 gün tutulur |
| `server/catalog.mjs` | Katalog, favoriler, yorum ve 1–10 puan |
| `server/social.mjs` | Arkadaşlık istekleri, mesajlar ve manga paylaşımı |
| `server/schema.sql`, `shared/mangas.json` | MySQL şeması ve başlangıç kataloğu |

**Favori örneği.** Kullanıcı kalbe bastığında React, `PUT /api/favorites/1` gönderir. Express oturumdan kullanıcıyı belirler ve `favorites` tablosuna `(user_id, manga_id)` kaydı yazar. Birleşik birincil anahtar aynı kaydın tekrar eklenmesini önler. Hesaba girişte bu tarayıcıda bekleyen misafir favorileri de hesaba aktarılır.

```sql
SELECT m.id, m.title, m.cover
FROM favorites f
JOIN mangas m ON m.id = f.manga_id
WHERE f.user_id = ?
ORDER BY f.created_at DESC;
```

**Puan ve yorumlar.** Bir kullanıcı her mangaya bir puan verir ve sonradan güncelleyebilir. Katalog gerçek oyların ortalamasını, henüz oy yoksa başlangıç puanını gösterir. Yorumlar herkese görünür ve sayfalar halinde yüklenir.

**Arkadaşlık ve sohbet.** İki hesap için kimlikler küçükten büyüğe sıralanır ve tek arkadaşlık kaydı kullanılır. İsteği alan kullanıcı kabul edince sohbet açılır. Mesaj metni ve isteğe bağlı manga kimliği MySQL’de saklanır. Sohbet açıkken tarayıcı her dört saniyede bir yeni mesajları alır. Eski mesajlar sayfalar halinde açılır.

**Katalog yönetimi.** İlk veri `shared/mangas.json` dosyasından gelir. Tam sürüm kataloğu MySQL’den okur. Yönetim paneli sonraki aşama.

## Tam sürüm ve GitHub Pages

Sunucuda `npm ci`, `npm run db:setup`, `npm run build` ve `npm start` çalıştır. Express hem derlenmiş React uygulamasını hem API’yi aynı adreste sunar. Canlıda HTTPS sağlayan reverse proxy kullan ve `NODE_ENV=production` ayarla. Proxy aynı makinede çalışıyorsa `TRUST_PROXY=loopback` ayarı kullanılabilir. MySQL bağlantı bilgilerini sunucudaki `.env` dosyasında tut.

Mevcut GitHub Actions akışı `npm run build:pages` ile statik bir katalog önizlemesi yayınlar. Bu önizlemenin favorileri tarayıcıda tutulur. Hesap ve sosyal özellikler için Express ve MySQL çalışan tam sürüm gerekir.
