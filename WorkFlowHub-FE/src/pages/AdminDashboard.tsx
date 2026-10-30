import React, { useState } from 'react';
import { mockUsers } from '../data/mockData';
import { Plus } from 'lucide-react';

export const AdminDashboard: React.FC = () => {
  const [activeTab, setActiveTab] = useState('overview');
  const [twoFactorAuth, setTwoFactorAuth] = useState(true);
  const [googleSSO, setGoogleSSO] = useState(false);
  const [periodicBackup, setPeriodicBackup] = useState(false);

  const sidebarItems = [
    { id: 'overview', label: 'Overview' },
    { id: 'members', label: 'Members' },
    { id: 'billing', label: 'Billing' },
    { id: 'security', label: 'Security' },
    { id: 'settings', label: 'System Settings' },
    { id: 'audit', label: 'Audit Logs' },
  ];

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
        {/* Left Sidebar */}
        <div className="lg:col-span-3">
          <div className="bg-white rounded-3xl p-5 border border-gray-100 shadow-xs space-y-1">
            <div className="text-[11px] font-bold text-gray-400 tracking-wider px-3 py-2 uppercase">
              Admin
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

        {/* Main Content Area */}
        <div className="lg:col-span-9 space-y-6">
          {/* Header Title */}
          <div>
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Overview</h1>
            <p className="text-xs text-gray-500 mt-1">
              Demo interface for <strong className="font-semibold text-gray-700">Admin</strong> role — sample data preview.
            </p>
          </div>

          {/* Metric KPI Cards (4 cards) */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">18</div>
              <div className="text-[11px] text-gray-500 mt-1">Active Members</div>
            </div>
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">6</div>
              <div className="text-[11px] text-gray-500 mt-1">Active Projects</div>
            </div>
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">3.58M VND</div>
              <div className="text-[11px] text-gray-500 mt-1">Current Month Plan Cost</div>
            </div>
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">2</div>
              <div className="text-[11px] text-gray-500 mt-1">Approval Requests</div>
            </div>
          </div>

          {/* Member List Card */}
          <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 className="text-sm font-bold text-gray-900">Members</h3>
                <p className="text-[11px] text-gray-500">Full members table: name, email, role, status.</p>
              </div>
              <button className="inline-flex items-center gap-1.5 px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-full text-xs font-semibold shadow-xs cursor-pointer">
                <Plus size={14} /> Invite Member
              </button>
            </div>

            <div className="divide-y divide-gray-100">
              {mockUsers.map((member) => (
                <div key={member.id} className="py-3 flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-9 h-9 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-xs overflow-hidden shrink-0">
                      {member.avatar && (member.avatar.startsWith('http://') || member.avatar.startsWith('https://')) ? (
                        <img src={member.avatar} alt={member.name} className="w-full h-full object-cover" />
                      ) : (
                        <span>{member.avatar || member.name.substring(0, 2).toUpperCase()}</span>
                      )}
                    </div>
                    <div>
                      <div className="text-xs font-semibold text-gray-900">{member.name}</div>
                      <div className="text-[11px] text-gray-400">{member.email}</div>
                    </div>
                  </div>

                  <div className="flex items-center gap-3">
                    <span className="px-2 py-0.5 text-[10px] font-semibold tracking-wider rounded bg-emerald-50 text-emerald-800 border border-emerald-100">
                      {member.role?.replace('_', ' ')}
                    </span>
                    <span
                      className={`px-2 py-0.5 text-[10px] rounded-full font-medium ${
                        member.status === 'active'
                          ? 'bg-emerald-100/70 text-emerald-800'
                          : 'bg-gray-100 text-gray-500'
                      }`}
                    >
                      {member.status === 'active' ? 'Active' : 'Pending Invite'}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Bottom Grid: Billing Card + Security & Settings Card */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Billing Card */}
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
              <h3 className="text-xs font-bold text-gray-900 uppercase tracking-wide">Billing</h3>
              <div className="flex items-baseline justify-between">
                <div>
                  <div className="text-sm font-bold text-gray-900">Pro Plan</div>
                  <div className="text-[11px] text-gray-500 mt-0.5">18 members × 199,000 VND / month</div>
                </div>
                <div className="text-lg font-bold text-gray-900">3.58M VND</div>
              </div>

              <div className="pt-2">
                <div className="flex justify-between text-[11px] text-gray-500 mb-1.5">
                  <span>Current Billing Cycle</span>
                  <span>12 days left</span>
                </div>
                <div className="w-full h-1.5 bg-gray-100 rounded-full overflow-hidden">
                  <div className="h-full bg-emerald-500 rounded-full w-2/5"></div>
                </div>
              </div>
            </div>

            {/* Security and Settings Card */}
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
              <h3 className="text-xs font-bold text-gray-900 uppercase tracking-wide">Security & Configuration</h3>

              <div className="space-y-3 divide-y divide-gray-100 text-xs">
                {/* 2FA Toggle */}
                <div className="flex items-center justify-between pt-1">
                  <div>
                    <div className="font-semibold text-gray-900">Enforce Two-Factor Authentication (2FA)</div>
                    <div className="text-[11px] text-gray-400">Enable for all organization members</div>
                  </div>
                  <button
                    onClick={() => setTwoFactorAuth(!twoFactorAuth)}
                    className={`w-10 h-5 rounded-full transition-colors relative cursor-pointer ${
                      twoFactorAuth ? 'bg-emerald-600' : 'bg-gray-200'
                    }`}
                  >
                    <span
                      className={`w-4 h-4 rounded-full bg-white absolute top-0.5 transition-transform ${
                        twoFactorAuth ? 'left-5.5' : 'left-0.5'
                      }`}
                    ></span>
                  </button>
                </div>

                {/* Google SSO */}
                <div className="flex items-center justify-between pt-3">
                  <div>
                    <div className="font-semibold text-gray-900">Sign in with Google</div>
                    <div className="text-[11px] text-gray-400">Allow Workspace SSO logins</div>
                  </div>
                  <button
                    onClick={() => setGoogleSSO(!googleSSO)}
                    className={`w-10 h-5 rounded-full transition-colors relative cursor-pointer ${
                      googleSSO ? 'bg-emerald-600' : 'bg-gray-200'
                    }`}
                  >
                    <span
                      className={`w-4 h-4 rounded-full bg-white absolute top-0.5 transition-transform ${
                        googleSSO ? 'left-5.5' : 'left-0.5'
                      }`}
                    ></span>
                  </button>
                </div>

                {/* Export Data */}
                <div className="flex items-center justify-between pt-3">
                  <div>
                    <div className="font-semibold text-gray-900">Automated Data Backups</div>
                    <div className="text-[11px] text-gray-400">Weekly cloud repository archive</div>
                  </div>
                  <button
                    onClick={() => setPeriodicBackup(!periodicBackup)}
                    className={`w-10 h-5 rounded-full transition-colors relative cursor-pointer ${
                      periodicBackup ? 'bg-emerald-600' : 'bg-gray-200'
                    }`}
                  >
                    <span
                      className={`w-4 h-4 rounded-full bg-white absolute top-0.5 transition-transform ${
                        periodicBackup ? 'left-5.5' : 'left-0.5'
                      }`}
                    ></span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
