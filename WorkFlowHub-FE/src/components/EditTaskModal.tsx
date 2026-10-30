import React, { useState, useEffect } from 'react';
import { TaskService } from '../services/api';
import type { Task, ProjectMember, Sprint, UpdateTaskPayload } from '../types';
import { X, Calendar, Loader2, AlertCircle, Edit3, User, Layers, Flag, Clock } from 'lucide-react';
import { useAuth } from '../context/AuthContext';

interface EditTaskModalProps {
  task: Task | null;
  members: ProjectMember[];
  sprints?: Sprint[];
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (updatedTask: Task) => void;
}

export const EditTaskModal: React.FC<EditTaskModalProps> = ({
  task,
  members,
  sprints = [],
  isOpen,
  onClose,
  onSuccess,
}) => {
  const { user, currentRole } = useAuth();

  const isPM = Boolean(
    (user?.roleName || user?.role || '').toUpperCase() === 'PROJECT_MANAGER' ||
    (user?.roleName || user?.role || '').toUpperCase() === 'ADMIN' ||
    currentRole === 'PROJECT_MANAGER' ||
    currentRole === 'ADMIN'
  );

  const isAssignee = Boolean(
    user && task && (
      task.assigneeId === user.id ||
      (task.assigneeName && task.assigneeName.trim().toLowerCase() === user.name?.trim().toLowerCase())
    )
  );

  const canChangeStatus = isPM || isAssignee;

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assigneeId, setAssigneeId] = useState<string>('');
  const [sprintId, setSprintId] = useState<string>('');
  const [priority, setPriority] = useState<string>('MEDIUM');
  const [status, setStatus] = useState<string>('TO_DO');
  const [dueDate, setDueDate] = useState<string>('');
  const [estimate, setEstimate] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen && task) {
      setTitle(task.title || '');
      setDescription(task.description || '');
      setAssigneeId(task.assigneeId ? String(task.assigneeId) : '');
      setSprintId(task.sprintId ? String(task.sprintId) : '');
      setPriority(task.priority || 'MEDIUM');
      setStatus(task.status || 'TO_DO');
      setDueDate(task.dueDate || '');
      setEstimate(task.estimate || '');
      setError(null);
    }
  }, [isOpen, task]);

  if (!isOpen || !task) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      setError('Please enter a task title');
      return;
    }

    setLoading(true);
    setError(null);

    const payload: UpdateTaskPayload = {
      projectId: task.projectId,
      sprintId: sprintId ? Number(sprintId) : null,
      assigneeId: assigneeId ? Number(assigneeId) : null,
      title: title.trim(),
      description: description.trim() || undefined,
      priority,
      status,
      dueDate: dueDate || undefined,
      estimate: estimate.trim() || undefined,
    };

    try {
      const updated = await TaskService.updateTask(task.id, payload as any);

      // Merge with member/sprint names for immediate UI sync
      const selectedMember = members.find((m) => String(m.userId) === assigneeId);
      const selectedSprint = sprints.find((s) => String(s.id) === sprintId);

      const mergedTask: Task = {
        ...task,
        ...updated,
        title: title.trim(),
        description: description.trim() || undefined,
        status,
        priority,
        dueDate: dueDate || undefined,
        estimate: estimate.trim() || undefined,
        assigneeId: assigneeId ? Number(assigneeId) : null,
        assigneeName: selectedMember ? (selectedMember.userName || `User #${selectedMember.userId}`) : undefined,
        sprintId: sprintId ? Number(sprintId) : null,
        sprintName: selectedSprint ? selectedSprint.name : undefined,
      };

      onSuccess(mergedTask);
      onClose();
    } catch (err: any) {
      console.error('Error updating task:', err);
      setError(err.message || 'Could not update task. Please try again!');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-lg w-full p-7 sm:p-8 shadow-2xl relative border border-gray-100 max-h-[90vh] overflow-y-auto">
        <button
          onClick={onClose}
          disabled={loading}
          className="absolute top-6 right-6 text-gray-400 hover:text-gray-600 transition-colors cursor-pointer disabled:opacity-50"
        >
          <X size={20} />
        </button>

        <div className="flex items-center gap-3 mb-6">
          <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center">
            <Edit3 size={24} />
          </div>
          <div>
            <h2 className="text-xl font-bold text-gray-900">Edit Task</h2>
            <p className="text-xs text-gray-500 mt-0.5">
              Update task details #{task.id} {task.taskCode ? `(${task.taskCode})` : ''}
            </p>
          </div>
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 text-red-600 text-xs rounded-xl flex items-center gap-2">
            <AlertCircle size={16} className="shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1.5">
              Task Title <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              required
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="e.g. Implement Payment Gateway API Integration..."
              className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800"
              disabled={loading}
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1.5">
              Detailed Description
            </label>
            <textarea
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Requirements, acceptance criteria, attachments or checklist..."
              className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 resize-none"
              disabled={loading}
            />
          </div>

          {/* Assignee Selection */}
          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1.5 flex items-center justify-between">
              <span className="flex items-center gap-1.5">
                <User size={13} className="text-emerald-600" />
                <span>Assignee</span>
              </span>
              <span className="text-[10px] text-gray-400 font-normal">
                (Project members)
              </span>
            </label>
            <select
              value={assigneeId}
              onChange={(e) => setAssigneeId(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 cursor-pointer"
              disabled={loading}
            >
              <option value="">-- Unassigned --</option>
              {members.map((member) => (
                <option key={member.userId} value={member.userId}>
                  {member.userName || `User #${member.userId}`} {member.userEmail ? `(${member.userEmail})` : ''} - [{member.roleName || 'Member'}]
                </option>
              ))}
            </select>
          </div>

          {/* Sprint Selection */}
          <div>
            <label className="block text-xs font-semibold text-gray-700 mb-1.5 flex items-center justify-between">
              <span className="flex items-center gap-1.5">
                <Layers size={13} className="text-emerald-600" />
                <span>Sprint (Cycle)</span>
              </span>
              <span className="text-[10px] text-gray-400 font-normal">
                (Optional)
              </span>
            </label>
            <select
              value={sprintId}
              onChange={(e) => setSprintId(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 cursor-pointer"
              disabled={loading}
            >
              <option value="">-- Unassigned (Product Backlog) --</option>
              {sprints.map((sprint) => (
                <option key={sprint.id} value={sprint.id}>
                  {sprint.name} {sprint.status === 'ACTIVE' ? '(Active)' : sprint.status === 'COMPLETED' ? '(Done)' : '(Planned)'}
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-gray-700 mb-1.5 flex items-center gap-1">
                <Flag size={13} className="text-gray-400" />
                <span>Priority</span>
              </label>
              <select
                value={priority}
                onChange={(e) => setPriority(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 cursor-pointer"
                disabled={loading}
              >
                <option value="LOW">Low</option>
                <option value="MEDIUM">Medium</option>
                <option value="HIGH">High</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-700 mb-1.5">
                Status
              </label>
              <select
                value={status}
                disabled={loading || !canChangeStatus}
                title={!canChangeStatus ? 'Only PM or assigned member has authority to change status' : undefined}
                onChange={(e) => setStatus(e.target.value)}
                className={`w-full px-3.5 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 ${
                  !canChangeStatus ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'
                }`}
              >
                <option value="TO_DO">To Do</option>
                <option value="IN_PROGRESS">In Progress</option>
                <option value="DONE">Done</option>
                <option value="CLOSED">Closed</option>
              </select>
              {!canChangeStatus && (
                <span className="text-[10px] text-amber-600 block mt-1">
                  Only the PM or the assignee has permission to change task status
                </span>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-gray-700 mb-1.5 flex items-center gap-1">
                <Clock size={13} className="text-gray-400" />
                <span>Estimate (EST)</span>
              </label>
              <input
                type="text"
                value={estimate}
                onChange={(e) => setEstimate(e.target.value)}
                placeholder="e.g. 4h, 2d, 5pts..."
                className="w-full px-3 py-2 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800"
                disabled={loading}
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-700 mb-1.5 flex items-center gap-1">
                <Calendar size={13} className="text-gray-400" />
                <span>Due Date</span>
              </label>
              <input
                type="date"
                value={dueDate}
                onChange={(e) => setDueDate(e.target.value)}
                className="w-full px-3 py-2 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800"
                disabled={loading}
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 pt-4 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={loading}
              className="px-4 py-2.5 text-xs font-semibold text-gray-600 hover:bg-gray-100 rounded-xl transition-all cursor-pointer disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-all cursor-pointer disabled:opacity-50"
            >
              {loading && <Loader2 size={15} className="animate-spin" />}
              <span>Save Changes</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
