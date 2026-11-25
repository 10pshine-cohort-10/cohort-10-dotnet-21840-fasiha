import { useCallback } from 'react';
import { CheckCircle2, CircleDashed, Clock3, Plus, Search } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { api } from './api.js';
import { Avatar, dateLabel, EmptyTasks, ErrorMessage, Loading, priorities, PriorityPill, statuses, StatusPill, useResource } from './common.jsx';

const cards = [
  { status: 'Pending', property: 'pending', Icon: CircleDashed },
  { status: 'InProgress', property: 'inProgress', Icon: Clock3 },
  { status: 'Completed', property: 'completed', Icon: CheckCircle2 },
];

function NewTaskLink() {
  return <Link to="/tasks/new" className="button primary"><Plus size={19} aria-hidden="true" />New task</Link>;
}

function TaskTable({ items }) {
  return (
    <div className="table-scroll" role="region" aria-label="Task table" tabIndex={0}>
      <table>
        <thead><tr><th scope="col">Task</th><th scope="col">Status</th><th scope="col">Priority</th><th scope="col">Due date</th><th scope="col">Assignee</th></tr></thead>
        <tbody>
          {items.map((task) => (
            <tr key={task.id}>
              <td><Link className="task-link" to={'/tasks/' + task.id}>{task.title}</Link>{task.category && <span className="task-category">{task.category}</span>}</td>
              <td><StatusPill value={task.status} /></td>
              <td><PriorityPill value={task.priority} /></td>
              <td>{task.dueDate ? <time dateTime={task.dueDate}>{dateLabel(task.dueDate)}</time> : '—'}</td>
              <td>{task.assignee ? <span className="person"><Avatar name={task.assignee.displayName} /><span>{task.assignee.displayName}</span></span> : 'Unassigned'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const loadDashboard = (signal) => Promise.all([api.taskCounts(signal), api.tasks({ pageSize: 5 }, signal)]);

export function Dashboard({ user }) {
  const data = useResource(loadDashboard, 'dashboard');
  const isAdmin = user.roles.includes('Admin');

  return (
    <>
      <header className="page-heading">
        <div><h1>Overview</h1><p>{isAdmin ? 'Track the status of tasks across all accounts.' : 'Track the tasks you created and the work assigned to you.'}</p></div>
        <NewTaskLink />
      </header>
      {data.state === 'loading' && <Loading />}
      {data.state === 'error' && <ErrorMessage error={data.error} retry={data.retry} />}
      {data.state === 'ready' && (
        <>
          <section className="stats" aria-label="Task counts">
            {cards.map(({ status, property, Icon }) => (
              <Link to={'/tasks?status=' + status} className="stat-card" key={status}>
                <span className={'stat-icon status-' + status}><Icon size={22} aria-hidden="true" /></span>
                <div><span>{statuses[status]}</span><strong>{data.data[0][property]}</strong></div>
              </Link>
            ))}
          </section>
          <section className="panel" aria-labelledby="recent-tasks">
            <div className="panel-heading"><h2 id="recent-tasks">Recent tasks</h2><Link className="text-link" to="/tasks">View all tasks</Link></div>
            {data.data[1].items.length ? <TaskTable items={data.data[1].items} /> : <EmptyTasks />}
          </section>
        </>
      )}
    </>
  );
}

export function TaskList() {
  const [params, setParams] = useSearchParams();
  const search = (params.get('search') || '').slice(0, 120);
  const category = (params.get('category') || '').slice(0, 60);
  const status = Object.hasOwn(statuses, params.get('status')) ? params.get('status') : '';
  const priority = Object.hasOwn(priorities, params.get('priority')) ? params.get('priority') : '';
  const rawPage = Number(params.get('page'));
  const page = Number.isInteger(rawPage) && rawPage >= 1 && rawPage <= 1000000 ? rawPage : 1;
  const filterKey = JSON.stringify([search, category, status, priority]);
  const filtered = Boolean(search || category || status || priority);
  const load = useCallback((signal) => api.tasks({ search, category, status, priority, page }, signal), [search, category, status, priority, page]);
  const result = useResource(load, filterKey + page);
  const clear = () => setParams({});

  function apply(event) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const next = new URLSearchParams();
    for (const [key, value] of data.entries()) {
      if (value.trim()) next.set(key, value.trim());
    }
    setParams(next);
  }

  function goTo(nextPage) {
    const next = new URLSearchParams(params);
    if (nextPage === 1) next.delete('page');
    else next.set('page', nextPage);
    setParams(next);
  }

  return (
    <>
      <header className="page-heading"><div><h1>Tasks</h1><p>Find tasks, update progress, and manage deadlines.</p></div><NewTaskLink /></header>
      <section className="panel task-list" aria-label="Task list">
        <form className="filters" onSubmit={apply} key={filterKey}>
          <label className="search-field">Search<span><Search size={17} aria-hidden="true" /><input type="search" name="search" placeholder="Search tasks" maxLength={120} defaultValue={search} /></span></label>
          <div className="field">
            <label htmlFor="filter-status">Status</label>
            <select id="filter-status" name="status" defaultValue={status}><option value="">All statuses</option>{Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
          </div>
          <div className="field">
            <label htmlFor="filter-priority">Priority</label>
            <select id="filter-priority" name="priority" defaultValue={priority}><option value="">All priorities</option>{Object.entries(priorities).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
          </div>
          <label>Category<input name="category" placeholder="All categories" maxLength={60} defaultValue={category} /></label>
          <button className="button">Apply filters</button>
          {filtered && <button className="button button-plain" type="button" onClick={clear}>Clear</button>}
        </form>
        {result.state === 'loading' && <Loading />}
        {result.state === 'error' && <div className="panel-inset"><ErrorMessage error={result.error} retry={result.retry} /></div>}
        {result.state === 'ready' && (
          <>
            {result.data.items.length ? <TaskTable items={result.data.items} /> : <EmptyTasks filtered={filtered} clear={clear} />}
            {(result.data.total > 0 || page > 1) && (
              <div className="pagination">
                <span>{result.data.total} {result.data.total === 1 ? 'task' : 'tasks'}</span>
                <div>
                  <button className="button button-small" type="button" disabled={page <= 1} onClick={() => goTo(page - 1)}>Previous</button>
                  <span>Page {page}</span>
                  <button className="button button-small" type="button" disabled={page * result.data.pageSize >= result.data.total} onClick={() => goTo(page + 1)}>Next</button>
                </div>
              </div>
            )}
          </>
        )}
      </section>
    </>
  );
}
