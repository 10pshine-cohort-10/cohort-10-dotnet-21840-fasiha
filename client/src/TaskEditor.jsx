import { useCallback, useState } from 'react';
import { Pencil, Trash2 } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from './api.js';
import { useTaskRevision } from './taskUpdates.jsx';
import { dateLabel, ErrorMessage, Loading, priorities, PriorityPill, statuses, StatusPill, timestampLabel, useResource } from './common.jsx';

function EnumOptions({ options }) {
  return Object.entries(options).map(([value, label]) => <option key={value} value={value}>{label}</option>);
}

function TaskDetailCard({ initialTask, reload }) {
  const navigate = useNavigate();
  const [task, setTask] = useState(initialTask);
  const [selectedStatus, setSelectedStatus] = useState(task.status);
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  async function changeStatus(event) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const updated = await api.updateStatus(task.id, { status: selectedStatus, version: task.version });
      setTask(updated);
      setSelectedStatus(updated.status);
    } catch (failure) {
      setError(failure);
    } finally {
      setSaving(false);
    }
  }

  async function remove() {
    if (!window.confirm('Remove this task from the workspace?')) return;
    setSaving(true);
    setError(null);
    try {
      await api.deleteTask(task.id, task.version);
      navigate('/tasks', { replace: true });
    } catch (failure) {
      setError(failure);
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <header className="page-heading">
        <div><Link to="/tasks" className="text-link breadcrumb">All tasks</Link><h1>{task.title}</h1></div>
        {task.canManage && <Link to={'/tasks/' + task.id + '/edit'} className="button primary"><Pencil size={17} aria-hidden="true" />Edit task</Link>}
      </header>
      <section className="panel detail-card" aria-labelledby="detail-title">
        <h2 id="detail-title">Task details</h2>
        <dl className="detail-grid">
          <div><dt>Status</dt><dd><StatusPill value={task.status} /></dd></div>
          <div><dt>Priority</dt><dd><PriorityPill value={task.priority} /></dd></div>
          <div><dt>Category</dt><dd>{task.category || 'Uncategorized'}</dd></div>
          <div><dt>Due date</dt><dd>{task.dueDate ? <time dateTime={task.dueDate}>{dateLabel(task.dueDate)}</time> : 'No due date'}</dd></div>
          <div><dt>Assignee</dt><dd>{task.assignee?.displayName || 'Unassigned'}</dd></div>
          <div><dt>Created by</dt><dd>{task.creator.displayName}</dd></div>
          <div><dt>Created</dt><dd><time dateTime={task.createdAt}>{timestampLabel(task.createdAt)}</time></dd></div>
          <div><dt>Last updated</dt><dd><time dateTime={task.updatedAt}>{timestampLabel(task.updatedAt)}</time></dd></div>
        </dl>
        <div className="description"><h3>Description</h3><p>{task.description || 'No description provided.'}</p></div>
        {error && <ErrorMessage error={error} retry={error.status === 409 ? reload : undefined} />}
        <div className="detail-actions">
          <form className="status-form" onSubmit={changeStatus}>
            <label htmlFor="task-status">Change status</label>
            <select id="task-status" value={selectedStatus} onChange={(event) => setSelectedStatus(event.target.value)} disabled={saving}><EnumOptions options={statuses} /></select>
            <button className="button" disabled={saving || selectedStatus === task.status}>Update status</button>
          </form>
          {task.canManage && <button className="button danger" type="button" onClick={remove} disabled={saving}><Trash2 size={17} aria-hidden="true" />Delete task</button>}
        </div>
      </section>
    </>
  );
}

export function TaskDetails() {
  const { id } = useParams();
  const load = useCallback((signal) => api.task(id, signal), [id]);
  const result = useResource(load, id, useTaskRevision());
  if (result.state === 'loading') return <Loading />;
  if (result.state === 'error') return <><Link to="/tasks" className="text-link breadcrumb">All tasks</Link><ErrorMessage error={result.error} retry={result.error.status === 404 ? undefined : result.retry} /></>;
  return <TaskDetailCard key={result.data.version} initialTask={result.data} reload={result.retry} />;
}

function AssigneeFields({ selection, onSelect }) {
  const [search, setSearch] = useState('');
  const term = search.trim();
  const load = useCallback((signal) => api.assignees(term, signal), [term]);
  const result = useResource(load, term);
  const results = result.state === 'ready' ? result.data : [];
  const options = selection && !results.some((person) => person.id === selection.id) ? [selection, ...results] : results;

  return (
    <div className="assignee-fields">
      <label>Find an account<input type="search" value={search} maxLength={80} placeholder="Search name or email" onChange={(event) => setSearch(event.target.value)} /></label>
      <div className="field">
        <label htmlFor="task-assignee">Assignee</label>
        <select id="task-assignee" value={selection?.id || ''} onChange={(event) => onSelect(options.find((person) => person.id === event.target.value) || null)}>
          <option value="">Unassigned</option>
          {options.map((person) => <option key={person.id} value={person.id}>{person.displayName}{person.email ? ' (' + person.email + ')' : ''}</option>)}
        </select>
      </div>
      {result.state === 'loading' && <small role="status">Finding accounts…</small>}
      {result.state === 'error' && <ErrorMessage error={result.error} retry={result.retry} />}
      {result.state === 'ready' && results.length === 0 && <small role="status">No accounts match this search.</small>}
    </div>
  );
}

function TaskForm({ task, user, reload }) {
  const isAdmin = user.roles.includes('Admin');
  const isEditing = Boolean(task);
  const navigate = useNavigate();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [assignee, setAssignee] = useState(task ? task.assignee : isAdmin ? null : user);
  const cancel = isEditing ? '/tasks/' + task.id : '/tasks';

  async function submit(event) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const title = data.get('title').trim();
    if (!title) {
      setError(new Error('Enter a task title.'));
      return;
    }

    setSaving(true);
    setError(null);
    const input = {
      title,
      description: data.get('description').trim() || null,
      category: data.get('category').trim() || null,
      status: data.get('status'),
      priority: data.get('priority'),
      dueDate: data.get('dueDate') || null,
      assigneeId: assignee?.id || null,
    };
    try {
      const saved = isEditing
        ? await api.updateTask(task.id, { ...input, version: task.version })
        : await api.createTask(input);
      navigate('/tasks/' + saved.id, { replace: true });
    } catch (failure) {
      setError(failure);
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <header className="page-heading">
        <div><Link to={cancel} className="text-link breadcrumb">{isEditing ? 'Task details' : 'All tasks'}</Link><h1>{isEditing ? 'Edit task' : 'New task'}</h1><p>Set the details, priority, and deadline for this task.</p></div>
      </header>
      <form className="panel editor fields" onSubmit={submit}>
        <label>Title<input name="title" required maxLength={120} defaultValue={task?.title} /></label>
        <label>Description<textarea name="description" rows={5} maxLength={2000} defaultValue={task?.description || ''} /></label>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="editor-status">Status</label>
            <select id="editor-status" name="status" defaultValue={task?.status || 'Pending'}><EnumOptions options={statuses} /></select>
          </div>
          <div className="field">
            <label htmlFor="editor-priority">Priority</label>
            <select id="editor-priority" name="priority" defaultValue={task?.priority || 'Medium'}><EnumOptions options={priorities} /></select>
          </div>
          <label>Category<input name="category" maxLength={60} defaultValue={task?.category || ''} placeholder="e.g. Planning" /></label>
          <label>Due date<input type="date" name="dueDate" min="0001-01-01" max="9999-12-31" defaultValue={task?.dueDate || ''} /></label>
        </div>
        {isAdmin
          ? <AssigneeFields selection={assignee} onSelect={setAssignee} />
          : <div className="readonly-field"><span>Assignee</span><strong>{assignee?.displayName || 'Unassigned'}</strong></div>}
        {error && <ErrorMessage error={error} retry={error.status === 409 ? reload : undefined} />}
        <div className="form-actions"><Link to={cancel} className="button">Cancel</Link><button className="button primary" disabled={saving}>{saving ? 'Saving…' : isEditing ? 'Save changes' : 'Create task'}</button></div>
      </form>
    </>
  );
}

function EditTask({ user }) {
  const { id } = useParams();
  const load = useCallback((signal) => api.task(id, signal), [id]);
  const result = useResource(load, id);

  if (result.state === 'loading') return <Loading />;
  if (result.state === 'error') return <><Link to="/tasks" className="text-link breadcrumb">All tasks</Link><ErrorMessage error={result.error} retry={result.error.status === 404 ? undefined : result.retry} /></>;
  if (!result.data.canManage) return <><Link to={'/tasks/' + id} className="text-link breadcrumb">Task details</Link><ErrorMessage error={new Error('Only the creator or an administrator can edit this task.')} /></>;
  return <TaskForm key={result.data.version} task={result.data} user={user} reload={result.retry} />;
}

export function TaskEditor({ user, editing }) {
  return editing ? <EditTask user={user} /> : <TaskForm user={user} />;
}
