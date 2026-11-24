import { useEffect, useState } from 'react'
import { Award, Trophy } from 'lucide-react'
import { api, errorText, mediaUrl, roleLabel, type LeaderboardResponse, type User } from './api'

type Period = 'tum' | 'ay' | 'hafta'

const periodLabels: Record<Period, string> = {
  tum: 'Tüm Zamanlar',
  ay: 'Bu Ay',
  hafta: 'Bu Hafta',
}

export function Leaderboard({ user, signIn }: { user: User | null; signIn: () => void }) {
  const [period, setPeriod] = useState<Period>('tum')
  const [data, setData] = useState<LeaderboardResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    setLoading(true)
    setError('')
    api<LeaderboardResponse>(`/leaderboard?period=${period}&limit=50`)
      .then((res) => { if (active) setData(res) })
      .catch((err) => { if (active) setError(errorText(err)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [period, user?.id])

  const me = data?.me
  const nextThreshold = me ? me.totalPoints + me.nextLevelRemaining : 100
  const levelSpan = me ? Math.max(1, nextThreshold - me.levelRequired) : 100
  const levelProgress = me
    ? (me.nextLevelRemaining === 0 ? 100 : Math.min(100, Math.round(((me.totalPoints - me.levelRequired) / levelSpan) * 100)))
    : 0

  return (
    <section className="stack">
      <div className="heading">
        <div>
          <p className="label">KAGE95 SIRALAMA</p>
          <h2 className="inline-title"><Trophy size={26} /> Liderlik Tablosu</h2>
        </div>
        <div className="filters">
          {(['tum', 'ay', 'hafta'] as Period[]).map((p) => (
            <button key={p} className={period === p ? 'selected' : ''} onClick={() => setPeriod(p)}>
              {periodLabels[p]}
            </button>
          ))}
        </div>
      </div>

      {me ? (
        <div className="community-panel rank-banner">
          <div className="rank-main">
            <div>
              <p className="label">SENİN KONUMUN</p>
              <h3>
                {me.rank ? `#${me.rank}` : 'Sıralama dışı'} · <span className="rank-title">{me.title}</span>
              </h3>
              <p className="muted">
                Seviye {me.level} · Dönem puanı: <strong>{me.points}</strong> · Toplam: <strong>{me.totalPoints}</strong> puan
              </p>
            </div>
            <div className="rank-progress-wrap">
              <div className="rank-progress-bar">
                <div style={{ width: `${levelProgress}%` }} />
              </div>
              <small className="muted">
                {me.nextLevelRemaining > 0
                  ? `Seviye ${me.level + 1} için ${me.nextLevelRemaining} puan kaldı`
                  : 'Maksimum seviyeye (95) ulaştın!'}
                {me.nextTitle ? ` · Sonraki rütbe (${me.nextTitle}): ${me.nextTitleRemaining} puan` : ''}
              </small>
            </div>
          </div>
        </div>
      ) : (
        <div className="community-panel friend-row">
          <p className="muted">Puan kazanmak, rütbe atlamak ve sıralamada yer almak için giriş yap.</p>
          <button className="primary" onClick={signIn}>Giriş yap</button>
        </div>
      )}

      {error && <p role="alert" className="error">{error}</p>}
      {loading && <p role="status" className="muted">Liderlik tablosu yükleniyor…</p>}

      {!loading && data && (
        <div className="leaderboard-grid">
          <div className="community-panel stack">
            <h3>{periodLabels[period]} Sıralaması</h3>
            {data.list.length === 0 ? (
              <p className="muted">Bu dönemde henüz puan kazanan yok. İlk sıraya yerleş!</p>
            ) : (
              <ol className="leaderboard-list">
                {data.list.map((row) => {
                  const badge = roleLabel(row.role)
                  const medal = row.rank === 1 ? '🥇' : row.rank === 2 ? '🥈' : row.rank === 3 ? '🥉' : `#${row.rank}`
                  return (
                    <li key={row.userId} className={`leaderboard-row ${user?.id === row.userId ? 'is-me' : ''}`}>
                      <span className="lb-rank">{medal}</span>
                      <div className="avatar-mini">
                        {row.avatarUrl
                          ? <img src={mediaUrl(row.avatarUrl)} alt={row.username} />
                          : <span>{row.username[0]?.toUpperCase()}</span>}
                      </div>
                      <div className="lb-user">
                        <div>
                          <strong>@{row.username}</strong>
                          {badge && <span className={`role-badge role-${row.role}`}>{badge}</span>}
                        </div>
                        <small className="muted">{row.title} · Seviye {row.level}</small>
                      </div>
                      <strong className="lb-points">{row.points} puan</strong>
                    </li>
                  )
                })}
              </ol>
            )}
          </div>

          <aside className="community-panel stack">
            <h3 className="inline-title"><Award size={19} /> Puan Kuralları & Rütbeler</h3>
            <div className="rules-list">
              {data.rules.map((r) => (
                <div key={r.action} className="rule-item">
                  <div className="friend-row">
                    <strong>{r.name}</strong>
                    <span className="points-pill">+{r.points} puan</span>
                  </div>
                  <small className="muted">{r.rule}</small>
                </div>
              ))}
            </div>
            <p className="muted"><small>Günlük toplam üst sınır: {data.dailyCap} puan.</small></p>
            <div className="ranks-legend">
              <p className="label">RÜTBELER</p>
              <small className="muted">Sv. 1–2 Çırak · Sv. 3–5 Genç Gölge · Sv. 6–11 Gölge · Sv. 12–24 Usta · Sv. 25+ Efsane</small>
            </div>
          </aside>
        </div>
      )}
    </section>
  )
}
