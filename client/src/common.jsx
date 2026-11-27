import { useEffect, useState } from 'react';
import { CheckCheck, CircleAlert, ClipboardList } from 'lucide-react';
import { Link } from 'react-router-dom';

export const statuses = { Pending: 'Pending', InProgress: 'In progress', Completed: 'Completed' };
export const priorities = { Low: 'Low', Medium: 'Medium', High: 'High' };

export function Brand() {
  return <div className="brand"><span className="brand-mark"><CheckCheck size={22} aria-hidden="true" /></span>Taskline</div>;
}

export function Avatar({ name }) {
  const initials = name.trim().split(/\s+/).slice(0, 2).map((part) => [...part][0]).join('').toUpperCase();
  return <span className="avatar" aria-hidden="true">{initials}</span>;
}

export function ErrorMessage({ error, retry }) {
  return (
    <div className="error" role="alert">
      <CircleAlert size={19} aria-hidden="true" />
      <span>{error.message}</span>
      {retry && <button type="button" className="button button-small" onClick={retry}>Retry</button>}
    </div>
  );
}

export function Loading() {
  return <div className="loading" role="status"><span aria-hidden="true" />Loading…</div>;
}

export function EmptyTasks({ filtered = false, clear }) {
  return (
    <div className="empty">
      <div className="empty-icon"><ClipboardList size={25} aria-hidden="true" /></div>
      <h3>{filtered ? 'No matching tasks' : 'No tasks yet'}</h3>
      <p>{filtered ? 'Try a different search or clear the filters.' : 'Create a task to start tracking your work.'}</p>
      {filtered
        ? <button type="button" className="button" onClick={clear}>Clear filters</button>
        : <Link to="/tasks/new" className="button">Create a task</Link>}
    </div>
  );
}

export function StatusPill({ value }) {
  return <span className={'pill status-' + value}><span aria-hidden="true" />{statuses[value]}</span>;
}

export function PriorityPill({ value }) {
  return <span className={'pill priority-' + value}>{priorities[value]}</span>;
}

export function dateLabel(value) {
  if (!value) return '—';
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value + 'T00:00:00'));
}

export function timestampLabel(value) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

export function useResource(load, key, revision = 0) {
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState({ key, attempt, state: 'loading' });
  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ key, attempt, state: 'ready', data });
      })
      .catch((error) => {
        if (!controller.signal.aborted) setResult({ key, attempt, state: 'error', error });
      });
    return () => controller.abort();
  }, [load, key, attempt, revision]);
  const current = result.key === key && result.attempt === attempt ? result : { state: 'loading' };
  return { ...current, retry: () => setAttempt((previous) => previous + 1) };
}
