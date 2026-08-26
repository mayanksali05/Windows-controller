const encoder = new TextEncoder();

/** UTF-8 encode a string. */
export function textToUtf8(value: string): Uint8Array {
  return encoder.encode(value);
}

/** Parse an ISO8601 timestamp to a Date, or undefined when invalid. */
export function parseIso(value: string | undefined): Date | undefined {
  if (!value) {
    return undefined;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date;
}

/** Format a Date as an ISO8601 string with the Z suffix (as .NET round-trip). */
export function toIso(date: Date): string {
  return date.toISOString();
}