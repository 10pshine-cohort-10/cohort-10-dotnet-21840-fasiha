import { createContext, useContext, useEffect, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const TaskRevision = createContext(0);

export function TaskUpdatesProvider({ children }) {
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    let disposed = false;
    let timer;
    const connection = new HubConnectionBuilder()
      .withUrl('/api/task-updates')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    const refresh = () => {
      if (!disposed) setRevision((previous) => previous + 1);
    };
    const scheduleStart = () => {
      if (!disposed) timer = window.setTimeout(start, 5000);
    };
    async function start() {
      try {
        await connection.start();
      } catch {
        scheduleStart();
      }
    }

    connection.on('TasksChanged', refresh);
    connection.onclose(scheduleStart);
    void start();

    return () => {
      disposed = true;
      window.clearTimeout(timer);
      void connection.stop();
    };
  }, []);

  return <TaskRevision.Provider value={revision}>{children}</TaskRevision.Provider>;
}

export function useTaskRevision() {
  return useContext(TaskRevision);
}
