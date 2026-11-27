import { useEffect, useState } from 'react';
import { ClipboardList, LayoutGrid, UserRound } from 'lucide-react';
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { api } from './api.js';
import { Avatar, Brand, ErrorMessage, Loading, useResource } from './common.jsx';
import { AccountForm, Profile } from './Account.jsx';
import { Dashboard, TaskList } from './TaskList.jsx';
import { TaskDetails, TaskEditor } from './TaskEditor.jsx';
import { TaskUpdatesProvider } from './taskUpdates.jsx';

const sections = [
  { to: '/', label: 'Overview', Icon: LayoutGrid },
  { to: '/tasks', label: 'Tasks', Icon: ClipboardList },
  { to: '/profile', label: 'Profile', Icon: UserRound },
];

function Workspace({ user, onLogout }) {
  const { pathname } = useLocation();
  const role = user.roles.includes('Admin') ? 'Administrator' : 'User';

  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [pathname]);

  return (
    <div className="workspace">
      <aside className="sidebar">
        <Brand />
        <nav aria-label="Main navigation">
          {sections.map(({ to, label, Icon }) => (
            <NavLink end={to === '/'} to={to} key={to}>
              <Icon size={19} aria-hidden="true" /><span>{label}</span>
            </NavLink>
          ))}
        </nav>
        <NavLink to="/profile" className="sidebar-user" aria-label="Open account profile">
          <Avatar name={user.displayName} /><span><strong>{user.displayName}</strong><small>{role}</small></span>
        </NavLink>
      </aside>
      <main className="workspace-content" id="main-content">
        <Routes>
          <Route index element={<Dashboard user={user} />} />
          <Route path="/tasks" element={<TaskList />} />
          <Route path="/tasks/new" element={<TaskEditor key="new" user={user} />} />
          <Route path="/tasks/:id" element={<TaskDetails />} />
          <Route path="/tasks/:id/edit" element={<TaskEditor key="edit" user={user} editing />} />
          <Route path="/profile" element={<Profile user={user} onLogout={onLogout} />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

const loadProfile = async (signal) => {
  try {
    return await api.profile(signal);
  } catch (error) {
    if (error.status === 401) return null;
    throw error;
  }
};

export default function App() {
  const session = useResource(loadProfile, 'profile');
  const [userOverride, setUserOverride] = useState(undefined);
  const logout = () => setUserOverride(null);

  useEffect(() => {
    window.addEventListener('taskline-session-expired', logout);
    return () => window.removeEventListener('taskline-session-expired', logout);
  }, []);

  if (session.state === 'loading') return <main className="startup"><Brand /><Loading /></main>;
  if (session.state === 'error') return <main className="startup"><Brand /><ErrorMessage error={session.error} retry={session.retry} /></main>;

  const user = userOverride === undefined ? session.data : userOverride;
  if (user) {
    return (
      <TaskUpdatesProvider key={user.id}>
        <Workspace user={user} onLogout={logout} />
      </TaskUpdatesProvider>
    );
  }

  return (
    <Routes>
      <Route path="/login" element={<AccountForm key="login" onLogin={setUserOverride} />} />
      <Route path="/register" element={<AccountForm key="register" register onLogin={setUserOverride} />} />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}
