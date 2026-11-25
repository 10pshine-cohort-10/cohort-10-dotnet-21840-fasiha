import { useState } from 'react';
import { CheckCircle2, ListChecks, LogOut } from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { api } from './api.js';
import { Avatar, Brand, ErrorMessage } from './common.jsx';

export function AccountForm({ register, onLogin }) {
  const navigate = useNavigate();
  const { state } = useLocation();
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  async function submit(event) {
    event.preventDefault();
    const fields = new FormData(event.currentTarget);
    const email = fields.get('email').trim();
    const password = fields.get('password');
    setError(null);
    setSaving(true);
    try {
      if (register) {
        await api.register({ email, password, displayName: fields.get('displayName').trim() });
        navigate('/login', { replace: true, state: { registered: true, email } });
      } else {
        const user = await api.login({ email, password });
        onLogin(user);
        navigate('/', { replace: true });
      }
    } catch (failure) {
      setError(failure);
    } finally {
      setSaving(false);
    }
  }

  return (
    <main className="auth-shell">
      <div className="auth-form-wrap">
        <Brand />
        <section className="auth-form" aria-labelledby="auth-title">
          <h1 id="auth-title">{register ? 'Create your account' : 'Sign in to Taskline'}</h1>
          <p>{register ? 'Get a workspace to organize and track your tasks.' : 'Access your dashboard and pick up where you left off.'}</p>
          {!register && state?.registered && <p role="status" className="success"><CheckCircle2 size={18} aria-hidden="true" />Account created. Sign in to continue.</p>}
          <form className="fields" onSubmit={submit}>
            {register && <label>Display name<input name="displayName" autoComplete="name" minLength={2} maxLength={80} required /></label>}
            <label>Email address<input type="email" name="email" autoComplete="email" maxLength={256} defaultValue={state?.email || ''} required /></label>
            <label>Password<input type="password" name="password" autoComplete={register ? 'new-password' : 'current-password'} minLength={register ? 10 : undefined} maxLength={128} aria-describedby={register ? 'password-hint' : undefined} required />
              {register && <small id="password-hint">Use 10 or more characters, including a lowercase letter and a number.</small>}
            </label>
            {error && <ErrorMessage error={error} />}
            <button className="button primary auth-button" disabled={saving}>{saving ? 'Please wait…' : register ? 'Create account' : 'Sign in'}</button>
          </form>
          <p className="auth-switch">
            {register ? 'Already have an account?' : 'New to Taskline?'}{' '}
            <Link to={register ? '/login' : '/register'}>{register ? 'Sign in' : 'Create an account'}</Link>
          </p>
        </section>
      </div>
      <aside className="auth-aside">
        <div className="auth-motif" aria-hidden="true">
          <div className="motif-card">
            <ListChecks size={32} />
            <div className="motif-line"><CheckCircle2 /> <span /></div>
            <div className="motif-line"><CheckCircle2 /> <span /></div>
            <div className="motif-line"><span className="motif-ring" /> <span /></div>
          </div>
        </div>
        <p>Plan your work, track progress, and meet your deadlines.</p>
      </aside>
    </main>
  );
}

export function Profile({ user, onLogout }) {
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  async function logout() {
    setSaving(true);
    setError(null);
    try {
      await api.logout();
      onLogout();
    } catch (failure) {
      setError(failure);
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <header className="page-heading"><div><h1>Profile</h1><p>Your Taskline account information.</p></div></header>
      <section className="panel profile-card">
        <div className="profile-identity"><Avatar name={user.displayName} /><h2>{user.displayName}</h2></div>
        <dl className="detail-grid">
          <div><dt>Email address</dt><dd>{user.email}</dd></div>
          <div><dt>Role</dt><dd>{user.roles.includes('Admin') ? 'Administrator' : 'Member'}</dd></div>
        </dl>
        {error && <ErrorMessage error={error} />}
        <button type="button" className="button" onClick={logout} disabled={saving}><LogOut size={17} aria-hidden="true" />{saving ? 'Signing out…' : 'Sign out'}</button>
      </section>
    </>
  );
}
