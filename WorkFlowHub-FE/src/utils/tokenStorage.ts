/**
 * Token and Session Storage Management for WorkFlowHub.
 * Supports:
 * - sessionStorage (default, destroyed when browser/tab is closed or computer shut down)
 * - localStorage (only when user opts in to 'Remember Me')
 * - JWT expiration checking on load
 */

const TOKEN_KEY = 'workflowhub_token';
const REFRESH_TOKEN_KEY = 'workflowhub_refresh_token';
const USER_KEY = 'workflowhub_user';
const ROLE_KEY = 'workflowhub_role';
const REMEMBER_ME_KEY = 'workflowhub_remember_me';

/**
 * Check if a JWT token string has expired based on its 'exp' claim.
 */
export function isJwtExpired(token: string | null | undefined): boolean {
  if (!token) return true;
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return false;

    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    const parsed = JSON.parse(jsonPayload);
    if (typeof parsed.exp === 'number') {
      const nowInSeconds = Math.floor(Date.now() / 1000);
      return parsed.exp <= nowInSeconds;
    }
  } catch (e) {
    console.warn('Failed to parse JWT payload for expiration check:', e);
    return true;
  }
  return false;
}

export const TokenStorage = {
  /**
   * Check if 'Remember Me' was selected.
   */
  isRememberMe(): boolean {
    return localStorage.getItem(REMEMBER_ME_KEY) === 'true';
  },

  /**
   * Get active token from either sessionStorage or localStorage.
   * If token is expired, clears storage and returns null.
   */
  getToken(): string | null {
    // 1. Check sessionStorage first (current active tab/session)
    let token = sessionStorage.getItem(TOKEN_KEY);
    let fromLocalStorage = false;

    // 2. Fallback to localStorage if rememberMe was chosen
    if (!token) {
      token = localStorage.getItem(TOKEN_KEY);
      fromLocalStorage = true;
    }

    if (!token) return null;

    // 3. Check expiration
    if (isJwtExpired(token)) {
      this.clear();
      return null;
    }

    // If token exists in localStorage, sync to sessionStorage for consistent tab-level isolation
    if (fromLocalStorage && token) {
      const user = localStorage.getItem(USER_KEY);
      const role = localStorage.getItem(ROLE_KEY);
      const refresh = localStorage.getItem(REFRESH_TOKEN_KEY);
      sessionStorage.setItem(TOKEN_KEY, token);
      if (user) sessionStorage.setItem(USER_KEY, user);
      if (role) sessionStorage.setItem(ROLE_KEY, role);
      if (refresh) sessionStorage.setItem(REFRESH_TOKEN_KEY, refresh);
    }

    return token;
  },

  /**
   * Get parsed user object from active storage.
   */
  getUser<T = any>(): T | null {
    const raw = sessionStorage.getItem(USER_KEY) || localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as T;
    } catch {
      return null;
    }
  },

  /**
   * Get saved role from active storage.
   */
  getRole(): string | null {
    return sessionStorage.getItem(ROLE_KEY) || localStorage.getItem(ROLE_KEY);
  },

  /**
   * Get active refresh token.
   */
  getRefreshToken(): string | null {
    return sessionStorage.getItem(REFRESH_TOKEN_KEY) || localStorage.getItem(REFRESH_TOKEN_KEY);
  },

  /**
   * Save authentication payload.
   * If rememberMe is true, stores in localStorage.
   * If rememberMe is false, stores only in sessionStorage (automatically wiped on browser close/shutdown).
   */
  saveAuth(token: string, user: any, refreshToken?: string | null, rememberMe = false): void {
    // Clear old auth from both storages first
    this.clear();

    const roleName = (user?.roleName || user?.role?.name || user?.role || 'USER').toUpperCase();
    const normalizedRole = roleName === 'ADMIN' ? 'ADMIN' : 'USER';
    const userJson = JSON.stringify(user);

    // Always keep active in sessionStorage for current browser session
    sessionStorage.setItem(TOKEN_KEY, token);
    sessionStorage.setItem(USER_KEY, userJson);
    sessionStorage.setItem(ROLE_KEY, normalizedRole);
    if (refreshToken) {
      sessionStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    }

    // If Remember Me is chosen, also persist to localStorage
    if (rememberMe) {
      localStorage.setItem(TOKEN_KEY, token);
      localStorage.setItem(USER_KEY, userJson);
      localStorage.setItem(ROLE_KEY, normalizedRole);
      localStorage.setItem(REMEMBER_ME_KEY, 'true');
      if (refreshToken) {
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
      }
    } else {
      localStorage.removeItem(REMEMBER_ME_KEY);
    }
  },

  /**
   * Update active role.
   */
  saveRole(role: string): void {
    if (sessionStorage.getItem(TOKEN_KEY)) {
      sessionStorage.setItem(ROLE_KEY, role);
    }
    if (localStorage.getItem(TOKEN_KEY)) {
      localStorage.setItem(ROLE_KEY, role);
    }
  },

  /**
   * Wipe all authentication state from both sessionStorage and localStorage.
   */
  clear(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    sessionStorage.removeItem(ROLE_KEY);

    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(ROLE_KEY);
    localStorage.removeItem(REMEMBER_ME_KEY);
  },
};
