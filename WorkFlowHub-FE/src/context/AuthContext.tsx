import React, { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import type { User } from '../types';
import { AuthService } from '../services/api';
import { TokenStorage } from '../utils/tokenStorage';
import { useIdleTimeout } from '../hooks/useIdleTimeout';

export type RoleType = 'ADMIN' | 'USER' | 'PROJECT_MANAGER' | 'DEVELOPER' | 'VIEWER';

interface AuthContextType {
  user: User | null;
  currentRole: RoleType;
  setCurrentRole: (role: RoleType) => void;
  token: string | null;
  login: (token: string, user: User, rememberMe?: boolean) => void;
  logout: (reason?: string) => Promise<void>;
  isAuthenticated: boolean;
  isAdmin: boolean;
  sessionExpiredMessage: string | null;
  clearSessionExpiredMessage: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

// 15 minutes of inactivity before auto-logout
const IDLE_TIMEOUT_MS = 15 * 60 * 1000;

function parseInitialOAuthRedirect(): { token: string; user: User; refreshToken?: string } | null {
  if (typeof window === 'undefined') return null;
  try {
    const params = new URLSearchParams(window.location.search);
    const tokenParam = params.get('token');
    const userParam = params.get('user');
    const refreshTokenParam = params.get('refreshToken');

    if (tokenParam && userParam) {
      const parsedUser = JSON.parse(decodeURIComponent(userParam));
      // Store synchronously right now so any subsequent call to TokenStorage in this render or children has the token
      TokenStorage.saveAuth(tokenParam, parsedUser, refreshTokenParam, false);
      if (refreshTokenParam) {
        sessionStorage.setItem('workflowhub_refresh_token', refreshTokenParam);
      }
      // Clean query params from URL
      window.history.replaceState({}, document.title, window.location.pathname);
      return { token: tokenParam, user: parsedUser, refreshToken: refreshTokenParam || undefined };
    }
  } catch (err) {
    console.error('Failed to parse OAuth redirect params:', err);
  }
  return null;
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  // Synchronously parse and store OAuth parameters from Google redirect before initial state evaluation
  const [initialOAuth] = useState(() => parseInitialOAuthRedirect());

  const [token, setToken] = useState<string | null>(() => {
    if (initialOAuth?.token) return initialOAuth.token;
    return TokenStorage.getToken();
  });

  const [user, setUser] = useState<User | null>(() => {
    if (initialOAuth?.user) return initialOAuth.user;
    const validToken = TokenStorage.getToken();
    if (!validToken) return null;
    return TokenStorage.getUser<User>();
  });

  const [currentRole, setCurrentRole] = useState<RoleType>(() => {
    const activeUser = initialOAuth?.user || TokenStorage.getUser<User>();
    if (activeUser) {
      const rawRole = (activeUser.roleName || (activeUser as any).role || 'USER').toUpperCase();
      return rawRole === 'ADMIN' ? 'ADMIN' : 'USER';
    }
    const savedRole = TokenStorage.getRole();
    if (savedRole) {
      return savedRole as RoleType;
    }
    return 'USER';
  });

  const [sessionExpiredMessage, setSessionExpiredMessage] = useState<string | null>(null);

  const clearSessionExpiredMessage = useCallback(() => {
    setSessionExpiredMessage(null);
  }, []);

  const isLoggingOutRef = useRef(false);

  const logout = useCallback(async (reason?: string) => {
    if (isLoggingOutRef.current) return;
    isLoggingOutRef.current = true;
    try {
      await AuthService.logout();
    } catch (err) {
      console.warn('Logout API error:', err);
    } finally {
      TokenStorage.clear();
      setToken(null);
      setUser(null);
      setCurrentRole('USER');
      if (reason) {
        setSessionExpiredMessage(reason);
      }
      isLoggingOutRef.current = false;
    }
  }, []);

  // Idle Timeout: auto-logout after 15 minutes without user activity
  useIdleTimeout({
    enabled: !!token && !!user,
    timeoutMs: IDLE_TIMEOUT_MS,
    onIdle: () => {
      logout('Your session was automatically terminated due to 15 minutes of inactivity.');
    },
  });

  // Listen for 401 Unauthorized events from ApiClient interceptor
  useEffect(() => {
    const handleAuthExpired = (event: Event) => {
      const customEvent = event as CustomEvent<{ message?: string }>;
      const message = customEvent.detail?.message || 'Your session has expired. Please log in again.';
      logout(message);
    };

    window.addEventListener('workflowhub:auth_expired', handleAuthExpired);
    return () => {
      window.removeEventListener('workflowhub:auth_expired', handleAuthExpired);
    };
  }, [logout]);

  useEffect(() => {
    TokenStorage.saveRole(currentRole);
  }, [currentRole]);

  const login = (newToken: string, newUser: User, rememberMe = false) => {
    const rawRole = (newUser.roleName || (newUser as any).role || 'USER').toUpperCase();
    const normalizedRole: RoleType = rawRole === 'ADMIN' ? 'ADMIN' : 'USER';

    TokenStorage.saveAuth(newToken, newUser, null, rememberMe);

    setToken(newToken);
    setUser(newUser);
    setCurrentRole(normalizedRole);
    setSessionExpiredMessage(null);
  };

  const isAdmin = (user?.roleName || currentRole || '').toUpperCase() === 'ADMIN';

  return (
    <AuthContext.Provider
      value={{
        user,
        currentRole,
        setCurrentRole,
        token,
        login,
        logout,
        isAuthenticated: !!token && !!user,
        isAdmin,
        sessionExpiredMessage,
        clearSessionExpiredMessage,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
