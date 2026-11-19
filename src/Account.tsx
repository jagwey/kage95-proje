import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { api, errorText, type User } from './api'

export function Modal({ children, label, close, className = '' }: {
  children: ReactNode; label: string; close: () => void; className?: string
}) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const dialog = ref.current
    dialog?.showModal()
    return () => dialog?.close()
  }, [])
  return (
    <dialog ref={ref} className={`modal-content ${className}`} aria-label={label}
      onCancel={(event) => { event.preventDefault(); close() }}
      onClick={(event) => { if (event.target === event.currentTarget) close() }}>
      <div className="dialog-body">
        <button className="close" type="button" onClick={close} aria-label="Kapat"><X size={20} /></button>
        {children}
      </div>
    </dialog>
  )
}

export function Account({ close, signedIn }: { close: () => void; signedIn: (user: User) => Promise<void> }) {
  const [register, setRegister] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const label = register ? 'Kayıt ol' : 'Giriş yap'

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const body = Object.fromEntries(new FormData(event.currentTarget))
      const { user } = await api<{ user: User }>(register ? '/auth/register' : '/auth/login', 'POST', body)
      await signedIn(user)
    } catch (error) {
      setError(errorText(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal label={label} close={() => { if (!busy) close() }} className="account-dialog">
      <h2>{label}</h2>
      <form key={label} className="stack" onSubmit={submit}>
        {register ? <>
          <div className="form-grid">
            <label>Ad<input name="firstName" autoComplete="given-name" maxLength={60} required /></label>
            <label>Soyad<input name="lastName" autoComplete="family-name" maxLength={60} required /></label>
          </div>
          <label>E-posta<input name="email" type="email" autoComplete="email" maxLength={254} required /></label>
          <label>Kullanıcı adı<input name="username" autoComplete="username" minLength={3} maxLength={24}
            pattern="[a-zA-Z0-9_]{3,24}" required /><small>3–24 harf, rakam veya alt çizgi</small></label>
        </> : <label>E-posta veya kullanıcı adı<input name="login" autoComplete="username" maxLength={254} required /></label>}
        <label>Şifre<input name="password" type="password" autoComplete={register ? 'new-password' : 'current-password'}
          minLength={register ? 10 : undefined} maxLength={128} required />
          {register && <small>En az 10 karakter</small>}
        </label>
        {error && <p role="alert" className="error">{error}</p>}
        <button type="submit" className="primary" disabled={busy}>{busy ? 'Bekle…' : label}</button>
      </form>
      <button type="button" className="secondary account-switch" disabled={busy}
        onClick={() => { setRegister(!register); setError('') }}>{register ? 'Giriş yap' : 'Hesap oluştur'}</button>
    </Modal>
  )
}
