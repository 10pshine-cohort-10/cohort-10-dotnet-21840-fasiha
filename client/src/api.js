export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

async function request(path, options = {}) {
  const { method = 'GET', body, signal, requiresSession = true } = options;
  const headers = { Accept: 'application/json, application/problem+json' };
  if (method !== 'GET') {
    const { token } = await request('/account/csrf', { requiresSession: false, signal });
    headers['X-CSRF-TOKEN'] = token;
  }
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let response;
  try {
    response = await fetch('/api' + path, {
      method,
      headers,
      credentials: 'same-origin',
      signal,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError('Could not reach Taskline. Check your connection and try again.', 0);
  }

  if (response.status === 204 || response.status === 201 && path === '/account/register') return null;
  const isJson = response.headers.get('content-type')?.includes('json');
  const data = isJson ? await response.json() : null;
  if (!response.ok) {
    if (response.status === 401 && requiresSession) window.dispatchEvent(new Event('taskline-session-expired'));
    const validation = data?.errors && Object.values(data.errors).flat().join(' ');
    const message = validation || data?.title || 'The request could not be completed.';
    throw new ApiError(message, response.status);
  }
  return data;
}

function withQuery(path, values) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== '' && value != null) query.set(key, value);
  }
  return path + (query.size ? '?' + query : '');
}

export const api = {
  profile: (signal) => request('/account/me', { signal, requiresSession: false }),
  login: (body) => request('/account/login', { method: 'POST', body, requiresSession: false }),
  register: (body) => request('/account/register', { method: 'POST', body, requiresSession: false }),
  logout: () => request('/account/logout', { method: 'POST' }),
  assignees: (search, signal) => request(withQuery('/account/assignees', { search }), { signal }),
  taskCounts: (signal) => request('/tasks/counts', { signal }),
  tasks: (filter, signal) => request(withQuery('/tasks', filter), { signal }),
  task: (id, signal) => request('/tasks/' + encodeURIComponent(id), { signal }),
  createTask: (body) => request('/tasks', { method: 'POST', body }),
  updateTask: (id, body) => request('/tasks/' + encodeURIComponent(id), { method: 'PUT', body }),
  updateStatus: (id, body) => request('/tasks/' + encodeURIComponent(id) + '/status', { method: 'PATCH', body }),
  deleteTask: (id, version) => request(withQuery('/tasks/' + encodeURIComponent(id), { version }), { method: 'DELETE' }),
};
