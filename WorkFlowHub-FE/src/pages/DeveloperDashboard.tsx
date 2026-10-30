import React, { useState } from 'react';
import { mockComments, mockAttachments } from '../data/mockData';
import { Plus, Download, Send } from 'lucide-react';

export const DeveloperDashboard: React.FC = () => {
  const [activeTab, setActiveTab] = useState('myTasks');
  const [comments, setComments] = useState(mockComments);
  const [newComment, setNewComment] = useState('');

  const sidebarItems = [
    { id: 'myTasks', label: 'My Tasks' },
    { id: 'sprintBoard', label: 'Sprint Board' },
    { id: 'comments', label: 'Comments' },
    { id: 'attachments', label: 'Attachments' },
    { id: 'techNotes', label: 'Technical Notes' },
  ];

  // Developer personal task cards (Image 7)
  const developerTasks = [
    {
      id: 1,
      category: 'Infrastructure',
      title: 'Connect Kafka for Notifications',
      deadline: 'Today',
      progress: 70,
      priority: 'High Priority',
      color: 'bg-emerald-500',
    },
    {
      id: 2,
      category: 'Performance',
      title: 'Benchmark API Performance',
      deadline: 'Thursday',
      progress: 40,
      priority: 'Medium Priority',
      color: 'bg-emerald-500',
    },
    {
      id: 3,
      category: 'Documentation',
      title: 'Write Public API Docs',
      deadline: 'Friday',
      progress: 10,
      priority: 'Low Priority',
      color: 'bg-emerald-500',
    },
  ];

  const handleAddComment = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newComment.trim()) return;

    setComments([
      ...comments,
      {
        id: Date.now(),
        userName: 'Me',
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        content: newComment,
        isSelf: true,
      },
    ]);
    setNewComment('');
  };

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
        {/* Left Sidebar */}
        <div className="lg:col-span-3">
          <div className="bg-white rounded-3xl p-5 border border-gray-100 shadow-xs space-y-1">
            <div className="text-[11px] font-bold text-gray-400 tracking-wider px-3 py-2 uppercase">
              Developer
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
            <h1 className="text-2xl font-bold text-gray-900 tracking-tight">My Tasks</h1>
            <p className="text-xs text-gray-500 mt-1">
              Demo interface for <strong className="font-semibold text-gray-700">Developer</strong> role — sample data preview.
            </p>
          </div>

          {/* Metric KPI Cards (4 cards) */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">3</div>
              <div className="text-[11px] text-gray-500 mt-1">Assigned Tasks</div>
            </div>
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">1</div>
              <div className="text-[11px] text-gray-500 mt-1">Due Today</div>
            </div>
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">5</div>
              <div className="text-[11px] text-gray-500 mt-1">New Comments</div>
            </div>
            <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
              <div className="text-2xl sm:text-3xl font-bold text-gray-900">2</div>
              <div className="text-[11px] text-gray-500 mt-1">Open PRs</div>
            </div>
          </div>

          {/* My Tasks Cards */}
          <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-5">
            <div className="flex items-center justify-between">
              <h3 className="text-xs font-bold text-gray-900 uppercase tracking-wide">My Tasks</h3>
              <button className="inline-flex items-center gap-1 px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-full text-xs font-semibold shadow-xs cursor-pointer">
                <Plus size={14} /> New Task
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {developerTasks.map((task) => (
                <div key={task.id} className="p-4 rounded-2xl border border-gray-100 bg-gray-50/50 hover:bg-white hover:border-gray-200 transition-all space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="px-2 py-0.5 text-[10px] rounded font-semibold bg-emerald-50 text-emerald-800 border border-emerald-100">
                      {task.category}
                    </span>
                    <span className="text-[11px] text-gray-400 font-medium">{task.deadline}</span>
                  </div>

                  <div className="text-xs font-bold text-gray-900 line-clamp-1">{task.title}</div>

                  <div>
                    <div className="flex justify-between items-center text-[10px] text-gray-500 mb-1">
                      <span>Progress</span>
                      <span className="font-semibold text-gray-700">{task.progress}%</span>
                    </div>
                    <div className="w-full h-1.5 bg-gray-200 rounded-full overflow-hidden">
                      <div
                        style={{ width: `${task.progress}%` }}
                        className="h-full bg-emerald-500 rounded-full"
                      ></div>
                    </div>
                  </div>

                  <div className="text-[10px] text-gray-500 pt-1 border-t border-gray-100">
                    {task.priority}
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Bottom Grid: Comments + Attachments */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Comments card */}
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs flex flex-col justify-between space-y-4">
              <div>
                <h3 className="text-xs font-bold text-gray-900 uppercase tracking-wide mb-4">
                  Comments — Connect Kafka for Notifications
                </h3>

                <div className="space-y-3">
                  {comments.map((c) => (
                    <div key={c.id} className="flex items-start gap-2.5 text-xs">
                      <span
                        className={`w-2 h-2 rounded-full mt-1.5 shrink-0 ${
                          c.isSelf ? 'bg-emerald-500' : 'bg-gray-300'
                        }`}
                      ></span>
                      <div className="space-y-0.5">
                        <div className="flex items-center gap-2">
                          <strong className="text-gray-900 font-semibold">{c.userName}</strong>
                          <span className="text-[10px] text-gray-400">{c.time}</span>
                        </div>
                        <p className="text-gray-600 leading-relaxed">{c.content}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Add comment input */}
              <form onSubmit={handleAddComment} className="pt-3 border-t border-gray-100 flex items-center gap-2">
                <input
                  type="text"
                  value={newComment}
                  onChange={(e) => setNewComment(e.target.value)}
                  placeholder="Write a comment..."
                  className="flex-1 px-3.5 py-2 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
                />
                <button
                  type="submit"
                  className="p-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl transition-colors cursor-pointer"
                >
                  <Send size={14} />
                </button>
              </form>
            </div>

            {/* Attachments card */}
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
              <h3 className="text-xs font-bold text-gray-900 uppercase tracking-wide">Attachments</h3>

              <div className="divide-y divide-gray-100">
                {mockAttachments.map((f) => (
                  <div key={f.id} className="py-3 flex items-center justify-between">
                    <div>
                      <div className="text-xs font-semibold text-gray-900">{f.fileName}</div>
                      <div className="text-[11px] text-gray-400 mt-0.5">
                        {f.fileSize} · {f.uploaderName}
                      </div>
                    </div>
                    <button className="px-3 py-1 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg text-xs font-medium flex items-center gap-1 transition-colors cursor-pointer">
                      <Download size={12} /> Download
                    </button>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
