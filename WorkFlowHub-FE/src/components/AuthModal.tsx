import React, { useState, useRef, useEffect } from 'react';
import { AuthService } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { X, Lock, Mail, User as UserIcon, Loader2, AlertCircle, CheckCircle2 } from 'lucide-react';

interface AuthModalProps {
  isOpen: boolean;
  initialMode?: 'login' | 'register';
  onClose: () => void;
  onSuccess: () => void;
}

export const AuthModal: React.FC<AuthModalProps> = ({
  isOpen,
  initialMode = 'login',
  onClose,
  onSuccess,
}) => {
  const [mode, setMode] = useState<'login' | 'register'>(initialMode);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [googleStatusText, setGoogleStatusText] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [rememberMe, setRememberMe] = useState(false);

  const abortControllerRef = useRef<AbortController | null>(null);

  const { login, sessionExpiredMessage, clearSessionExpiredMessage } = useAuth();

  useEffect(() => {
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    };
  }, []);

  const handleClose = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    setGoogleLoading(false);
    setGoogleStatusText(null);
    onClose();
  };

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMessage(null);
    setLoading(true);

    try {
      if (mode === 'login') {
        const response = await AuthService.login({ email, password }, rememberMe);
        if (response && response.data) {
          login(response.data.accessToken, response.data.user, rememberMe);
          clearSessionExpiredMessage();
          onSuccess();
          onClose();
        }
      } else {
        await AuthService.register({ email, password, name });
        setSuccessMessage('Registration successful! You can now log in.');
        setMode('login');
      }
    } catch (err: any) {
      console.error('Auth request error:', err);
      setError(err.message || 'Operation failed. Please check your credentials!');
    } finally {
      setLoading(false);
    }
  };

  const triggerGooglePrompt = async () => {
    setError(null);
    setSuccessMessage(null);
    setGoogleLoading(true);
    setGoogleStatusText('Checking server connection...');

    const abortController = new AbortController();
    abortControllerRef.current = abortController;

    try {
      // 1. Fast check: is backend already awake?
      const isAlreadyAwake = await AuthService.checkHealth(3000, abortController.signal);

      if (isAlreadyAwake) {
        setGoogleStatusText('Redirecting to Google...');
        window.location.href = AuthService.getGoogleLoginUrl();
        return;
      }

      // 2. Slow check: Backend is sleeping on Render (Cold start)
      setGoogleStatusText('Server is waking up (may take 30-45s)...');

      const isAwake = await AuthService.waitForServerWakeUp(
        (elapsedSec) => {
          setGoogleStatusText(`Server is waking up (${elapsedSec}s)...`);
        },
        60,
        abortController.signal
      );

      if (abortController.signal.aborted) {
        return;
      }

      if (isAwake) {
        setGoogleStatusText('Server ready! Redirecting to Google...');
        setTimeout(() => {
          window.location.href = AuthService.getGoogleLoginUrl();
        }, 300);
      } else {
        setError('The server took too long to wake up. Please try again in a few moments.');
        setGoogleLoading(false);
        setGoogleStatusText(null);
      }
    } catch {
      if (!abortController.signal.aborted) {
        setError('Failed to reach backend server. Please try again.');
        setGoogleLoading(false);
        setGoogleStatusText(null);
      }
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
      <div className="bg-white rounded-3xl max-w-md w-full p-8 shadow-2xl relative border border-gray-100">
        <button
          onClick={handleClose}
          className="absolute top-5 right-5 text-gray-400 hover:text-gray-600 transition-colors cursor-pointer"
        >
          <X size={20} />
        </button>

        <div className="text-center mb-6">
          <div className="w-12 h-12 rounded-xl bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold text-xl mx-auto mb-3">
            W
          </div>
          <h2 className="text-2xl font-bold text-gray-900">
            {mode === 'login' ? 'Welcome Back' : 'Create an Account'}
          </h2>
          <p className="text-xs text-gray-500 mt-1">
            {mode === 'login'
              ? 'Log in to access your team workspace'
              : 'Experience streamlined sprint management in 2 minutes'}
          </p>
        </div>

        {sessionExpiredMessage && (
          <div className="mb-4 p-3 text-xs rounded-xl bg-amber-50 text-amber-800 border border-amber-200 flex items-start gap-2">
            <AlertCircle size={16} className="shrink-0 mt-0.5 text-amber-600" />
            <span>{sessionExpiredMessage}</span>
          </div>
        )}

        {error && (
          <div className="mb-4 p-3 text-xs rounded-xl bg-red-50 text-red-700 border border-red-200 flex items-start gap-2">
            <AlertCircle size={16} className="shrink-0 mt-0.5" />
            <span>{error}</span>
          </div>
        )}

        {successMessage && (
          <div className="mb-4 p-3 text-xs rounded-xl bg-emerald-50 text-emerald-800 border border-emerald-200 flex items-start gap-2">
            <CheckCircle2 size={16} className="shrink-0 mt-0.5" />
            <span>{successMessage}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          {mode === 'register' && (
            <div>
              <label className="block text-xs font-semibold text-gray-700 mb-1">Full Name</label>
              <div className="relative">
                <UserIcon className="absolute left-3.5 top-3 text-gray-400" size={18} />
                <input
                  type="text"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="John Doe"
                  className="w-full pl-10 pr-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all"
                />
              </div>
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1">Email Address</label>
            <div className="relative">
              <Mail className="absolute left-3.5 top-3 text-gray-400" size={18} />
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="name@workflowhub.vn"
                className="w-full pl-10 pr-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1">Password</label>
            <div className="relative">
              <Lock className="absolute left-3.5 top-3 text-gray-400" size={18} />
              <input
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full pl-10 pr-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all"
              />
            </div>
          </div>

          {mode === 'login' && (
            <div className="flex items-center justify-between text-xs pt-0.5">
              <label className="flex items-center gap-2 cursor-pointer text-gray-600 select-none hover:text-gray-900">
                <input
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  className="rounded border-gray-300 text-emerald-600 focus:ring-emerald-500 cursor-pointer w-3.5 h-3.5"
                />
                <span>Remember me</span>
              </label>
              <span className="text-[11px] text-gray-400" title="If unchecked, session is destroyed when browser or machine is closed">
                (Logout on browser close)
              </span>
            </div>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-60 text-white font-semibold text-xs rounded-xl transition-all shadow-sm flex items-center justify-center gap-2 cursor-pointer"
          >
            {loading ? <Loader2 size={16} className="animate-spin" /> : (mode === 'login' ? 'Log In' : 'Sign Up Now')}
          </button>
        </form>

        <div className="my-5 flex items-center gap-3">
          <div className="h-px bg-gray-200 flex-1"></div>
          <span className="text-[11px] text-gray-400">or</span>
          <div className="h-px bg-gray-200 flex-1"></div>
        </div>

        <button
          type="button"
          disabled={loading || googleLoading}
          onClick={triggerGooglePrompt}
          className="w-full py-2.5 bg-white border border-gray-200 hover:bg-gray-50 disabled:opacity-60 text-gray-700 font-semibold text-xs rounded-xl transition-all flex items-center justify-center gap-2 cursor-pointer shadow-2xs"
        >
          {googleLoading ? (
            <Loader2 size={16} className="animate-spin text-emerald-600" />
          ) : (
            <svg className="w-4 h-4" viewBox="0 0 24 24">
              <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
              <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
              <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z"/>
              <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z"/>
            </svg>
          )}
          {googleLoading ? (googleStatusText || 'Connecting...') : 'Sign in with Google'}
        </button>

        {googleLoading && (
          <div className="mt-3 p-3 bg-amber-50 border border-amber-200 rounded-xl flex items-start gap-2.5 text-left animate-in fade-in duration-200">
            <Loader2 size={16} className="animate-spin text-amber-600 mt-0.5 shrink-0" />
            <div className="text-xs text-amber-800">
              <p className="font-semibold">{googleStatusText || 'Connecting to server...'}</p>
              <p className="text-[11px] text-amber-600 mt-0.5">
                Loading server
              </p>
            </div>
          </div>
        )}

        <div className="mt-6 text-center text-xs text-gray-500">
          {mode === 'login' ? (
            <>
              Don't have an account?{' '}
              <button
                type="button"
                onClick={() => {
                  setError(null);
                  setSuccessMessage(null);
                  setMode('register');
                }}
                className="text-emerald-600 font-semibold hover:underline cursor-pointer"
              >
                Sign up now
              </button>
            </>
          ) : (
            <>
              Already have an account?{' '}
              <button
                type="button"
                onClick={() => {
                  setError(null);
                  setSuccessMessage(null);
                  setMode('login');
                }}
                className="text-emerald-600 font-semibold hover:underline cursor-pointer"
              >
                Log in
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
