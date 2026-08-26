/** Session-token expiry helpers. */

export function isExpired(expiresAt: number | null): boolean {
  return expiresAt === null || expiresAt <= Date.now();
}

/** True when the ISO8601 expiry (or the absence of one) means a refresh is due. */
export function shouldRefresh(expiresAtIso: string | undefined): boolean {
  if (!expiresAtIso) {
    return true;
  }
  const time = new Date(expiresAtIso).getTime();
  return Number.isNaN(time) || time <= Date.now();
}