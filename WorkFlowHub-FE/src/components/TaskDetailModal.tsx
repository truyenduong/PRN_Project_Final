import React, { useState, useEffect } from 'react';
import type { Task, Subtask, ProjectMember, Sprint } from '../types';
import { SubtaskService } from '../services/subtaskService';
import { useAuth } from '../context/AuthContext';
import { ErrorModal } from './ErrorModal';
import { 
  X, 
  Calendar, 
  CheckSquare, 
  ListChecks, 
  Edit3, 
  Trash2, 
  User, 
  Layers, 
  Flag, 
  Clock, 
  ArrowLeft, 
  CheckCircle2, 
  Plus, 
  FolderKanban
} from 'lucide-react';

interface TaskDetailModalProps {
  task?: Task | null;
  subtask?: Subtask | null;
  parentTask?: Task | null;
  members?: ProjectMember[];
  sprints?: Sprint[];
  isOpen: boolean;
  onClose: () => void;
  onEditTask?: (task: Task) => void;
  onEditSubtask?: (subtask: Subtask, parentTaskId: number) => void;
  onDeleteTask?: (taskId: number) => void;
  onDeleteSubtask?: (subtaskId: number | string, parentTaskId: number) => void;
  onSelectSubtask?: (subtask: Subtask, parentTask: Task) => void;
  onBackToParentTask?: (parentTask: Task) => void;
  onCreateSubtaskForTask?: (task: Task) => void;
}

export const TaskDetailModal: React.FC<TaskDetailModalProps> = ({
  task,
  subtask,
  parentTask,
  members = [],
  sprints = [],
  isOpen,
  onClose,
  onEditTask,
  onEditSubtask,
  onDeleteTask,
  onDeleteSubtask,
  onSelectSubtask,
  onBackToParentTask,
  onCreateSubtaskForTask,
}) => {
  const { user, currentRole } = useAuth();
  const isSubtaskMode = Boolean(subtask);
  const [subtasks, setSubtasks] = useState<Subtask[]>([]);
  const [loadingSubtasks, setLoadingSubtasks] = useState(false);

  // Pop-up Error Modal
  const [errorModalState, setErrorModalState] = useState<{
    isOpen: boolean;
    title?: string;
    message: string;
    code?: string;
  }>({
    isOpen: false,
    message: '',
  });

  const showErrorModal = (message: string, title: string = 'Error Notification', code?: string) => {
    setErrorModalState({
      isOpen: true,
      title,
      message,
      code,
    });
  };

  const isPM = Boolean(
    (user?.roleName || user?.role || '').toUpperCase() === 'PROJECT_MANAGER' ||
    (user?.roleName || user?.role || '').toUpperCase() === 'ADMIN' ||
    currentRole === 'PROJECT_MANAGER' ||
    currentRole === 'ADMIN'
  );

  const canToggleSubtask = (st: Subtask) => {
    if (isPM) return true;
    if (!user) return false;
    const isTaskAssignee = Boolean(
      task && (
        task.assigneeId === user.id ||
        (task.assigneeName && task.assigneeName.trim().toLowerCase() === user.name?.trim().toLowerCase())
      )
    );
    if (isTaskAssignee) return true;
    const isSubtaskAssignee = Boolean(
      st.assigneeId === user.id ||
      (st.assigneeName && user.name && st.assigneeName.trim().toLowerCase() === user.name.trim().toLowerCase())
    );
    return isSubtaskAssignee;
  };

  // Load subtasks if viewing main task
  useEffect(() => {
    if (isOpen && task && !subtask) {
      loadSubtasks(task.id);
    }
  }, [isOpen, task?.id, subtask]);

  const loadSubtasks = async (taskId: number) => {
    setLoadingSubtasks(true);
    try {
      const list = await SubtaskService.fetchSubtasks(taskId);
      setSubtasks(list);
    } finally {
      setLoadingSubtasks(false);
    }
  };

  const handleToggleSubtaskItem = async (stId: number | string) => {
    if (!task) return;
    const targetSt = subtasks.find((s) => s.id === stId);
    if (targetSt && !canToggleSubtask(targetSt)) {
      showErrorModal(
        'Only the Project Manager or the assignee has permission to toggle subtask status.',
        'Access Denied',
        'FORBIDDEN'
      );
      return;
    }
    const previousSubtasks = [...subtasks];
    setSubtasks((prev) =>
      prev.map((s) => (s.id === stId ? { ...s, isCompleted: !s.isCompleted } : s))
    );
    try {
      const updated = await SubtaskService.toggleSubtask(task.id, stId);
      setSubtasks(updated);
    } catch (err: any) {
      console.error('Error toggling subtask status:', err);
      setSubtasks(previousSubtasks);
      showErrorModal(
        err.message || 'Could not change subtask status.',
        'Access Denied',
        err.code || (err.status === 403 ? 'FORBIDDEN' : 'ERROR')
      );
    }
  };

  if (!isOpen || (!task && !subtask)) return null;

  // Resolve metadata
  const currentItem = subtask || task!;
  const title = currentItem.title;
  const description = currentItem.description;
  const priority = currentItem.priority || 'MEDIUM';
  const estimate = currentItem.estimate;
  const dueDate = currentItem.dueDate;
  const createdAt = currentItem.createdAt;

  // Assignee resolution
  let assigneeName = currentItem.assigneeName;
  if (!assigneeName && currentItem.assigneeId) {
    const found = members.find((m) => m.userId === currentItem.assigneeId);
    if (found) assigneeName = found.userName || `User #${found.userId}`;
  }

  // Sprint resolution (if main task)
  let sprintName = task?.sprintName;
  if (!sprintName && task?.sprintId) {
    const s = sprints.find((sp) => sp.id === task.sprintId);
    if (s) sprintName = s.name;
  }

  const completedSubtasksCount = subtasks.filter((s) => s.isCompleted).length;
  const subtasksProgress = subtasks.length > 0 ? Math.round((completedSubtasksCount / subtasks.length) * 100) : 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/45 backdrop-blur-sm animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-2xl w-full p-6 sm:p-8 shadow-2xl relative border border-gray-100 max-h-[92vh] flex flex-col overflow-hidden">
        {/* Top Header Bar */}
        <div className="flex items-center justify-between pb-4 border-b border-gray-100 shrink-0">
          <div className="flex items-center gap-2.5">
            {isSubtaskMode && parentTask && onBackToParentTask && (
              <button
                type="button"
                onClick={() => onBackToParentTask(parentTask)}
                className="p-1.5 text-gray-500 hover:text-emerald-700 hover:bg-emerald-50 rounded-xl transition-all cursor-pointer mr-1"
                title="Back to Parent Task"
              >
                <ArrowLeft size={18} />
              </button>
            )}

            <div className={`w-9 h-9 rounded-xl flex items-center justify-center ${
              isSubtaskMode ? 'bg-teal-50 text-teal-600' : 'bg-emerald-50 text-emerald-600'
            }`}>
              {isSubtaskMode ? <ListChecks size={20} /> : <CheckSquare size={20} />}
            </div>

            <div>
              <div className="flex items-center gap-2">
                <span className="text-[11px] font-mono font-bold px-2 py-0.5 rounded-md bg-gray-100 text-gray-700">
                  {isSubtaskMode ? `Subtask #${subtask?.id}` : `#${task?.id}${task?.taskCode ? ` (${task.taskCode})` : ''}`}
                </span>
                <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-md uppercase tracking-wider ${
                  isSubtaskMode ? 'bg-teal-100 text-teal-800' : 'bg-emerald-100 text-emerald-800'
                }`}>
                  {isSubtaskMode ? 'Subtask' : 'Main Task'}
                </span>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {/* Edit Button */}
            {isSubtaskMode && subtask && onEditSubtask && parentTask && (
              <button
                type="button"
                onClick={() => onEditSubtask(subtask, parentTask.id)}
                className="px-3 py-1.5 bg-gray-100 hover:bg-emerald-50 text-gray-700 hover:text-emerald-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer"
              >
                <Edit3 size={14} />
                <span>Edit</span>
              </button>
            )}

            {!isSubtaskMode && task && onEditTask && (
              <button
                type="button"
                onClick={() => onEditTask(task)}
                className="px-3 py-1.5 bg-gray-100 hover:bg-emerald-50 text-gray-700 hover:text-emerald-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer"
              >
                <Edit3 size={14} />
                <span>Edit</span>
              </button>
            )}

            {/* Delete Button */}
            {isSubtaskMode && subtask && onDeleteSubtask && parentTask && (
              <button
                type="button"
                onClick={() => {
                  if (confirm(`Are you sure you want to delete subtask "${subtask.title}"?`)) {
                    onDeleteSubtask(subtask.id, parentTask.id);
                    onClose();
                  }
                }}
                className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-all cursor-pointer"
                title="Delete subtask"
              >
                <Trash2 size={16} />
              </button>
            )}

            {!isSubtaskMode && task && onDeleteTask && (
              <button
                type="button"
                onClick={() => {
                  if (confirm(`Are you sure you want to delete task "${task.title}"?`)) {
                    onDeleteTask(task.id);
                    onClose();
                  }
                }}
                className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-xl transition-all cursor-pointer"
                title="Delete task"
              >
                <Trash2 size={16} />
              </button>
            )}

            {/* Close Button */}
            <button
              onClick={onClose}
              className="p-1.5 text-gray-400 hover:text-gray-700 hover:bg-gray-100 rounded-xl transition-colors cursor-pointer"
            >
              <X size={18} />
            </button>
          </div>
        </div>

        {/* Modal Scrollable Body */}
        <div className="overflow-y-auto flex-1 py-5 space-y-6 pr-1">
          {/* Parent Task Context Banner (if in subtask mode) */}
          {isSubtaskMode && parentTask && (
            <div className="p-3 bg-teal-50/70 border border-teal-100 rounded-2xl flex items-center justify-between text-xs text-teal-900">
              <div className="flex items-center gap-2 min-w-0">
                <span className="font-semibold text-teal-700 shrink-0">Part of Task:</span>
                <span className="truncate font-medium text-teal-950">
                  #{parentTask.id} - {parentTask.title}
                </span>
              </div>
              {onBackToParentTask && (
                <button
                  type="button"
                  onClick={() => onBackToParentTask(parentTask)}
                  className="px-2.5 py-1 bg-white hover:bg-teal-100 text-teal-800 rounded-lg font-semibold text-[11px] shrink-0 transition-all cursor-pointer shadow-2xs"
                >
                  View Parent Task
                </button>
              )}
            </div>
          )}

          {/* Title */}
          <div>
            <h1 className="text-xl sm:text-2xl font-bold text-gray-900 leading-snug">
              {title}
            </h1>
          </div>

          {/* Key Status & Priority Badges */}
          <div className="flex flex-wrap items-center gap-2.5">
            {/* Status */}
            {isSubtaskMode ? (
              <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-xl text-xs font-semibold ${
                subtask?.isCompleted
                  ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                  : 'bg-amber-50 text-amber-700 border border-amber-200'
              }`}>
                <CheckCircle2 size={14} />
                <span>{subtask?.isCompleted ? 'Completed' : 'In Progress'}</span>
              </span>
            ) : (
              <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-xl text-xs font-semibold ${
                task?.status === 'DONE'
                  ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                  : task?.status === 'IN_PROGRESS'
                  ? 'bg-blue-50 text-blue-700 border border-blue-200'
                  : task?.status === 'CLOSED'
                  ? 'bg-gray-100 text-gray-600 border border-gray-300'
                  : 'bg-amber-50 text-amber-700 border border-amber-200'
              }`}>
                <CheckSquare size={13} />
                <span>
                  {task?.status === 'DONE'
                    ? 'Done'
                    : task?.status === 'IN_PROGRESS'
                    ? 'In Progress'
                    : task?.status === 'CLOSED'
                    ? 'Closed'
                    : 'To Do'}
                </span>
              </span>
            )}

            {/* Priority */}
            <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-xl text-xs font-semibold ${
              priority === 'HIGH'
                ? 'bg-red-50 text-red-700 border border-red-200'
                : priority === 'MEDIUM'
                ? 'bg-amber-50 text-amber-700 border border-amber-200'
                : 'bg-gray-100 text-gray-600 border border-gray-200'
            }`}>
              <Flag size={13} />
              <span>
                {priority === 'HIGH'
                  ? 'High Priority'
                  : priority === 'MEDIUM'
                  ? 'Medium Priority'
                  : 'Low Priority'}
              </span>
            </span>

            {/* Estimate */}
            {estimate && (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-xl text-xs font-semibold bg-purple-50 text-purple-700 border border-purple-200">
                <Clock size={13} />
                <span>Estimate: {estimate}</span>
              </span>
            )}

            {/* Due date */}
            {dueDate && (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-xl text-xs font-semibold bg-gray-50 text-gray-700 border border-gray-200">
                <Calendar size={13} />
                <span>Due Date: {dueDate}</span>
              </span>
            )}
          </div>

          {/* Description Section */}
          <div className="space-y-2">
            <h3 className="text-xs font-bold text-gray-400 uppercase tracking-wider">
              Detailed Description
            </h3>
            <div className="p-4 bg-gray-50 rounded-2xl border border-gray-100 text-xs sm:text-sm text-gray-800 leading-relaxed whitespace-pre-wrap min-h-[70px]">
              {description ? description : <span className="text-gray-400 italic">No description provided for this item.</span>}
            </div>
          </div>

          {/* Two-Column Metadata Details */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
            {/* Assignee */}
            <div className="p-3.5 bg-gray-50/60 rounded-2xl border border-gray-100 space-y-1.5">
              <div className="text-[11px] font-semibold text-gray-400 flex items-center gap-1.5">
                <User size={13} className="text-emerald-600" />
                <span>Assignee</span>
              </div>
              <div className="text-xs font-bold text-gray-900 flex items-center gap-2">
                <div className="w-6 h-6 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center text-[10px] font-bold">
                  {(assigneeName || 'U').substring(0, 2).toUpperCase()}
                </div>
                <span>{assigneeName || 'Unassigned'}</span>
              </div>
            </div>

            {/* Sprint */}
            <div className="p-3.5 bg-gray-50/60 rounded-2xl border border-gray-100 space-y-1.5">
              <div className="text-[11px] font-semibold text-gray-400 flex items-center gap-1.5">
                <Layers size={13} className="text-emerald-600" />
                <span>Sprint Cycle</span>
              </div>
              <div className="text-xs font-bold text-gray-900 truncate">
                {sprintName || (task?.sprintId ? `Sprint #${task.sprintId}` : 'Not assigned to Sprint')}
              </div>
            </div>

            {/* Project */}
            {task?.projectName && (
              <div className="p-3.5 bg-gray-50/60 rounded-2xl border border-gray-100 space-y-1.5">
                <div className="text-[11px] font-semibold text-gray-400 flex items-center gap-1.5">
                  <FolderKanban size={13} className="text-emerald-600" />
                  <span>Project</span>
                </div>
                <div className="text-xs font-bold text-gray-900 truncate">
                  {task.projectName}
                </div>
              </div>
            )}

            {/* Created At */}
            {createdAt && (
              <div className="p-3.5 bg-gray-50/60 rounded-2xl border border-gray-100 space-y-1.5">
                <div className="text-[11px] font-semibold text-gray-400 flex items-center gap-1.5">
                  <Clock size={13} className="text-gray-400" />
                  <span>Created At</span>
                </div>
                <div className="text-xs font-medium text-gray-700">
                  {new Date(createdAt).toLocaleString('en-US')}
                </div>
              </div>
            )}
          </div>

          {/* Subtasks Section (Only shown when viewing a Task) */}
          {!isSubtaskMode && task && (
            <div className="space-y-3 pt-4 border-t border-gray-100">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <ListChecks size={16} className="text-emerald-600" />
                  <h3 className="text-xs font-bold text-gray-800 uppercase tracking-wider">
                    Subtasks
                  </h3>
                  <span className="text-xs text-gray-500 font-semibold">
                    ({completedSubtasksCount}/{subtasks.length})
                  </span>
                </div>

                {onCreateSubtaskForTask && (
                  <button
                    type="button"
                    onClick={() => onCreateSubtaskForTask(task)}
                    className="px-3 py-1.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-800 rounded-xl text-xs font-semibold flex items-center gap-1 transition-all cursor-pointer shadow-2xs"
                  >
                    <Plus size={14} />
                    <span>+ Add Subtask</span>
                  </button>
                )}
              </div>

              {/* Progress bar */}
              {subtasks.length > 0 && (
                <div className="w-full h-2 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-emerald-500 rounded-full transition-all duration-300"
                    style={{ width: `${subtasksProgress}%` }}
                  />
                </div>
              )}

              {/* List */}
              {loadingSubtasks ? (
                <div className="py-4 text-center text-xs text-gray-400">
                  Loading subtasks...
                </div>
              ) : subtasks.length === 0 ? (
                <div className="py-6 text-center text-xs text-gray-400 border border-dashed border-gray-200 rounded-2xl space-y-2">
                  <p>No subtasks found for this task.</p>
                  {onCreateSubtaskForTask && (
                    <button
                      type="button"
                      onClick={() => onCreateSubtaskForTask(task)}
                      className="text-emerald-700 hover:text-emerald-800 font-semibold cursor-pointer underline"
                    >
                      Click here to create the first subtask
                    </button>
                  )}
                </div>
              ) : (
                <div className="space-y-2">
                  {subtasks.map((st) => {
                    const canToggle = canToggleSubtask(st);
                    return (
                    <div
                      key={st.id}
                      className="p-3 bg-gray-50/70 hover:bg-emerald-50/40 rounded-2xl border border-gray-100 transition-all flex items-center justify-between gap-3 group"
                    >
                      <div className="flex items-center gap-3 min-w-0 flex-1">
                        <button
                          type="button"
                          disabled={!canToggle}
                          onClick={() => handleToggleSubtaskItem(st.id)}
                          title={!canToggle ? 'Only PM or assignee can change status' : undefined}
                          className={`w-5 h-5 rounded-lg border flex items-center justify-center transition-all shrink-0 ${
                            !canToggle ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'
                          } ${
                            st.isCompleted
                              ? 'bg-emerald-600 border-emerald-600 text-white'
                              : 'border-gray-300 hover:border-emerald-500 bg-white'
                          }`}
                        >
                          {st.isCompleted && <CheckCircle2 size={13} strokeWidth={3} />}
                        </button>

                        <div 
                          className="min-w-0 flex-1 cursor-pointer"
                          onClick={() => onSelectSubtask && onSelectSubtask(st, task)}
                        >
                          <div className="flex items-center gap-2">
                            <span className="text-[10px] font-mono font-semibold text-gray-500">
                              #{st.id}
                            </span>
                            <span className={`text-xs font-semibold transition-all truncate hover:text-emerald-700 ${
                              st.isCompleted ? 'text-gray-400 line-through' : 'text-gray-900'
                            }`}>
                              {st.title}
                            </span>
                          </div>

                          <div className="flex items-center gap-2 mt-0.5 text-[10px] text-gray-400">
                            {st.assigneeName && (
                              <span className="text-gray-600 font-medium bg-white px-1.5 py-0.5 rounded border border-gray-100">
                                👤 {st.assigneeName}
                              </span>
                            )}
                            {st.estimate && (
                              <span className="text-purple-700 font-medium bg-purple-50 px-1.5 py-0.5 rounded border border-purple-100">
                                ⏱️ {st.estimate}
                              </span>
                            )}
                            {st.dueDate && (
                              <span>📅 {st.dueDate}</span>
                            )}
                          </div>
                        </div>
                      </div>

                      <div className="flex items-center gap-1 shrink-0">
                        <button
                          type="button"
                          onClick={() => onSelectSubtask && onSelectSubtask(st, task)}
                          className="px-2.5 py-1 text-[11px] font-semibold text-emerald-700 hover:bg-emerald-100 rounded-lg transition-all cursor-pointer"
                        >
                          Details &rarr;
                        </button>
                      </div>
                    </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Modal Footer */}
        <div className="pt-4 border-t border-gray-100 flex items-center justify-between shrink-0">
          <div className="text-[11px] text-gray-400">
            {isSubtaskMode ? 'Subtask Details' : 'Task Details'}
          </div>
          <button
            type="button"
            onClick={onClose}
            className="px-5 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-xl text-xs font-semibold transition-all cursor-pointer"
          >
            Close
          </button>
        </div>
      </div>

      {/* Pop-up Error Modal */}
      <ErrorModal
        isOpen={errorModalState.isOpen}
        onClose={() => setErrorModalState((prev) => ({ ...prev, isOpen: false }))}
        title={errorModalState.title}
        message={errorModalState.message}
        code={errorModalState.code}
      />
    </div>
  );
};
