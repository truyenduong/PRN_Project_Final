import React, { useState } from 'react';
import { mockActivityLogs, mockNotifications } from '../data/mockData';

export const ViewerDashboard: React.FC = () => {
  const [activeTab, setActiveTab] = useState('board');

  const sidebarItems = [
    { id: 'board', label: 'Task Board' },
    { id: 'activities', label: 'Activities' },
    { id: 'notifications', label: 'Notifications' },
  ];

  // Viewer sprint board tasks (Image 8)
  const todoList = ['Design Dashboard', 'Write API Docs', 'Optimize Queries'];
  const inProgressList = ['Connect Kafka for Notifications', 'Benchmark Performance'];
  const doneList = [
    'Configure CI/CD',
    'Migrate to PostgreSQL',
    'Fix Login Bug',
    'Write Security Tests',
  ];

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
        {/* Left Sidebar */}
        <div className="lg:col-span-3">
          <div className="bg-white rounded-3xl p-5 border border-gray-100 shadow-xs space-y-1">
            <div className="text-[11px] font-bold text-gray-400 tracking-wider px-3 py-2 uppercase">
              Viewer
            </div>
            {sidebarItems.map((item) => {
              const isActive = activeTab === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => setActiveTab(item.id)}
                  className={`w-full text-left px-3.5 py-2.5 rounded-xl text-xs font-medium transition-all flex items-center gap-2 cursor-pointer ${
                    isActive
                      ? 'bg-emerald-50 text-emerald-800 font-semibold'
                      : 'text-gray-600 hover:bg-gray-50'
                  }`}
                >
                  <span
                    className={`w-1.5 h-1.5 rounded-full ${
                      isActive ? 'bg-emerald-500' : 'bg-transparent'
                    }`}
                  ></span>
                  {item.label}
                </button>
              );
            })}
          </div>
        </div>

        {/* Main Content */}
        <div className="lg:col-span-9 space-y-6">
          {/* Header */}
          <div>
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Task Board</h1>
            <p className="text-xs text-gray-500 mt-1">
              Demo interface for <strong className="font-semibold text-gray-700">Viewer</strong> role — sample data preview.
            </p>
          </div>

          {/* Read-only Banner (Image 8) */}
          <div className="bg-emerald-50/60 border border-emerald-200/80 rounded-2xl p-4 flex items-center gap-2.5 text-xs text-emerald-900">
            <span className="w-2 h-2 rounded-full bg-emerald-600"></span>
            <div>
              <strong className="font-semibold">View-only mode.</strong> You can browse boards, track activities, and receive notifications — editing is restricted.
            </div>
          </div>

          {/* Kanban Board (Image 8) */}
          <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
            <div className="text-sm font-bold text-gray-900">Sprint Board — Week 32</div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {/* TO-DO Column */}
              <div className="bg-gray-50/70 p-4 rounded-2xl border border-gray-100 space-y-3">
                <div className="flex items-center justify-between text-xs font-semibold text-gray-500">
                  <span>TO-DO</span>
                  <span className="text-[11px] font-normal text-gray-400">3</span>
                </div>
                <div className="space-y-2">
                  {todoList.map((item, idx) => (
                    <div key={idx} className="bg-white p-3 rounded-xl border border-gray-100 text-xs font-medium text-gray-800 shadow-2xs">
                      {item}
                    </div>
                  ))}
                </div>
              </div>

              {/* IN PROGRESS Column */}
              <div className="bg-gray-50/70 p-4 rounded-2xl border border-gray-100 space-y-3">
                <div className="flex items-center justify-between text-xs font-semibold text-gray-700">
                  <span>IN PROGRESS</span>
                  <span className="text-[11px] font-normal text-gray-400">2</span>
                </div>
                <div className="space-y-2">
                  {inProgressList.map((item, idx) => (
                    <div key={idx} className="bg-white p-3 rounded-xl border border-emerald-100 text-xs font-medium text-gray-800 shadow-2xs">
                      {item}
                    </div>
                  ))}
                </div>
              </div>

              {/* DONE Column */}
              <div className="bg-gray-50/70 p-4 rounded-2xl border border-gray-100 space-y-3">
                <div className="flex items-center justify-between text-xs font-semibold text-gray-500">
                  <span>DONE</span>
                  <span className="text-[11px] font-normal text-gray-400">4</span>
                </div>
                <div className="space-y-2">
                  {doneList.map((item, idx) => (
                    <div key={idx} className="bg-white p-3 rounded-xl border border-gray-100 text-xs font-medium text-gray-800 shadow-2xs">
                      {item}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>

          {/* Bottom Grid: Recent Activities + Notifications */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Recent Activities */}
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
              <h3 className="text-xs font-bold text-gray-900 uppercase tracking-wide">Recent Activities</h3>

              <div className="space-y-3.5">
                {mockActivityLogs.map((log) => (
                  <div key={log.id} className="flex items-start gap-2.5 text-xs">
                    <span className="w-2 h-2 rounded-full bg-emerald-500 mt-1.5 shrink-0"></span>
                    <p className="text-gray-700">
                      <strong className="text-gray-900 font-semibold">{log.userName}</strong> {log.action} {log.target}
                    </p>
                  </div>
                ))}
              </div>
            </div>

            {/* Notifications */}
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4 flex flex-col justify-between">
              <div>
                <h3 className="text-xs font-bold text-gray-900 uppercase tracking-wide mb-3">Notifications</h3>

                <div className="space-y-2.5">
                  {mockNotifications.map((notif) => (
                    <div key={notif.id} className="p-2.5 rounded-xl hover:bg-gray-50 flex items-center justify-between transition-colors">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-full bg-gray-100 text-gray-600 flex items-center justify-center font-bold text-[10px]">
                          {notif.initials}
                        </div>
                        <span className="text-xs font-medium text-gray-800">{notif.title}</span>
                      </div>
                      <span className="text-[10px] text-gray-400">{notif.time}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="pt-3 border-t border-gray-100">
                <span className="inline-block px-3 py-1 bg-gray-100 text-gray-500 rounded-md text-[11px] font-medium">
                  You do not have edit permissions
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
