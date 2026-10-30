import React, { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { LogOut, Loader2 } from 'lucide-react';

interface NavbarProps {
  onNavigate: (page: string) => void;
  onOpenLogin: () => void;
  onOpenRegister: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({ onNavigate, onOpenLogin, onOpenRegister }) => {
  const { isAuthenticated, user, logout } = useAuth();
  const [loggingOut, setLoggingOut] = useState(false);

  const handleLogout = async () => {
    try {
      setLoggingOut(true);
      await logout();
      onNavigate('home');
    } catch (err) {
      console.error('Logout error:', err);
    } finally {
      setLoggingOut(false);
    }
  };

  return (
    <header className="sticky top-0 z-40 bg-white/80 backdrop-blur-md border-b border-gray-100">
      <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
        <div 
          className="flex items-center gap-2.5 cursor-pointer"
          onClick={() => onNavigate('home')}
        >
          <div className="w-8 h-8 rounded-lg bg-emerald-600 flex items-center justify-center font-bold text-white shadow-xs">
            W
          </div>
          <span className="font-semibold text-lg text-gray-900 tracking-tight">WorkFlowHub</span>
        </div>

        <nav className="hidden md:flex items-center gap-8 text-sm font-medium text-gray-600">
          <a href="#features" className="hover:text-emerald-600 transition-colors">Features</a>
          <a href="#roles" className="hover:text-emerald-600 transition-colors">Roles</a>
          <a href="#pricing" className="hover:text-emerald-600 transition-colors">Pricing</a>
          <a href="#tech" className="hover:text-emerald-600 transition-colors">Tech Stack</a>
        </nav>

        <div className="flex items-center gap-3">
          {isAuthenticated && user ? (
            <div className="flex items-center gap-3">
              <button
                onClick={() => onNavigate('dashboard')}
                className="px-4 py-2 text-sm font-medium text-emerald-700 bg-emerald-50 hover:bg-emerald-100 rounded-full transition-colors flex items-center gap-2 cursor-pointer"
              >
                <div className="w-5 h-5 rounded-full bg-emerald-600 text-white flex items-center justify-center text-[10px] font-bold overflow-hidden shrink-0">
                  {user.avatar && (user.avatar.startsWith('http://') || user.avatar.startsWith('https://')) ? (
                    <img src={user.avatar} alt={user.name} className="w-full h-full object-cover" />
                  ) : (
                    <span>{user.name?.substring(0, 2).toUpperCase() || 'U'}</span>
                  )}
                </div>
                <span>{user.name}</span>
              </button>
              <button
                onClick={handleLogout}
                disabled={loggingOut}
                title="Sign Out"
                className="px-3 py-1.5 text-xs font-semibold text-red-600 hover:text-red-700 bg-red-50 hover:bg-red-100 rounded-full transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
              >
                {loggingOut ? <Loader2 size={14} className="animate-spin" /> : <LogOut size={14} />}
                <span>Sign Out</span>
              </button>
            </div>
          ) : (
            <>
              <button
                onClick={onOpenLogin}
                className="px-4 py-2 text-sm font-medium text-gray-700 hover:text-emerald-600 transition-colors cursor-pointer"
              >
                Sign In
              </button>
              <button
                onClick={onOpenRegister}
                className="px-4 py-2 text-sm font-medium text-white bg-emerald-600 hover:bg-emerald-700 rounded-full shadow-xs hover:shadow transition-all cursor-pointer"
              >
                Get Started Free
              </button>
            </>
          )}
        </div>
      </div>
    </header>
  );
};
