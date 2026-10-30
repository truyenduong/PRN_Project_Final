import React, { useState, useEffect } from 'react';
import { SubtaskService } from '../services/subtaskService';
import type { Subtask, ProjectMember } from '../types';
import { X, Calendar, Loader2, AlertCircle, ListChecks, User, Clock, Flag, CheckCircle2 } from 'lucide-react';
import { useAuth } from '../context/AuthContext';

interface SubtaskFormModalProps {
  taskId: number;
  parentTaskAssigneeId?: number | null;
  subtask?: Subtask | null; // If provided, modal is in EDIT mode; if null/undefined, CREATE mode
  members?: ProjectMember[];
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (savedSubtask: Subtask) => void;
}

export const SubtaskFormModal: React.FC<SubtaskFormModalProps> = ({
  taskId,
  parentTaskAssigneeId,
  subtask,
  members = [],
  isOpen,
  onClose,
  onSuccess,
}) => {
  const { user, currentRole } = useAuth();
  const isEdit = Boolean(subtask);

  const isPM = Boolean(
    (user?.roleName || user?.role || '').toUpperCase() === 'PROJECT_MANAGER' ||
    (user?.roleName || user?.role || '').toUpperCase() === 'ADMIN' ||
    currentRole === 'PROJECT_MANAGER' ||
    currentRole === 'ADMIN'
  );

  const isAssignee = Boolean(
    user && (
      (parentTaskAssigneeId && parentTaskAssigneeId === user.id) ||
      (subtask && subtask.assigneeId === user.id) ||
      (subtask?.assigneeName && user.name && subtask.assigneeName.trim().toLowerCase() === user.name.trim().toLowerCase())
    )
  );

  const canChangeStatus = isPM || isAssignee;

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assigneeId, setAssigneeId] = useState<string>('');
  const [estimate, setEstimate] = useState<string>('');
  const [priority, setPriority] = useState<string>('MEDIUM');
  const [dueDate, setDueDate] = useState<string>('');
  const [isCompleted, setIsCompleted] = useState<boolean>(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      if (subtask) {
        setTitle(subtask.title || '');
        setDescription(subtask.description || '');
        setAssigneeId(subtask.assigneeId ? String(subtask.assigneeId) : '');
        setEstimate(subtask.estimate || '');
        setPriority(subtask.priority || 'MEDIUM');
        setDueDate(subtask.dueDate || '');
        setIsCompleted(Boolean(subtask.isCompleted));
      } else {
        setTitle('');
        setDescription('');
        setAssigneeId('');
        setEstimate('');
        setPriority('MEDIUM');
        setDueDate('');
        setIsCompleted(false);
      }
      setError(null);
    }
  }, [isOpen, subtask]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      setError('Please enter a subtask title');
      return;
    }

    setLoading(true);
    setError(null);

    const selectedMember = members.find((m) => String(m.userId) === assigneeId);

    try {
      if (isEdit && subtask) {
        const updated = await SubtaskService.updateSubtaskDetails(taskId, subtask.id, {
          title: title.trim(),
          description: description.trim() || undefined,
          assigneeId: assigneeId ? Number(assigneeId) : null,
          assigneeName: selectedMember ? (selectedMember.userName || `User #${selectedMember.userId}`) : undefined,
          estimate: estimate.trim() || undefined,
          priority,
          dueDate: dueDate || undefined,
          isCompleted,
        });
        if (updated) {
          onSuccess(updated);
        }
      } else {
        const created = await SubtaskService.addSubtask(taskId, {
          taskId,
          title: title.trim(),
          description: description.trim() || undefined,
          assigneeId: assigneeId ? Number(assigneeId) : null,
          assigneeName: selectedMember ? (selectedMember.userName || `User #${selectedMember.userId}`) : undefined,
          estimate: estimate.trim() || undefined,
          priority,
          dueDate: dueDate || undefined,
          isCompleted,
        });
        onSuccess(created);
      }
      onClose();
    } catch (err: any) {
      console.error('Error saving subtask:', err);
      setError(err.message || 'Could not save subtask. Please try again!');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-lg w-full p-6 sm:p-8 shadow-2xl relative border border-gray-100 max-h-[90vh] overflow-y-auto">
        <button
          onClick={onClose}
          disabled={loading}
          className="absolute top-6 right-6 text-gray-400 hover:text-gray-600 transition-colors cursor-pointer disabled:opacity-50"
        >
          <X size={20} />
        </button>

        <div className="flex items-center gap-3 mb-6">
          <div className="w-11 h-11 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
            <ListChecks size={22} />
          </div>
          <div>
            <h2 className="text-xl font-bold text-gray-900">
              {isEdit ? 'Edit Subtask' : 'Add New Subtask'}
            </h2>
            <p className="text-xs text-gray-500 mt-0.5">
              {isEdit ? `Update details for subtask #${subtask?.id}` : `Create checklist item for Task #${taskId}`}
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
              Subtask Title <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              required
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="e.g. Design database schema, Write integration tests..."
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
              placeholder="Describe specific scope or criteria for this subtask..."
              className="w-full px-4 py-2.5 bg-gray-50 border border-gray-200 rounded-xl text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800 resize-none"
              disabled={loading}
            />
          </div>

          {/* Assignee */}
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

          <div className="grid grid-cols-2 gap-3">
            {/* Estimate */}
            <div>
              <label className="block text-xs font-semibold text-gray-700 mb-1.5 flex items-center gap-1">
                <Clock size={13} className="text-gray-400" />
                <span>Estimate (EST)</span>
              </label>
              <input
                type="text"
                value={estimate}
                onChange={(e) => setEstimate(e.target.value)}
                placeholder="e.g. 2h, 4h, 1d, 3pts..."
                className="w-full px-3 py-2 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800"
                disabled={loading}
              />
            </div>

            {/* Priority */}
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
          </div>

          {/* Due date */}
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

          {/* Completion status (when editing) */}
          {isEdit && (
            <div className="pt-2">
              <label className={`flex items-center gap-2 text-xs font-medium text-gray-700 ${
                !canChangeStatus ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'
              }`}>
                <input
                  type="checkbox"
                  checked={isCompleted}
                  onChange={(e) => setIsCompleted(e.target.checked)}
                  className="w-4 h-4 text-emerald-600 rounded border-gray-300 focus:ring-emerald-500"
                  disabled={loading || !canChangeStatus}
                />
                <span className="flex items-center gap-1.5">
                  <CheckCircle2 size={14} className={isCompleted ? 'text-emerald-600' : 'text-gray-400'} />
                  <span>Mark this subtask as completed</span>
                </span>
              </label>
              {!canChangeStatus && (
                <span className="text-[10px] text-amber-600 block mt-1">
                  Only the PM or the assignee has permission to change status
                </span>
              )}
            </div>
          )}

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
              <span>{isEdit ? 'Update Subtask' : 'Create Subtask'}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
