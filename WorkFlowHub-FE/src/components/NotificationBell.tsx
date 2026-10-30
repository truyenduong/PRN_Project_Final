import React, { useState, useEffect, useRef } from 'react';
import { Bell, CheckCheck, Loader2, Sparkles, Inbox, ArrowUpRight } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { NotificationService } from '../services/api';
import type { AppNotification } from '../types';

function formatTimeAgo(dateStr?: string): string {
  if (!dateStr) return '';
  try {
    const date = new Date(dateStr);
    const now = new Date();
    const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffSec < 45) return 'Just now';
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin}m ago`;
    const diffHour = Math.floor(diffMin / 60);
    if (diffHour < 24) return `${diffHour}h ago`;
    const diffDay = Math.floor(diffHour / 24);
    if (diffDay < 7) return `${diffDay}d ago`;
    return date.toLocaleDateString();
  } catch {
    return dateStr;
  }
}

export const NotificationBell: React.FC = () => {
  const { user, isAuthenticated } = useAuth();
  const [isOpen, setIsOpen] = useState(false);
  const [notifications, setNotifications] = useState<AppNotification[]>([]);
  const [loading, setLoading] = useState(false);
  const [markingAllRead, setMarkingAllRead] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const fetchNotifications = async (showLoading = false) => {
    if (!isAuthenticated || !user?.id) return;
    if (showLoading) setLoading(true);
    try {
      const res = await NotificationService.getMyNotifications(user.id, 0, 25);
      if (res && Array.isArray(res.content)) {
        setNotifications(res.content);
      }
    } catch (err) {
      console.warn('Failed to load notifications:', err);
    } finally {
      if (showLoading) setLoading(false);
    }
  };

  // Initial fetch and periodic polling (every 25 seconds)
  useEffect(() => {
    if (!isAuthenticated || !user?.id) return;

    fetchNotifications();

    const interval = setInterval(() => {
      fetchNotifications();
    }, 25000);

    return () => clearInterval(interval);
  }, [isAuthenticated, user?.id]);

  // Refetch when popover is opened
  useEffect(() => {
    if (isOpen) {
      fetchNotifications();
    }
  }, [isOpen]);

  // Click outside listener
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const handleNotificationClick = async (notif: AppNotification) => {
    // 1. Mark as read on backend if not already read
    if (!notif.isRead) {
      try {
        await NotificationService.markAsRead(notif.id);
        setNotifications((prev) =>
          prev.map((item) => (item.id === notif.id ? { ...item, isRead: true } : item))
        );
      } catch (err) {
        console.warn('Failed to mark notification as read:', err);
      }
    }

    // 2. Extract taskId from notification type (e.g. TASK_ASSIGNED:12 or SUBTASK_ASSIGNED:12)
    let taskId: number | null = null;
    if (notif.type) {
      const match = notif.type.match(/:(\d+)$/);
      if (match && match[1]) {
        taskId = parseInt(match[1], 10);
      }
    }

    // 3. Close the dropdown
    setIsOpen(false);

    // 4. Dispatch navigation event if taskId exists
    if (taskId) {
      sessionStorage.setItem('workflowhub_pending_task_id', String(taskId));
      window.dispatchEvent(
        new CustomEvent('workflowhub:open-task', {
          detail: { taskId, notificationId: notif.id },
        })
      );
    }
  };

  const handleMarkAllAsRead = async () => {
    const unreadItems = notifications.filter((n) => !n.isRead);
    if (unreadItems.length === 0) return;

    try {
      setMarkingAllRead(true);
      await Promise.all(unreadItems.map((n) => NotificationService.markAsRead(n.id)));
      setNotifications((prev) => prev.map((item) => ({ ...item, isRead: true })));
    } catch (err) {
      console.warn('Failed to mark all notifications as read:', err);
    } finally {
      setMarkingAllRead(false);
    }
  };

  if (!isAuthenticated || !user) {
    return null;
  }

  return (
    <div className="relative" ref={containerRef}>
      {/* Bell Icon Trigger */}
      <button
        onClick={() => setIsOpen((prev) => !prev)}
        className={`relative p-2 rounded-full transition-all cursor-pointer ${
          isOpen
            ? 'bg-emerald-50 text-emerald-700'
            : 'text-gray-600 hover:text-gray-900 hover:bg-gray-100'
        }`}
        title="Notifications"
        aria-label="Notifications"
      >
        <Bell size={18} />
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 bg-red-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center ring-2 ring-white shadow-xs animate-in zoom-in">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {/* Popover Dropdown */}
      {isOpen && (
        <div className="absolute right-0 mt-2 w-80 sm:w-96 bg-white rounded-2xl shadow-xl border border-gray-100 py-2 z-50 animate-in fade-in slide-in-from-top-2 duration-150">
          {/* Header */}
          <div className="px-4 py-2.5 border-b border-gray-100 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="font-semibold text-xs text-gray-900">Notifications</span>
              {unreadCount > 0 ? (
                <span className="px-1.5 py-0.5 text-[10px] font-bold bg-emerald-100 text-emerald-800 rounded-full">
                  {unreadCount} new
                </span>
              ) : (
                <span className="px-1.5 py-0.5 text-[10px] font-medium bg-gray-100 text-gray-500 rounded-full">
                  All read
                </span>
              )}
            </div>

            {unreadCount > 0 && (
              <button
                onClick={handleMarkAllAsRead}
                disabled={markingAllRead}
                className="text-[11px] font-medium text-emerald-600 hover:text-emerald-700 flex items-center gap-1 cursor-pointer disabled:opacity-50"
              >
                {markingAllRead ? (
                  <Loader2 size={12} className="animate-spin" />
                ) : (
                  <CheckCheck size={13} />
                )}
                <span>Mark all as read</span>
              </button>
            )}
          </div>

          {/* Notification List */}
          <div className="max-h-88 overflow-y-auto divide-y divide-gray-50">
            {loading ? (
              <div className="py-8 flex flex-col items-center justify-center text-xs text-gray-400 gap-2">
                <Loader2 size={20} className="animate-spin text-emerald-600" />
                <span>Loading notifications...</span>
              </div>
            ) : notifications.length === 0 ? (
              <div className="py-8 text-center px-4">
                <div className="w-10 h-10 rounded-full bg-gray-50 text-gray-400 mx-auto flex items-center justify-center mb-2">
                  <Inbox size={20} />
                </div>
                <p className="text-xs font-semibold text-gray-700">No notifications yet</p>
                <p className="text-[11px] text-gray-400 mt-0.5">
                  When you are assigned tasks or subtasks, they will appear here.
                </p>
              </div>
            ) : (
              notifications.map((notif) => {
                const isTaskNotif = notif.type?.startsWith('TASK_ASSIGNED') || notif.type?.startsWith('SUBTASK_ASSIGNED');
                return (
                  <div
                    key={notif.id}
                    onClick={() => handleNotificationClick(notif)}
                    className={`px-4 py-3 transition-colors cursor-pointer group flex items-start gap-3 ${
                      !notif.isRead
                        ? 'bg-emerald-50/40 hover:bg-emerald-50/70'
                        : 'hover:bg-gray-50'
                    }`}
                  >
                    {/* Unread indicator / Icon */}
                    <div className="mt-0.5 shrink-0 relative">
                      <div
                        className={`w-8 h-8 rounded-xl flex items-center justify-center text-xs ${
                          !notif.isRead
                            ? 'bg-emerald-100 text-emerald-700 font-bold'
                            : 'bg-gray-100 text-gray-500'
                        }`}
                      >
                        <Sparkles size={15} />
                      </div>
                      {!notif.isRead && (
                        <span className="absolute -top-0.5 -right-0.5 w-2 h-2 rounded-full bg-emerald-600 ring-2 ring-white"></span>
                      )}
                    </div>

                    {/* Content */}
                    <div className="flex-1 min-w-0">
                      <p
                        className={`text-xs leading-snug break-words ${
                          !notif.isRead ? 'font-semibold text-gray-900' : 'text-gray-600'
                        }`}
                      >
                        {notif.content}
                      </p>
                      <div className="flex items-center justify-between mt-1 text-[11px] text-gray-400">
                        <span>{formatTimeAgo(notif.createdAt)}</span>
                        {isTaskNotif && (
                          <span className="text-emerald-600 opacity-0 group-hover:opacity-100 transition-opacity flex items-center gap-0.5 font-medium text-[10px]">
                            <span>View task</span>
                            <ArrowUpRight size={11} />
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}
    </div>
  );
};
