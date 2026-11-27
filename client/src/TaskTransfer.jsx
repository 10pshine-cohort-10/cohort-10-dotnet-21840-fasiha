import { useRef, useState } from 'react';
import { Download, Upload } from 'lucide-react';
import { api } from './api.js';
import { ErrorMessage } from './common.jsx';

const maxBytes = 16 * 1024 * 1024;

export function TaskTransfer({ filters, onImported }) {
  const chooser = useRef(null);
  const [working, setWorking] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState('');

  async function transfer(operation) {
    setWorking(true);
    setError(null);
    setMessage('');
    try {
      await operation();
    } catch (failure) {
      setError(failure);
    } finally {
      setWorking(false);
    }
  }

  function exportFile() {
    return transfer(async () => {
      const file = await api.exportTasks(filters);
      const url = URL.createObjectURL(file);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'taskline-tasks.json';
      document.body.append(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
  }

  function importFile(event) {
    const file = event.currentTarget.files?.[0];
    event.currentTarget.value = '';
    if (!file) return;
    return transfer(async () => {
      if (file.size > maxBytes) throw new Error('Choose a JSON file that is 16 MB or smaller.');
      const result = await api.importTasks(await file.text());
      onImported();
      setMessage('Imported ' + result.imported + (result.imported === 1 ? ' task.' : ' tasks.'));
    });
  }

  return (
    <div className="task-transfer">
      <div className="transfer-actions">
        <button className="button" type="button" disabled={working} onClick={exportFile}>
          <Download size={17} aria-hidden="true" />Export JSON
        </button>
        <button className="button" type="button" disabled={working} onClick={() => chooser.current.click()}>
          <Upload size={17} aria-hidden="true" />Import JSON
        </button>
        <input ref={chooser} type="file" accept=".json,application/json" hidden onChange={importFile} />
      </div>
      {error && <ErrorMessage error={error} />}
      {message && <p className="success" role="status">{message}</p>}
    </div>
  );
}
