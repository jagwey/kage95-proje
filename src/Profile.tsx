import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { Shield, Trophy, Upload, User as UserIcon } from 'lucide-react'
import {
  api,
  errorText,
  formatDate,
  isStaff,
  mediaUrl,
  roleLabel,
  uploadImage,
  type MyProfile,
  type User,
} from './api'

export function Profile({
  user,
  onUserUpdated,
  onNavigate,
}: {
  user: User
  onUserUpdated: (updated: User) => void
  onNavigate: (page: 'reading' | 'leaderboard' | 'admin' | 'favorites') => void
}) {
  const [profile, setProfile] = useState<MyProfile | null>(null)
  const [firstName, setFirstName] = useState(user.firstName)
  const [lastName, setLastName] = useState(user.lastName)
  const [bio, setBio] = useState(user.bio ?? '')
  const [avatarUrl, setAvatarUrl] = useState(user.avatarUrl ?? '')
  const [busy, setBusy] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [savedMsg, setSavedMsg] = useState('')
  const [error, setError] = useState('')

  async function loadProfile() {
    setError('')
    try {
      const data = await api<MyProfile>('/profile/me')
      setProfile(data)
      setFirstName(data.firstName)
      setLastName(data.lastName)
      setBio(data.bio ?? '')
      setAvatarUrl(data.avatarUrl ?? '')
    } catch (err) {
      setError(errorText(err))
    }
  }

  useEffect(() => {
    void loadProfile()
  }, [user.id])

  async function saveProfile(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError('')
    setSavedMsg('')
    try {
      const res = await api<{ user: User }>('/profile/me', 'PUT', {
        firstName,
        lastName,
        bio: bio.trim() || null,
        avatarUrl: avatarUrl.trim() || null,
      })
      onUserUpdated(res.user)
      setSavedMsg('Profil bilgilerin güncellendi.')
      await loadProfile()
    } catch (err) {
      setError(errorText(err))
    } finally {
      setBusy(false)
    }
  }

  async function onAvatarFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) return
    setUploading(true)
    setError('')
    setSavedMsg('')
    try {
      const url = await uploadImage('/profile/avatar', file)
      setAvatarUrl(url)
      onUserUpdated({ ...user, avatarUrl: url })
      setSavedMsg('Profil resmi yüklendi!')
      await loadProfile()
    } catch (err) {
      setError(errorText(err))
    } finally {
      setUploading(false)
      event.target.value = ''
    }
  }

  const badge = roleLabel(user.role)
  const rank = profile?.rank

  return (
    <section className="stack">
      <div className="heading">
        <div>
          <p className="label">HESABIM</p>
          <h2 className="inline-title"><UserIcon size={26} /> @{user.username}</h2>
        </div>
        <div className="inline-form">
          <button className="secondary" onClick={() => onNavigate('reading')}>Okuma Listem</button>
          <button className="secondary" onClick={() => onNavigate('favorites')}>Favorilerim</button>
          <button className="secondary" onClick={() => onNavigate('leaderboard')}><Trophy size={15} /> Liderlik</button>
          {isStaff(user) && (
            <button className="primary" onClick={() => onNavigate('admin')}>
              <Shield size={15} /> {user.role === 'admin' ? 'Admin Paneli' : 'Mod Paneli'}
            </button>
          )}
        </div>
      </div>

      {error && <p role="alert" className="error">{error}</p>}
      {!profile && !error && <p role="status" className="muted">Hesap bilgileri yükleniyor…</p>}

      {profile && (
        <>
          <div className="community-panel profile-hero">
            <div className="profile-avatar-col">
              <div className="avatar-large">
                {profile.avatarUrl
                  ? <img src={mediaUrl(profile.avatarUrl)} alt={profile.username} />
                  : <span>{profile.username[0]?.toUpperCase()}</span>}
              </div>
              <label className="secondary upload-btn">
                <Upload size={14} /> {uploading ? 'Yükleniyor…' : 'Fotoğraf yükle'}
                <input type="file" accept="image/*" hidden disabled={uploading} onChange={(e) => void onAvatarFile(e)} />
              </label>
            </div>

            <div className="stack">
              <div className="inline-form">
                <h3>{profile.firstName} {profile.lastName}</h3>
                {badge && <span className={`role-badge role-${profile.role}`}>{badge}</span>}
                <span className="points-pill">{rank?.title} · Sv. {rank?.level}</span>
              </div>
              <p className="muted">@{profile.username} · {profile.email} · Katılım: {formatDate(profile.createdAt)}</p>
              {profile.bio && <p>{profile.bio}</p>}
              {rank && (
                <p className="muted">
                  Liderlik sırası: <strong>{rank.rank ? `#${rank.rank}` : '—'}</strong> · Toplam puan: <strong>{rank.totalPoints}</strong>
                  {rank.nextTitle ? ` · Sonraki rütbe (${rank.nextTitle}): ${rank.nextTitleRemaining} puan` : ''}
                </p>
              )}
            </div>
          </div>

          <div className="stats-grid">
            <div className="stat-box"><strong>{profile.stats.favoritesCount}</strong><span>Favori</span></div>
            <div className="stat-box"><strong>{profile.stats.readingCount}</strong><span>Okuyor</span></div>
            <div className="stat-box"><strong>{profile.stats.completedCount}</strong><span>Tamamlandı</span></div>
            <div className="stat-box"><strong>{profile.stats.ratingsCount}</strong><span>Puan</span></div>
            <div className="stat-box"><strong>{profile.stats.commentsCount}</strong><span>Yorum</span></div>
            <div className="stat-box"><strong>{profile.stats.friendsCount}</strong><span>Arkadaş</span></div>
          </div>

          <div className="leaderboard-grid">
            <form className="community-panel stack" onSubmit={saveProfile}>
              <h3>Profil Bilgilerini Düzenle</h3>
              <div className="form-grid">
                <label>Ad<input value={firstName} onChange={(e) => setFirstName(e.target.value)} maxLength={60} required /></label>
                <label>Soyad<input value={lastName} onChange={(e) => setLastName(e.target.value)} maxLength={60} required /></label>
              </div>
              <label>
                Biyografi
                <textarea value={bio} onChange={(e) => setBio(e.target.value)} maxLength={300} rows={3} placeholder="Kendinden ve sevdiğin mangalardan bahset..." />
              </label>
              <label>
                Profil Resmi URL (isteğe bağlı)
                <input value={avatarUrl} onChange={(e) => setAvatarUrl(e.target.value)} maxLength={500} placeholder="/uploads/avatars/... veya https://..." />
              </label>
              {savedMsg && <p className="success-text">{savedMsg}</p>}
              <button className="primary align-start" disabled={busy}>{busy ? 'Kaydediliyor…' : 'Değişiklikleri kaydet'}</button>
            </form>

            <section className="community-panel stack">
              <h3>Son Puan Hareketleri</h3>
              {profile.recentPoints.length === 0 ? (
                <p className="muted">Henüz puan kaydın yok.</p>
              ) : (
                <div className="rules-list">
                  {profile.recentPoints.map((item) => (
                    <div key={item.id} className="friend-row rule-item">
                      <div>
                        <strong>{item.label}</strong>
                        {item.note && <small className="muted"> · {item.note}</small>}
                        <div><small className="muted">{formatDate(item.createdAt)}</small></div>
                      </div>
                      <span className={`points-pill ${item.points < 0 ? 'negative' : ''}`}>
                        {item.points > 0 ? `+${item.points}` : item.points} puan
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </section>
          </div>
        </>
      )}
    </section>
  )
}
