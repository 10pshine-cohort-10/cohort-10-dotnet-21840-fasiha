const host = process.env.SONAR_HOST_URL?.replace(/\/+$/, '');
const token = process.env.SONAR_PROFILE_TOKEN || process.env.SONAR_TOKEN;
const project = process.env.SONAR_PROJECT_KEY;

if (!host || !token || !project) {
  throw new Error('Set SONAR_HOST_URL, SONAR_PROJECT_KEY, and SONAR_PROFILE_TOKEN before configuring quality profiles.');
}

async function sonar(action, parameters, method = 'GET') {
  const form = new URLSearchParams(parameters);
  const response = await fetch(host + '/api/' + action + (method === 'GET' ? '?' + form : ''), {
    method,
    headers: { Authorization: 'Bearer ' + token },
    body: method === 'POST' ? form : undefined,
    signal: AbortSignal.timeout(30_000),
  });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    const detail = error?.errors?.map((item) => item.msg).join(' ');
    throw new Error(action + ' failed (' + response.status + '). ' + (detail || response.statusText));
  }
  return response.status === 204 ? null : response.json();
}

const profiles = [
  { language: 'cs', name: 'Taskline C#', rule: 'csharpsquid:S3776' },
  { language: 'js', name: 'Taskline JavaScript', rule: 'javascript:S3776' },
];

for (const { language, name, rule } of profiles) {
  const { profiles: available } = await sonar('qualityprofiles/search', { language });
  const existing = available.find((profile) => profile.name === name);
  const profile = existing || (await sonar('qualityprofiles/create', { language, name }, 'POST')).profile;
  await sonar('qualityprofiles/change_parent', { language, qualityProfile: name, parentQualityProfile: 'Sonar way' }, 'POST');
  await sonar('qualityprofiles/activate_rule', { key: profile.key, rule, params: 'threshold=18' }, 'POST');
  await sonar('qualityprofiles/add_project', { language, qualityProfile: name, project }, 'POST');
  console.log('Assigned ' + name + ' with ' + rule + ' to ' + project);
}
