import React, { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import { 
  CheckCircle2, 
  Clock, 
  FolderKanban, 
  ListTodo, 
  Plus, 
  Activity, 
  Search, 
  Check, 
  Calendar, 
  Layers, 
  Sparkles, 
  FolderPlus, 
  ArrowRight, 
  Loader2,
  ChevronDown,
  ChevronUp,
  ListChecks,
  Eye,
  Edit3,
  Trash2
} from 'lucide-react';
import { CreateProjectModal } from '../components/CreateProjectModal';
import { SubtaskList } from '../components/SubtaskList';
import { TaskDetailModal } from '../components/TaskDetailModal';
import { EditTaskModal } from '../components/EditTaskModal';
import { SubtaskFormModal } from '../components/SubtaskFormModal';
import { ErrorModal } from '../components/ErrorModal';
import { ProjectService, TaskService, ActivityLogService, ProjectMemberService, DashboardService, type UserDashboardData } from '../services/api';
import { SubtaskService } from '../services/subtaskService';
import type { Project, Task, ActivityLog, Subtask, ProjectMember } from '../types';

interface UserDashboardProps {
  onGoToProjectManager?: (projectId?: number) => void;
}

export const UserDashboard: React.FC<UserDashboardProps> = ({ onGoToProjectManager }) => {
  const { user, currentRole } = useAuth();
  const [activeTab, setActiveTab] = useState<'myTasks' | 'myProjects' | 'activities'>('myTasks');
  const [taskFilter, setTaskFilter] = useState<'ALL' | 'TO_DO' | 'IN_PROGRESS' | 'DONE' | 'CLOSED'>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [isCreateProjectOpen, setIsCreateProjectOpen] = useState(false);
  const [createdProjectNotification, setCreatedProjectNotification] = useState<string | null>(null);

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

  const showErrorModal = (message: string, title: string = 'Error Notice', code?: string) => {
    setErrorModalState({
      isOpen: true,
      title,
      message,
      code,
    });
  };

  // Personal task list (Real data from Backend API)
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loadingTasks, setLoadingTasks] = useState(false);
  const [newTaskTitle, setNewTaskTitle] = useState('');
  const [expandedTaskId, setExpandedTaskId] = useState<number | null>(null);

  // Projects list (Real data from backend)
  const [projectsList, setProjectsList] = useState<any[]>([]);
  const [loadingProjects, setLoadingProjects] = useState(false);

  // Recent activities (Real data from backend)
  const [activityLogs, setActivityLogs] = useState<ActivityLog[]>([]);
  const [loadingActivities, setLoadingActivities] = useState(false);

  // Cached Dashboard Metrics (From Redis via GET /api/v1/dashboard/user)
  const [dashboardStats, setDashboardStats] = useState<UserDashboardData | null>(null);

  // Modals: Detail, Edit Task, Subtask Form
  const [selectedDetailTask, setSelectedDetailTask] = useState<Task | null>(null);
  const [selectedDetailSubtask, setSelectedDetailSubtask] = useState<Subtask | null>(null);
  const [parentTaskForSubtask, setParentTaskForSubtask] = useState<Task | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);

  const [editingTask, setEditingTask] = useState<Task | null>(null);
  const [isEditTaskModalOpen, setIsEditTaskModalOpen] = useState(false);

  const [subtaskFormTaskId, setSubtaskFormTaskId] = useState<number | null>(null);
  const [editingSubtaskItem, setEditingSubtaskItem] = useState<Subtask | null>(null);
  const [isSubtaskFormOpen, setIsSubtaskFormOpen] = useState(false);

  const [projectMembers, setProjectMembers] = useState<Record<number, ProjectMember[]>>({});

  const loadMembersForProject = async (projectId: number) => {
    if (projectMembers[projectId]) return projectMembers[projectId];
    try {
      const list = await ProjectMemberService.getMembersByProjectId(projectId);
      setProjectMembers((prev) => ({ ...prev, [projectId]: list }));
      return list;
    } catch {
      return [];
    }
  };

  const handleOpenTaskDetail = (task: Task) => {
    if (task.projectId) {
      loadMembersForProject(task.projectId);
    }
    setSelectedDetailTask(task);
    setSelectedDetailSubtask(null);
    setParentTaskForSubtask(null);
    setIsDetailModalOpen(true);
  };

  const handleOpenSubtaskDetail = (st: Subtask, pTask: Task) => {
    if (pTask.projectId) {
      loadMembersForProject(pTask.projectId);
    }
    setSelectedDetailSubtask(st);
    setParentTaskForSubtask(pTask);
    setSelectedDetailTask(null);
    setIsDetailModalOpen(true);
  };

  const handleOpenEditTask = (task: Task) => {
    if (task.projectId) {
      loadMembersForProject(task.projectId);
    }
    setEditingTask(task);
    setIsEditTaskModalOpen(true);
  };

  const handleOpenCreateSubtask = (task: Task) => {
    if (task.projectId) {
      loadMembersForProject(task.projectId);
    }
    setSubtaskFormTaskId(task.id);
    setEditingSubtaskItem(null);
    setIsSubtaskFormOpen(true);
  };

  const handleOpenEditSubtask = (st: Subtask, parentTaskId: number) => {
    const pTask = tasks.find((t) => t.id === parentTaskId);
    if (pTask?.projectId) {
      loadMembersForProject(pTask.projectId);
    }
    setSubtaskFormTaskId(parentTaskId);
    setEditingSubtaskItem(st);
    setIsSubtaskFormOpen(true);
  };

  const handleDeleteTask = async (taskId: number) => {
    try {
      await TaskService.deleteTask(taskId);
      setTasks((prev) => prev.filter((t) => t.id !== taskId));
      setIsDetailModalOpen(false);
      loadDashboardStats();
    } catch (err) {
      console.warn('Error deleting task:', err);
    }
  };

  const handleDeleteSubtask = async (subtaskId: number | string, parentTaskId: number) => {
    await SubtaskService.deleteSubtask(parentTaskId, subtaskId);
    if (selectedDetailSubtask?.id === subtaskId) {
      if (parentTaskForSubtask) {
        handleOpenTaskDetail(parentTaskForSubtask);
      } else {
        setIsDetailModalOpen(false);
      }
    }
  };

  const handleTaskUpdated = (updatedTask: Task) => {
    setTasks((prev) => prev.map((t) => (t.id === updatedTask.id ? updatedTask : t)));
    if (selectedDetailTask?.id === updatedTask.id) {
      setSelectedDetailTask(updatedTask);
    }
    loadDashboardStats();
  };

  const [highlightedTaskId, setHighlightedTaskId] = useState<number | null>(null);

  const navigateAndOpenTask = async (taskId: number) => {
    setActiveTab('myTasks');
    setTaskFilter('ALL');
    setSearchQuery('');

    try {
      const task = await TaskService.getTaskById(taskId);
      if (task) {
        handleOpenTaskDetail(task);
        setHighlightedTaskId(taskId);
        setTasks((prev) => {
          if (!prev.some((t) => t.id === task.id)) {
            return [task, ...prev];
          }
          return prev.map((t) => (t.id === task.id ? task : t));
        });
        setTimeout(() => {
          const el = document.getElementById(`task-item-${taskId}`);
          if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
          }
        }, 300);
        setTimeout(() => {
          setHighlightedTaskId((prev) => (prev === taskId ? null : prev));
        }, 4000);
      }
    } catch (err) {
      console.warn('Failed to open task from notification:', err);
    }
  };

  useEffect(() => {
    const handleOpenTaskEvent = (e: Event) => {
      const customEvent = e as CustomEvent<{ taskId: number }>;
      if (customEvent.detail?.taskId) {
        navigateAndOpenTask(customEvent.detail.taskId);
      }
    };

    window.addEventListener('workflowhub:open-task', handleOpenTaskEvent);

    const pendingTaskIdStr = sessionStorage.getItem('workflowhub_pending_task_id');
    if (pendingTaskIdStr) {
      sessionStorage.removeItem('workflowhub_pending_task_id');
      const pendingTaskId = parseInt(pendingTaskIdStr, 10);
      if (!isNaN(pendingTaskId)) {
        setTimeout(() => {
          navigateAndOpenTask(pendingTaskId);
        }, 150);
      }
    }

    return () => {
      window.removeEventListener('workflowhub:open-task', handleOpenTaskEvent);
    };
  }, []);

  const sidebarItems = [
    { id: 'myTasks', label: 'My Tasks', icon: ListTodo },
    { id: 'myProjects', label: 'My Projects', icon: FolderKanban },
    { id: 'activities', label: 'Recent Activities', icon: Activity },
  ];

  // 1. Load projects list
  useEffect(() => {
    const fetchProjects = async () => {
      if (!user?.id) return;
      setLoadingProjects(true);
      try {
        const remoteProjects = await ProjectService.getMyProjects();
        if (remoteProjects) {
          const formatted = remoteProjects.map((p) => {
            const role = (p.currentUserRole || '').toUpperCase();
            const isOwner = p.ownerId === user?.id || role === 'PROJECT_MANAGER';
            const roleLabel = role === 'PROJECT_MANAGER' || isOwner
              ? 'Project Manager'
              : role === 'DEVELOPER'
              ? 'Developer'
              : role === 'TESTER'
              ? 'Tester'
              : role === 'VIEWER'
              ? 'Viewer'
              : p.currentUserRole || 'Member';

            return {
              id: p.id,
              name: p.name,
              description: p.description || 'No project description',
              membersCount: 1,
              progress: 0,
              activeSprint: 'No active sprint',
              roleInProject: roleLabel,
              currentUserRole: p.currentUserRole,
              ownerId: p.ownerId,
            };
          });
          setProjectsList(formatted);
        }
      } catch (err) {
        console.warn('Could not load remote projects:', err);
        setProjectsList([]);
      } finally {
        setLoadingProjects(false);
      }
    };
    fetchProjects();
  }, [user?.id]);

  // 2. Load user tasks from Backend API
  useEffect(() => {
    if (user?.id) {
      loadUserTasks();
    }
  }, [user?.id]);

  const loadUserTasks = async () => {
    if (!user?.id) return;
    setLoadingTasks(true);
    try {
      const myTasks = await TaskService.getMyTasks(user.id);
      if (myTasks) {
        const onlyMyTasks = myTasks.filter((t) => t.assigneeId === user.id);
        setTasks(onlyMyTasks);
      } else {
        setTasks([]);
      }
    } catch (err) {
      console.warn('Error loading my tasks from server:', err);
      setTasks([]);
    } finally {
      setLoadingTasks(false);
    }
  };

  // Load Cached Dashboard Metrics from Redis
  const loadDashboardStats = async () => {
    if (!user?.id) return;
    try {
      const stats = await DashboardService.getUserDashboard();
      if (stats) {
        setDashboardStats(stats);
      }
    } catch (err) {
      console.warn('Could not load cached dashboard metrics:', err);
    }
  };

  useEffect(() => {
    if (user?.id) {
      loadDashboardStats();
    }
  }, [user?.id]);

  // 3. Load recent activities
  useEffect(() => {
    if (activeTab === 'activities') {
      loadActivities();
    }
  }, [activeTab]);

  const loadActivities = async () => {
    setLoadingActivities(true);
    try {
      const logs = await ActivityLogService.getActivityLogs(0, 20);
      setActivityLogs(logs || []);
    } catch (err) {
      console.warn('Error loading activities from server:', err);
      setActivityLogs([]);
    } finally {
      setLoadingActivities(false);
    }
  };

  const handleProjectCreated = (newProject: Project) => {
    const projectItem = {
      id: newProject.id,
      name: newProject.name,
      description: newProject.description || 'No project description',
      membersCount: 1,
      progress: 0,
      activeSprint: 'No sprint started',
      roleInProject: 'Project Manager',
      currentUserRole: 'PROJECT_MANAGER',
      ownerId: newProject.ownerId,
    };
    setProjectsList((prev) => [projectItem, ...prev]);
    setCreatedProjectNotification(
      `Successfully created project "${newProject.name}"! You have been granted Project Manager permissions.`
    );
    if (onGoToProjectManager) {
      setTimeout(() => {
        onGoToProjectManager(newProject.id);
      }, 1200);
    }
  };

  // Chuẩn hóa trạng thái công việc (Hỗ trợ trạng thái CLOSED cho member)
  const normalizeStatus = (status?: string): 'TO_DO' | 'IN_PROGRESS' | 'DONE' | 'CLOSED' => {
    if (!status) return 'TO_DO';
    const s = status.trim().toUpperCase();
    if (s === 'CLOSED' || s === 'CLOSE' || s === 'ĐÃ ĐÓNG' || s === 'ĐÓNG') return 'CLOSED';
    if (s === 'DONE' || s === 'COMPLETED' || s === 'ĐÃ XONG' || s === 'HOÀN THÀNH') return 'DONE';
    if (s === 'IN_PROGRESS' || s === 'PROGRESS' || s === 'ĐANG LÀM') return 'IN_PROGRESS';
    return 'TO_DO';
  };

  const isPM = Boolean(
    (user?.roleName || user?.role || '').toUpperCase() === 'PROJECT_MANAGER' ||
    (user?.roleName || user?.role || '').toUpperCase() === 'ADMIN' ||
    currentRole === 'PROJECT_MANAGER' ||
    currentRole === 'ADMIN'
  );

  const canChangeTaskStatus = (task: Task) => {
    if (isPM) return true;
    if (!user) return false;
    return Boolean(
      task.assigneeId === user.id ||
      (task.assigneeName && task.assigneeName.trim().toLowerCase() === user.name?.trim().toLowerCase())
    );
  };

  const handleToggleTask = async (taskId: number) => {
    const currentTask = tasks.find((t) => t.id === taskId);
    if (!currentTask) return;

    if (!canChangeTaskStatus(currentTask)) {
      showErrorModal(
        'Only Project Managers or the assigned member can change task status.',
        'Access Denied',
        'FORBIDDEN'
      );
      return;
    }

    const isDone = normalizeStatus(currentTask.status) === 'DONE';
    const nextStatus = isDone ? 'IN_PROGRESS' : 'DONE';

    // Optimistic UI update
    setTasks((prev) =>
      prev.map((t) => {
        if (t.id === taskId) {
          return {
            ...t,
            status: nextStatus,
            progress: nextStatus === 'DONE' ? 100 : 50,
          };
        }
        return t;
      })
    );

    // Sync to backend API
    try {
      await TaskService.updateTask(taskId, { status: nextStatus });
      loadDashboardStats();
    } catch (err: any) {
      console.warn('Error updating task status on server:', err);
      // Revert back
      setTasks((prev) =>
        prev.map((t) => (t.id === taskId ? { ...t, status: currentTask.status, progress: currentTask.progress } : t))
      );
      showErrorModal(
        err.message || 'Unable to update task status.',
        'Access Denied',
        err.code || (err.status === 403 ? 'FORBIDDEN' : 'ERROR')
      );
    }
  };

  const handleStatusChange = async (taskId: number, newStatus: string) => {
    const currentTask = tasks.find((t) => t.id === taskId);
    if (!currentTask) return;

    if (!canChangeTaskStatus(currentTask)) {
      showErrorModal(
        'Only Project Managers or the assigned member can change task status.',
        'Access Denied',
        'FORBIDDEN'
      );
      return;
    }

    const previousStatus = currentTask.status;
    const previousProgress = currentTask.progress;

    setTasks((prev) =>
      prev.map((t) =>
        t.id === taskId
          ? {
              ...t,
              status: newStatus,
              progress: newStatus === 'DONE' || newStatus === 'CLOSED' ? 100 : 50,
            }
          : t
      )
    );
    try {
      await TaskService.updateTask(taskId, { status: newStatus });
      loadDashboardStats();
    } catch (err: any) {
      console.warn('Error updating status:', err);
      // Revert back
      setTasks((prev) =>
        prev.map((t) => (t.id === taskId ? { ...t, status: previousStatus, progress: previousProgress } : t))
      );
      showErrorModal(
        err.message || 'Unable to update task status.',
        'Access Denied',
        err.code || (err.status === 403 ? 'FORBIDDEN' : 'ERROR')
      );
    }
  };

  const handleCreateTask = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTaskTitle.trim()) return;

    const targetProject = projectsList[0];
    if (!targetProject) {
      alert('Please create or join a project before adding tasks.');
      setIsCreateProjectOpen(true);
      return;
    }

    try {
      const newTask = await TaskService.createTask({
        projectId: targetProject.id,
        assigneeId: user?.id,
        title: newTaskTitle.trim(),
        status: 'TO_DO',
        priority: 'MEDIUM',
      });
      setTasks((prev) => [newTask, ...prev]);
      setNewTaskTitle('');
      loadDashboardStats();
    } catch (err: any) {
      console.error('Error creating new task:', err);
      alert(err.message || 'Unable to create task. Please try again!');
    }
  };

  // Filter tasks by status and search query
  const filteredTasks = tasks.filter((t) => {
    const statusNorm = normalizeStatus(t.status);
    const matchFilter = taskFilter === 'ALL' || statusNorm === taskFilter;
    const query = searchQuery.toLowerCase().trim();
    const matchSearch =
      !query ||
      t.title.toLowerCase().includes(query) ||
      (t.category && t.category.toLowerCase().includes(query)) ||
      (t.description && t.description.toLowerCase().includes(query)) ||
      `#${t.id}`.includes(query) ||
      `task-${t.id}`.toLowerCase().includes(query) ||
      (t.taskCode && t.taskCode.toLowerCase().includes(query));
    return matchFilter && matchSearch;
  });

  const totalTasksDisplay = dashboardStats ? dashboardStats.totalAssignedTasks : tasks.length;
  const activeTasksCount =
    dashboardStats?.tasksByStatus?.['in_progress'] ??
    dashboardStats?.tasksByStatus?.['IN_PROGRESS'] ??
    tasks.filter((t) => normalizeStatus(t.status) === 'IN_PROGRESS').length;
  const todoTasksCount =
    dashboardStats?.tasksByStatus?.['pending'] ??
    dashboardStats?.tasksByStatus?.['TO_DO'] ??
    tasks.filter((t) => normalizeStatus(t.status) === 'TO_DO').length;
  const doneTasksCount =
    dashboardStats?.tasksByStatus?.['completed'] ??
    dashboardStats?.tasksByStatus?.['DONE'] ??
    tasks.filter((t) => normalizeStatus(t.status) === 'DONE').length;
  const totalProjectsDisplay = dashboardStats ? dashboardStats.totalProjects : projectsList.length;

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      {/* Welcome Banner */}
      <div className="bg-gradient-to-r from-emerald-700 via-emerald-600 to-teal-600 rounded-3xl p-6 sm:p-8 text-white mb-8 shadow-sm relative overflow-hidden">
        <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="space-y-2">
            <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white/15 backdrop-blur-md text-xs font-semibold">
              <Sparkles size={14} className="text-yellow-300" />
              <span>Member Workspace</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight">
              Welcome, {user?.name || 'Member'} 👋
            </h1>
            <p className="text-emerald-100 text-xs sm:text-sm max-w-xl">
              Dedicated workspace for team members. Track assigned tasks, update progress, and collaborate seamlessly.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3 shrink-0">
            <button
              onClick={() => setIsCreateProjectOpen(true)}
              className="px-4 py-2.5 bg-white text-emerald-800 hover:bg-emerald-50 rounded-2xl text-xs font-bold flex items-center gap-2 shadow-sm transition-all cursor-pointer"
            >
              <FolderPlus size={16} className="text-emerald-600" />
              <span>New Project</span>
            </button>
            <div className="bg-white/10 backdrop-blur-md px-4 py-2.5 rounded-2xl border border-white/20 text-center">
              <div className="text-[11px] text-emerald-100">System Role</div>
              <div className="text-sm font-bold text-white uppercase tracking-wider">
                {user?.roleName || 'USER'}
              </div>
            </div>
            <div className="bg-white/10 backdrop-blur-md px-4 py-2.5 rounded-2xl border border-white/20 text-center">
              <div className="text-[11px] text-emerald-100">Joined Projects</div>
              <div className="text-sm font-bold text-white">{totalProjectsDisplay}</div>
            </div>
          </div>
        </div>
      </div>

      {createdProjectNotification && (
        <div className="mb-8 p-4 bg-emerald-50 border border-emerald-200 rounded-2xl flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-emerald-900 text-xs shadow-xs animate-in fade-in">
          <div className="flex items-center gap-2.5">
            <Sparkles size={18} className="text-emerald-600 shrink-0" />
            <span className="font-semibold">{createdProjectNotification}</span>
          </div>
          {onGoToProjectManager && (
            <button
              onClick={() => onGoToProjectManager()}
              className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center justify-center gap-1.5 transition-all cursor-pointer shadow-xs shrink-0"
            >
              <span>Go to Project Manager</span>
              <ArrowRight size={14} />
            </button>
          )}
        </div>
      )}

      {/* Metric KPI Cards (Calculated directly from live data) */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs flex items-center justify-between">
          <div>
            <div className="text-2xl sm:text-3xl font-bold text-gray-900">{totalTasksDisplay}</div>
            <div className="text-[11px] text-gray-500 mt-1 font-medium">Total Tasks</div>
          </div>
          <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center">
            <ListTodo size={20} />
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs flex items-center justify-between">
          <div>
            <div className="text-2xl sm:text-3xl font-bold text-amber-600">{activeTasksCount}</div>
            <div className="text-[11px] text-gray-500 mt-1 font-medium">In Progress</div>
          </div>
          <div className="w-10 h-10 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center">
            <Clock size={20} />
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs flex items-center justify-between">
          <div>
            <div className="text-2xl sm:text-3xl font-bold text-emerald-600">{doneTasksCount}</div>
            <div className="text-[11px] text-gray-500 mt-1 font-medium">Completed</div>
          </div>
          <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
            <CheckCircle2 size={20} />
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs flex items-center justify-between">
          <div>
            <div className="text-2xl sm:text-3xl font-bold text-purple-600">{todoTasksCount}</div>
            <div className="text-[11px] text-gray-500 mt-1 font-medium">To Do</div>
          </div>
          <div className="w-10 h-10 rounded-xl bg-purple-50 text-purple-600 flex items-center justify-center">
            <Layers size={20} />
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
        {/* Left Sidebar */}
        <div className="lg:col-span-3 space-y-4">
          <div className="bg-white rounded-3xl p-5 border border-gray-100 shadow-xs space-y-1">
            <div className="text-[11px] font-bold text-gray-400 tracking-wider px-3 py-2 uppercase">
              Navigation
            </div>
            {sidebarItems.map((item) => {
              const isActive = activeTab === item.id;
              const Icon = item.icon;
              return (
                <button
                  key={item.id}
                  onClick={() => setActiveTab(item.id as any)}
                  className={`w-full text-left px-3.5 py-2.5 rounded-xl text-xs font-medium transition-all flex items-center gap-2.5 cursor-pointer ${
                    isActive
                      ? 'bg-emerald-50 text-emerald-800 font-semibold'
                      : 'text-gray-600 hover:bg-gray-50'
                  }`}
                >
                  <Icon size={16} className={isActive ? 'text-emerald-600' : 'text-gray-400'} />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </div>

          {/* User Profile Card Summary */}
          <div className="bg-white rounded-3xl p-5 border border-gray-100 shadow-xs space-y-3">
            <div className="text-[11px] font-bold text-gray-400 tracking-wider uppercase">
              Your Account
            </div>
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-sm overflow-hidden shrink-0">
                {user?.avatar && (user.avatar.startsWith('http://') || user.avatar.startsWith('https://')) ? (
                  <img src={user.avatar} alt={user.name || 'User'} className="w-full h-full object-cover" />
                ) : (
                  <span>{user?.name?.substring(0, 2).toUpperCase() || 'U'}</span>
                )}
              </div>
              <div className="min-w-0 flex-1">
                <div className="text-xs font-bold text-gray-900 truncate">{user?.name}</div>
                <div className="text-[11px] text-gray-400 truncate">{user?.email}</div>
              </div>
            </div>
            <div className="pt-2 border-t border-gray-100 text-[11px] text-gray-500 flex justify-between">
              <span>Status:</span>
              <span className="font-semibold text-emerald-600">Active</span>
            </div>
          </div>
        </div>

        {/* Main Content Area */}
        <div className="lg:col-span-9 space-y-6">
          {/* TAB 1: MY TASKS */}
          {activeTab === 'myTasks' && (
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-6">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                  <h2 className="text-lg font-bold text-gray-900">My Tasks</h2>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Manage and update progress for your assigned tasks.
                  </p>
                </div>

                {/* Filter pills by Status */}
                <div className="flex items-center bg-gray-100/80 p-1 rounded-xl text-xs font-medium overflow-x-auto">
                  {(['ALL', 'TO_DO', 'IN_PROGRESS', 'DONE', 'CLOSED'] as const).map((filter) => {
                    const labels = {
                      ALL: 'All',
                      TO_DO: 'To Do',
                      IN_PROGRESS: 'In Progress',
                      DONE: 'Done',
                      CLOSED: 'Closed',
                    };
                    const isActive = taskFilter === filter;
                    return (
                      <button
                        key={filter}
                        onClick={() => setTaskFilter(filter)}
                        className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer whitespace-nowrap ${
                          isActive
                            ? 'bg-white text-gray-900 font-semibold shadow-xs'
                            : 'text-gray-500 hover:text-gray-800'
                        }`}
                      >
                        {labels[filter]}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Quick Add Task & Search */}
              <div className="flex flex-col sm:flex-row gap-3">
                <div className="relative flex-1">
                  <Search size={16} className="absolute left-3.5 top-3 text-gray-400" />
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search tasks by ID (#1), title..."
                    className="w-full pl-10 pr-4 py-2 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all text-gray-800"
                  />
                </div>

                <form onSubmit={handleCreateTask} className="flex gap-2">
                  <input
                    type="text"
                    value={newTaskTitle}
                    onChange={(e) => setNewTaskTitle(e.target.value)}
                    placeholder="Quick add new task..."
                    className="pl-4 pr-3 py-2 bg-gray-50 border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 transition-all w-56 text-gray-800"
                  />
                  <button
                    type="submit"
                    className="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1 transition-all shrink-0 cursor-pointer shadow-xs"
                  >
                    <Plus size={15} /> Add
                  </button>
                </form>
              </div>

              {/* Task Items List */}
              {loadingTasks ? (
                <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
                  <Loader2 size={24} className="animate-spin text-emerald-600" />
                  <span>Loading your tasks...</span>
                </div>
              ) : filteredTasks.length === 0 ? (
                <div className="py-12 text-center text-xs text-gray-400 space-y-2 border-2 border-dashed border-gray-100 rounded-2xl">
                  <ListTodo size={28} className="mx-auto text-gray-300" />
                  <p>No tasks found matching your filter.</p>
                  <span className="text-[11px] text-gray-400">
                    {taskFilter !== 'ALL'
                      ? 'Try selecting the "All" filter or add a task above.'
                      : 'Quick add a task using the input box above.'}
                  </span>
                </div>
              ) : (
                <div className="divide-y divide-gray-100">
                  {filteredTasks.map((task) => {
                    const statusNorm = normalizeStatus(task.status);
                    const isDone = statusNorm === 'DONE';
                    const isInProgress = statusNorm === 'IN_PROGRESS';
                    const isExpanded = expandedTaskId === task.id;
                    const canChange = canChangeTaskStatus(task);

                    return (
                      <div
                        key={task.id}
                        id={`task-item-${task.id}`}
                        className={`py-3.5 px-2 rounded-xl transition-all space-y-2 group ${
                          highlightedTaskId === task.id
                            ? 'bg-emerald-50 ring-2 ring-emerald-500 shadow-sm'
                            : 'hover:bg-gray-50/60'
                        }`}
                      >
                        <div className="flex items-center justify-between gap-4">
                          <div className="flex items-center gap-3 min-w-0">
                            <button
                              type="button"
                              disabled={!canChange}
                              onClick={() => handleToggleTask(task.id)}
                              title={!canChange ? 'Only PM or the assignee can change status' : undefined}
                              className={`w-5 h-5 rounded-lg border flex items-center justify-center transition-all shrink-0 ${
                                !canChange ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'
                              } ${
                                isDone
                                  ? 'bg-emerald-600 border-emerald-600 text-white'
                                  : 'border-gray-300 hover:border-emerald-500 bg-white'
                              }`}
                            >
                              {isDone && <Check size={13} strokeWidth={3} />}
                            </button>

                            <div className="min-w-0">
                              <div className="flex items-center gap-2">
                                <span
                                  onClick={() => handleOpenTaskDetail(task)}
                                  className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-gray-100 hover:bg-gray-200 text-gray-600 font-semibold cursor-pointer transition-colors"
                                  title="View task details"
                                >
                                  #{task.id}
                                </span>
                                <p
                                  onClick={() => handleOpenTaskDetail(task)}
                                  className={`text-xs font-medium transition-all truncate cursor-pointer hover:text-emerald-700 ${
                                    isDone ? 'text-gray-400 line-through' : 'text-gray-900'
                                  }`}
                                  title="Click to view details"
                                >
                                  {task.title}
                                </p>
                              </div>

                              <div className="flex items-center gap-2 mt-1">
                                {task.category && (
                                  <span className="text-[10px] text-gray-500 bg-gray-100 px-2 py-0.5 rounded-md">
                                    {task.category}
                                  </span>
                                )}
                                <span className="text-[10px] text-gray-400 flex items-center gap-1">
                                  <Calendar size={11} /> {task.dueDate || 'Due date'}
                                </span>
                                <button
                                  type="button"
                                  onClick={() => setExpandedTaskId(isExpanded ? null : task.id)}
                                  className="text-[10px] font-medium text-emerald-600 hover:text-emerald-700 flex items-center gap-0.5 ml-2 cursor-pointer"
                                >
                                  <ListChecks size={12} />
                                  <span>Subtasks</span>
                                  {isExpanded ? <ChevronUp size={11} /> : <ChevronDown size={11} />}
                                </button>
                              </div>
                            </div>
                          </div>

                          <div className="flex items-center gap-2 shrink-0">
                            <span
                              className={`px-2 py-0.5 text-[10px] font-semibold rounded-md ${
                                task.priority === 'HIGH'
                                  ? 'bg-red-50 text-red-700 border border-red-100'
                                  : task.priority === 'MEDIUM'
                                  ? 'bg-amber-50 text-amber-700 border border-amber-100'
                                  : 'bg-gray-100 text-gray-600'
                              }`}
                            >
                              {task.priority === 'HIGH'
                                ? 'High'
                                : task.priority === 'MEDIUM'
                                ? 'Medium'
                                : 'Low'}
                            </span>

                            <select
                              value={statusNorm}
                              disabled={!canChange}
                              title={!canChange ? 'Only PM or the assignee can change status' : undefined}
                              onChange={(e) => handleStatusChange(task.id, e.target.value)}
                              className={`px-2.5 py-1 text-[11px] font-semibold rounded-lg border focus:ring-2 focus:ring-emerald-500 outline-none ${
                                !canChange ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'
                              } ${
                                isDone
                                  ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                                  : isInProgress
                                  ? 'bg-blue-50 text-blue-700 border-blue-200'
                                  : statusNorm === 'CLOSED'
                                  ? 'bg-gray-100 text-gray-500 border-gray-300'
                                  : 'bg-amber-50 text-amber-700 border-amber-200'
                              }`}
                            >
                              <option value="TO_DO">To Do</option>
                              <option value="IN_PROGRESS">In Progress</option>
                              <option value="DONE">Done</option>
                              <option value="CLOSED">Closed</option>
                            </select>

                            {/* View Details Button */}
                            <button
                              type="button"
                              onClick={() => handleOpenTaskDetail(task)}
                              className="p-1.5 text-gray-400 hover:text-emerald-700 hover:bg-emerald-50 rounded-lg transition-colors cursor-pointer"
                              title="View Details"
                            >
                              <Eye size={15} />
                            </button>

                            {/* Edit Task Button */}
                            <button
                              type="button"
                              onClick={() => handleOpenEditTask(task)}
                              className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors cursor-pointer"
                              title="Edit Task"
                            >
                              <Edit3 size={15} />
                            </button>

                            {/* Delete Task Button */}
                            <button
                              type="button"
                              onClick={() => {
                                if (confirm(`Are you sure you want to delete task "${task.title}"?`)) {
                                  handleDeleteTask(task.id);
                                }
                              }}
                              className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors cursor-pointer"
                              title="Delete Task"
                            >
                              <Trash2 size={15} />
                            </button>
                          </div>
                        </div>

                        {/* Expandable Subtask List */}
                        {isExpanded && (
                          <div className="pl-8 pt-1 animate-in fade-in">
                            <SubtaskList
                              taskId={task.id}
                              parentTaskAssigneeId={task.assigneeId}
                              members={task.projectId ? projectMembers[task.projectId] || [] : []}
                              onSelectSubtask={(st) => handleOpenSubtaskDetail(st, task)}
                              compact
                            />
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}

          {/* TAB 2: MY PROJECTS */}
          {activeTab === 'myProjects' && (
            <div className="space-y-4">
              <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">My Projects & Managed</h2>
                    <p className="text-xs text-gray-500 mt-0.5">
                      Projects you are contributing to or managing as Project Manager.
                    </p>
                  </div>
                  <button
                    onClick={() => setIsCreateProjectOpen(true)}
                    className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 shadow-xs transition-all cursor-pointer shrink-0"
                  >
                    <FolderPlus size={15} />
                    <span>New Project</span>
                  </button>
                </div>

                {loadingProjects ? (
                  <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
                    <Loader2 size={24} className="animate-spin text-emerald-600" />
                    <span>Loading projects...</span>
                  </div>
                ) : projectsList.length === 0 ? (
                  <div className="py-12 text-center text-xs text-gray-400 space-y-3 border-2 border-dashed border-gray-100 rounded-2xl">
                    <p>You have not joined or created any projects yet.</p>
                    <button
                      onClick={() => setIsCreateProjectOpen(true)}
                      className="px-4 py-2 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 rounded-xl text-xs font-semibold cursor-pointer"
                    >
                      + Click here to create your first project
                    </button>
                  </div>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {projectsList.map((p) => {
                      const isOwner = p.ownerId === user?.id || (p.roleInProject && p.roleInProject.includes('Project Manager')) || (p.currentUserRole && p.currentUserRole.toUpperCase() === 'PROJECT_MANAGER');
                      return (
                        <div
                          key={p.id}
                          onClick={() => onGoToProjectManager && onGoToProjectManager(p.id)}
                          className="p-5 rounded-2xl border border-gray-100 hover:border-emerald-300 bg-white hover:shadow-md transition-all space-y-4 flex flex-col justify-between cursor-pointer group"
                        >
                          <div className="space-y-3">
                            <div className="flex items-start justify-between gap-3">
                              <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold group-hover:bg-emerald-100 transition-colors">
                                <FolderKanban size={20} />
                              </div>
                              <span
                                className={`px-2.5 py-1 text-[10px] font-semibold rounded-md border ${
                                  isOwner
                                    ? 'bg-emerald-100 text-emerald-900 border-emerald-200'
                                    : 'bg-gray-100 text-gray-700 border-gray-200'
                                }`}
                              >
                                {p.roleInProject}
                              </span>
                            </div>

                            <div>
                              <h3 className="text-sm font-bold text-gray-900 group-hover:text-emerald-700 transition-colors">{p.name}</h3>
                              <p className="text-xs text-gray-500 mt-1 line-clamp-2">
                                {p.description}
                              </p>
                            </div>
                          </div>

                          <div className="space-y-3 pt-3 border-t border-gray-100">
                            <div className="space-y-1.5">
                              <div className="flex items-center justify-between text-xs">
                                <span className="text-gray-500 font-medium">Project Progress</span>
                                <span className="font-bold text-emerald-700">{p.progress}%</span>
                              </div>
                              <div className="w-full h-2 bg-gray-100 rounded-full overflow-hidden">
                                <div
                                  className="h-full bg-emerald-600 rounded-full transition-all"
                                  style={{ width: `${p.progress}%` }}
                                ></div>
                              </div>
                            </div>

                            <div className="flex items-center justify-between text-[11px] text-gray-400">
                              <span>Sprint: <strong className="text-gray-700 font-medium">{p.activeSprint}</strong></span>
                              {isOwner && onGoToProjectManager && (
                                <button
                                  type="button"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    onGoToProjectManager(p.id);
                                  }}
                                  className="text-emerald-700 hover:text-emerald-800 font-semibold flex items-center gap-1 cursor-pointer"
                                >
                                  <span>Manage Project</span>
                                  <ArrowRight size={12} />
                                </button>
                              )}
                            </div>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* TAB 3: RECENT ACTIVITIES (Live data from API) */}
          {activeTab === 'activities' && (
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
              <h2 className="text-lg font-bold text-gray-900">Recent Activities</h2>
              <p className="text-xs text-gray-500">
                Audit log of actions and status updates recorded by the system.
              </p>

              {loadingActivities ? (
                <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
                  <Loader2 size={24} className="animate-spin text-emerald-600" />
                  <span>Loading activities...</span>
                </div>
              ) : activityLogs.length === 0 ? (
                <div className="py-12 text-center text-xs text-gray-400 space-y-2 border-2 border-dashed border-gray-100 rounded-2xl">
                  <Activity size={24} className="mx-auto text-gray-300" />
                  <p>No recent activity recorded.</p>
                </div>
              ) : (
                <div className="divide-y divide-gray-100 pt-2">
                  {activityLogs.map((log) => (
                    <div key={log.id} className="py-3.5 flex items-center justify-between text-xs">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-full bg-gray-100 text-gray-700 flex items-center justify-center font-semibold text-[11px]">
                          {(log.userName || 'U').substring(0, 2).toUpperCase()}
                        </div>
                        <div>
                          <span className="font-semibold text-gray-900">{log.userName}</span>{' '}
                          <span className="text-gray-500">{log.action}</span>{' '}
                          <span className="font-medium text-emerald-700">{log.target}</span>
                        </div>
                      </div>
                      <span className="text-[11px] text-gray-400">{log.timeAgo}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Modal Create Project */}
      <CreateProjectModal
        isOpen={isCreateProjectOpen}
        onClose={() => setIsCreateProjectOpen(false)}
        onSuccess={handleProjectCreated}
      />

      {/* Modal Task / Subtask Details */}
      <TaskDetailModal
        task={selectedDetailTask}
        subtask={selectedDetailSubtask}
        parentTask={parentTaskForSubtask}
        members={selectedDetailTask?.projectId ? projectMembers[selectedDetailTask.projectId] || [] : []}
        isOpen={isDetailModalOpen}
        onClose={() => {
          setIsDetailModalOpen(false);
          setSelectedDetailTask(null);
          setSelectedDetailSubtask(null);
          setParentTaskForSubtask(null);
        }}
        onEditTask={(t) => {
          setIsDetailModalOpen(false);
          handleOpenEditTask(t);
        }}
        onEditSubtask={(st, pTaskId) => {
          setIsDetailModalOpen(false);
          handleOpenEditSubtask(st, pTaskId);
        }}
        onDeleteTask={handleDeleteTask}
        onDeleteSubtask={handleDeleteSubtask}
        onSelectSubtask={(st, pTask) => {
          handleOpenSubtaskDetail(st, pTask);
        }}
        onBackToParentTask={(pTask) => {
          handleOpenTaskDetail(pTask);
        }}
        onCreateSubtaskForTask={(t) => {
          handleOpenCreateSubtask(t);
        }}
      />

      {/* Modal Edit Task */}
      <EditTaskModal
        task={editingTask}
        members={editingTask?.projectId ? projectMembers[editingTask.projectId] || [] : []}
        isOpen={isEditTaskModalOpen}
        onClose={() => {
          setIsEditTaskModalOpen(false);
          setEditingTask(null);
        }}
        onSuccess={handleTaskUpdated}
      />

      {/* Modal Create / Edit Subtask */}
      {subtaskFormTaskId && (
        <SubtaskFormModal
          taskId={subtaskFormTaskId}
          subtask={editingSubtaskItem}
          members={(() => {
            const t = tasks.find((tk) => tk.id === subtaskFormTaskId);
            return t?.projectId ? projectMembers[t.projectId] || [] : [];
          })()}
          isOpen={isSubtaskFormOpen}
          onClose={() => {
            setIsSubtaskFormOpen(false);
            setSubtaskFormTaskId(null);
            setEditingSubtaskItem(null);
          }}
          onSuccess={(saved) => {
            // If we were viewing the parent task or this subtask, refresh
            if (selectedDetailSubtask?.id === saved.id) {
              setSelectedDetailSubtask(saved);
            }
          }}
        />
      )}

      {/* Modal Error Notification */}
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
