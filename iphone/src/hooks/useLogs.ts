import { useEffect, useState } from 'react';
import { useServices } from '../services/servicesContext';
import type { LogEntry } from '../services/logStore';

/** Live log entries. */
export function useLogs(): LogEntry[] {
  const services = useServices();
  const [entries, setEntries] = useState<LogEntry[]>(services.logStore.entries());

  useEffect(() => {
    return services.logStore.subscribe(() => {
      setEntries(services.logStore.entries());
    });
  }, [services]);

  return entries;
}