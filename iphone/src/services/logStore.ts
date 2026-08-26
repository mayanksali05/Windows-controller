export interface LogEntry {
  id: string;
  date: string;
  kind: string;
  message: string;
}

export interface LogStore {
  entries(): LogEntry[];
  add(kind: string, message: string): void;
  subscribe(listener: () => void): () => void;
}

let nextId = 0;

export function createLogStore(limit = 200): LogStore {
  let entries: LogEntry[] = [];
  const listeners = new Set<() => void>();

  function emit(): void {
    for (const listener of listeners) {
      listener();
    }
  }

  return {
    entries() {
      return entries;
    },
    add(kind, message) {
      entries = [
        { id: `${Date.now()}-${nextId++}`, date: new Date().toISOString(), kind, message },
        ...entries,
      ];
      if (entries.length > limit) {
        entries = entries.slice(0, limit);
      }
      emit();
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
}