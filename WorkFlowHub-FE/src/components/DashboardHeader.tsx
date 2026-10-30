import React, { useState } from 'react';
import { useAuth, type RoleType } from '../context/AuthContext';
import { ArrowLeft, Shield, Kanban, Code2, Eye, LogOut, Loader2, User as UserIcon } from 'lucide-react';
import { NotificationBell } from './NotificationBell';

interface DashboardHeaderProps {
  onBackHome: () => void;
}

export const DashboardHeader: React.FC<DashboardHeaderProps> = ({ onBackHome }) => {
  const { currentRole, setCurrentRole, user, logout, isAuthenticated } = useAuth();
  const [loggingOut, setLoggingOut] = useState(false);

  const roles: { id: RoleType; label: string; icon: React.ReactNode; badge: string }[] = [
    { id: 'ADMIN', label: 'Admin', icon: <Shield size={14} />, badge: 'ADMIN' },
    { id: 'USER', label: 'Member', icon: <UserIcon size={14} />, badge: 'USER' },
    { id: 'PROJECT_MANAGER', label: 'Project Manager', icon: <Kanban size={14} />, badge: 'PROJECT MANAGER' },
    { id: 'DEVELOPER', label: 'Developer', icon: <Code2 size={14} />, badge: 'DEVELOPER' },
    { id: 'VIEWER', label: 'Viewer', icon: <Eye size={14} />, badge: 'VIEWER' },
  ];

  // Effective role determination
  const effectiveRole: RoleType = isAuthenticated && user 
    ? (currentRole || ((user.roleName || user.role || 'USER').toUpperCase() as RoleType))
    : currentRole;

  const currentRoleObj = roles.find((r) => r.id === effectiveRole) || roles[1];

  const handleLogout = async () => {
    try {
      setLoggingOut(true);
      await logout();
    } catch (err) {
      console.error('Logout error:', err);
    } finally {
      setLoggingOut(false);
      onBackHome();
    }
  };

  return (
    <header className="sticky top-0 z-30 bg-white border-b border-gray-100 shadow-xs">
      <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
        {/* Brand & Active Role Badge */}
        <div className="flex items-center gap-3">
          <div 
            onClick={onBackHome}
            className="flex items-center gap-2 cursor-pointer group"
          >
            <div className="w-8 h-8 rounded-lg bg-emerald-600 flex items-center justify-center font-bold text-white shadow-xs">
              W
            </div>
            <span className="font-semibold text-lg text-gray-900 tracking-tight">WorkFlowHub</span>
          </div>
          <span className="px-2.5 py-0.5 text-[11px] font-semibold tracking-wider rounded-md bg-emerald-100 text-emerald-800 uppercase">
            {currentRoleObj.badge}
          </span>
        </div>

        {/* When not logged in (preview mode): display role switcher. When logged in: display active workspace */}
        {!isAuthenticated ? (
          <div className="flex items-center bg-gray-100/80 p-1 rounded-full border border-gray-200/60">
            {roles.map((r) => {
              const isActive = currentRole === r.id;
              return (
                <button
                  key={r.id}
                  onClick={() => setCurrentRole(r.id)}
                  className={`px-4 py-1.5 rounded-full text-xs font-medium transition-all flex items-center gap-1.5 cursor-pointer ${
                    isActive
                      ? 'bg-white text-gray-900 shadow-xs font-semibold'
                      : 'text-gray-600 hover:text-gray-900'
                  }`}
                >
                  {r.label}
                </button>
              );
            })}
          </div>
        ) : (
          <div className="hidden md:flex items-center gap-2 px-3.5 py-1.5 bg-gray-50 rounded-full border border-gray-200/60 text-xs text-gray-600">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
            <span>Workspace: <strong className="text-gray-900">{currentRoleObj.label}</strong></span>
          </div>
        )}

        {/* Actions: User Info, Notifications, Logout & Back to Home */}
        <div className="flex items-center gap-3 sm:gap-4">
          <NotificationBell />

          {user && (
            <div className="hidden sm:flex items-center gap-2 text-xs font-medium text-gray-700">
              <div className="w-7 h-7 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-[11px] overflow-hidden shrink-0">
                {user.avatar && (user.avatar.startsWith('http://') || user.avatar.startsWith('https://')) ? (
                  <img src={user.avatar} alt={user.name} className="w-full h-full object-cover" />
                ) : (
                  <span>{user.name?.substring(0, 2).toUpperCase() || 'U'}</span>
                )}
              </div>
              <span className="max-w-[120px] truncate">{user.name}</span>
            </div>
          )}

          <button
            onClick={onBackHome}
            className="text-xs font-medium text-gray-500 hover:text-emerald-700 flex items-center gap-1 transition-colors cursor-pointer"
          >
            <ArrowLeft size={14} /> Home
          </button>

          {/* Sign Out Button */}
          <button
            onClick={handleLogout}
            disabled={loggingOut}
            title="Sign Out"
            className="px-3 py-1.5 text-xs font-semibold text-red-600 hover:text-red-700 bg-red-50 hover:bg-red-100 rounded-xl transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
          >
            {loggingOut ? <Loader2 size={14} className="animate-spin" /> : <LogOut size={14} />}
            <span className="hidden sm:inline">Sign Out</span>
          </button>
        </div>
      </div>
    </header>
  );
};
