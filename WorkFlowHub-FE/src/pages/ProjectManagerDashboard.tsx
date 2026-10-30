import React, { useState, useEffect, useMemo } from 'react';
import { useAuth } from '../context/AuthContext';
import { 
  ProjectService, 
  SprintService, 
  ProjectMemberService, 
  TaskService 
} from '../services/api';
import type { Project, Sprint, ProjectMember, Task, Subtask } from '../types';
import { CreateSprintModal } from '../components/CreateSprintModal';
import { EditSprintModal } from '../components/EditSprintModal';
import { AddProjectMemberModal } from '../components/AddProjectMemberModal';
import { CreateProjectModal } from '../components/CreateProjectModal';
import { CreateTaskModal } from '../components/CreateTaskModal';
import { SubtaskList } from '../components/SubtaskList';
import { TaskDetailModal } from '../components/TaskDetailModal';
import { EditTaskModal } from '../components/EditTaskModal';
import { SubtaskFormModal } from '../components/SubtaskFormModal';
import { ErrorModal } from '../components/ErrorModal';
import { SubtaskService } from '../services/subtaskService';
import { 
  FolderKanban, 
  Sparkles, 
  Plus, 
  Calendar, 
  UserPlus, 
  Users, 
  ShieldCheck, 
  Trash2, 
  ArrowLeft, 
  Layers, 
  CheckCircle2, 
  AlertCircle, 
  FolderPlus, 
  Loader2, 
  Edit3,
  Eye,
  Search,
  Filter,
  Lock,
  ChevronDown,
  ChevronUp,
  ListChecks,
  ArrowRight,
  RotateCcw
} from 'lucide-react';

interface ProjectManagerDashboardProps {
  initialProjectId?: number | null;
  onSwitchToUserView?: () => void;
}

export const ProjectManagerDashboard: React.FC<ProjectManagerDashboardProps> = ({
  initialProjectId,
  onSwitchToUserView,
}) => {
  const { user, currentRole } = useAuth();
  const [activeTab, setActiveTab] = useState<'currentSprint' | 'sprints' | 'members' | 'assignments' | 'projects' | 'trash'>('sprints');

  // Quản lý danh sách dự án (100% dữ liệu thực từ Backend)
  const [projects, setProjects] = useState<Project[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<number | null>(initialProjectId || null);
  const [loadingProjects, setLoadingProjects] = useState(false);

  // Sprints, Members & Tasks của dự án đang chọn
  const [sprints, setSprints] = useState<Sprint[]>([]);
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [trashTasks, setTrashTasks] = useState<Task[]>([]);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [loadingTrash, setLoadingTrash] = useState(false);
  const [isClearingTrash, setIsClearingTrash] = useState(false);

  // Sprint được chọn để xem chi tiết các Task (Click vào sprint)
  const [selectedSprintForTasks, setSelectedSprintForTasks] = useState<Sprint | null>(null);

  // Sprint được chọn xem trong tab Burndown
  const [selectedBurndownSprintId, setSelectedBurndownSprintId] = useState<number | null>(null);

  // Filter trong Sprint Tasks view
  const [sprintTaskSearch, setSprintTaskSearch] = useState('');
  const [sprintMemberFilter, setSprintMemberFilter] = useState('ALL');
  const [sprintStatusFilter, setSprintStatusFilter] = useState('ALL');

  // Filter trong Phân công công việc (Assignments view)
  const [assignmentSprintFilter, setAssignmentSprintFilter] = useState('ALL');
  const [assignmentMemberFilter, setAssignmentMemberFilter] = useState('ALL');
  const [assignmentStatusFilter, setAssignmentStatusFilter] = useState('ALL');
  const [assignmentSearchQuery, setAssignmentSearchQuery] = useState('');

  // Expandable subtasks state cho từng task (taskId -> boolean)
  const [expandedTaskIds, setExpandedTaskIds] = useState<Record<number, boolean>>({});

  // Modals
  const [isCreateSprintOpen, setIsCreateSprintOpen] = useState(false);
  const [editingSprint, setEditingSprint] = useState<Sprint | null>(null);
  const [isEditSprintOpen, setIsEditSprintOpen] = useState(false);
  const [isAddMemberOpen, setIsAddMemberOpen] = useState(false);
  const [isCreateProjectOpen, setIsCreateProjectOpen] = useState(false);
  const [isCreateTaskOpen, setIsCreateTaskOpen] = useState(false);
  const [createTaskSprintId, setCreateTaskSprintId] = useState<number | null>(null);

  // Modals: Task Detail, Edit Task, Subtask Form
  const [selectedDetailTask, setSelectedDetailTask] = useState<Task | null>(null);
  const [selectedDetailSubtask, setSelectedDetailSubtask] = useState<Subtask | null>(null);
  const [parentTaskForSubtask, setParentTaskForSubtask] = useState<Task | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);

  const [editingTask, setEditingTask] = useState<Task | null>(null);
  const [isEditTaskModalOpen, setIsEditTaskModalOpen] = useState(false);

  const [subtaskFormTaskId, setSubtaskFormTaskId] = useState<number | null>(null);
  const [editingSubtaskItem, setEditingSubtaskItem] = useState<Subtask | null>(null);
  const [isSubtaskFormOpen, setIsSubtaskFormOpen] = useState(false);

  const handleOpenTaskDetail = (task: Task) => {
    setSelectedDetailTask(task);
    setSelectedDetailSubtask(null);
    setParentTaskForSubtask(null);
    setIsDetailModalOpen(true);
  };

  const handleOpenSubtaskDetail = (st: Subtask, pTask: Task) => {
    setSelectedDetailSubtask(st);
    setParentTaskForSubtask(pTask);
    setSelectedDetailTask(null);
    setIsDetailModalOpen(true);
  };

  const handleOpenEditTask = (task: Task) => {
    setEditingTask(task);
    setIsEditTaskModalOpen(true);
  };

  const handleOpenCreateSubtask = (task: Task) => {
    setSubtaskFormTaskId(task.id);
    setEditingSubtaskItem(null);
    setIsSubtaskFormOpen(true);
  };

  const handleOpenEditSubtask = (st: Subtask, parentTaskId: number) => {
    setSubtaskFormTaskId(parentTaskId);
    setEditingSubtaskItem(st);
    setIsSubtaskFormOpen(true);
  };

  const handleTaskUpdated = (updatedTask: Task) => {
    setTasks((prev) => prev.map((t) => (t.id === updatedTask.id ? updatedTask : t)));
    if (selectedDetailTask?.id === updatedTask.id) {
      setSelectedDetailTask(updatedTask);
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

  // Notifications
  const [notification, setNotification] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

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

  const sidebarItems = [
    { id: 'sprints', label: 'Sprint Management', icon: Layers },
    { id: 'members', label: 'Members & Permissions', icon: Users },
    { id: 'currentSprint', label: 'Active Sprint & Burndown', icon: Sparkles },
    { id: 'assignments', label: 'Task Assignments', icon: FolderKanban },
    { id: 'trash', label: 'Task Trash Bin', icon: Trash2, badge: trashTasks.length },
  ];

  const currentProject = projects.find((p) => p.id === selectedProjectId) || projects[0];

  // Check PM permissions for current project
  const currentMember = members.find((m) => m.userId === user?.id);
  const isPM = Boolean(
    (user?.roleName || user?.role || '').toUpperCase() === 'ADMIN' ||
    (currentProject && currentProject.ownerId === user?.id) ||
    (currentMember && (currentMember.roleName || '').toUpperCase() === 'PROJECT_MANAGER') ||
    (currentProject?.currentUserRole && ['PROJECT_MANAGER', 'PM', 'OWNER'].includes(currentProject.currentUserRole.toUpperCase())) ||
    (!user && currentRole === 'PROJECT_MANAGER')
  );

  // Sync when initialProjectId changes
  useEffect(() => {
    if (initialProjectId) {
      setSelectedProjectId(initialProjectId);
    }
  }, [initialProjectId]);

  // 1. Load projects list
  useEffect(() => {
    loadProjects();
  }, [user?.id]);

  const loadProjects = async () => {
    setLoadingProjects(true);
    try {
      const roleStr = (currentRole || user?.roleName || user?.role || '').toUpperCase();
      const fetchedProjects = roleStr === 'ADMIN'
        ? await ProjectService.getProjects()
        : await ProjectService.getMyProjects();
      if (fetchedProjects && fetchedProjects.length > 0) {
        setProjects(fetchedProjects);
        const target = initialProjectId
          ? fetchedProjects.find((p) => p.id === initialProjectId)
          : null;
        const myProject = target || fetchedProjects.find((p) => p.ownerId === user?.id) || fetchedProjects[0];
        setSelectedProjectId(myProject.id);
      } else {
        setProjects([]);
        setSelectedProjectId(null);
      }
    } catch (err) {
      console.warn('Error loading projects from server:', err);
      setProjects([]);
      setSelectedProjectId(null);
    } finally {
      setLoadingProjects(false);
    }
  };

  // 2. Load Sprints, Members, and Tasks for selected project
  useEffect(() => {
    if (selectedProjectId) {
      loadProjectDetails(selectedProjectId);
      setSelectedSprintForTasks(null);
    } else {
      setSprints([]);
      setMembers([]);
      setTasks([]);
      setSelectedSprintForTasks(null);
    }
  }, [selectedProjectId, user?.id]);

  const loadProjectDetails = async (projectId: number) => {
    setLoadingDetails(true);
    try {
      const [fetchedSprints, fetchedMembers, fetchedTasks, fetchedTrash] = await Promise.all([
        SprintService.getSprintsByProjectId(projectId),
        ProjectMemberService.getMembersByProjectId(projectId),
        TaskService.getTasksByProjectId(projectId),
        TaskService.getTrashTasks(projectId, user?.id).catch(() => []),
      ]);

      setSprints(fetchedSprints || []);
      setMembers(fetchedMembers || []);
      setTasks(fetchedTasks || []);
      setTrashTasks(fetchedTrash || []);
    } catch (err) {
      console.warn('Error loading project details:', err);
      setSprints([]);
      setMembers([]);
      setTasks([]);
      setTrashTasks([]);
    } finally {
      setLoadingDetails(false);
    }
  };

  const loadTrashTasks = async () => {
    if (!selectedProjectId) return;
    setLoadingTrash(true);
    try {
      const fetchedTrash = await TaskService.getTrashTasks(selectedProjectId, user?.id);
      setTrashTasks(fetchedTrash || []);
    } catch (err) {
      console.warn('Error loading trash bin:', err);
    } finally {
      setLoadingTrash(false);
    }
  };

  const handleSprintCreated = (newSprint: Sprint) => {
    setSprints((prev) => [newSprint, ...prev]);
    showNotification('success', `Successfully created sprint "${newSprint.name}"!`);
  };

  const handleSprintUpdated = (updatedSprint: Sprint) => {
    setSprints((prev) => prev.map((s) => (s.id === updatedSprint.id ? updatedSprint : s)));
    if (selectedSprintForTasks && selectedSprintForTasks.id === updatedSprint.id) {
      setSelectedSprintForTasks(updatedSprint);
    }
    showNotification('success', `Successfully updated sprint "${updatedSprint.name}"!`);
  };

  const handleDeleteSprint = async (sprint: Sprint) => {
    if (!isPM) {
      alert('Only users with Project Manager (PM) role are allowed to delete sprints.');
      return;
    }
    if (!window.confirm(`Are you sure you want to delete Sprint "${sprint.name}"?`)) {
      return;
    }
    try {
      await SprintService.deleteSprint(sprint.id);
      setSprints((prev) => prev.filter((s) => s.id !== sprint.id));
      if (selectedSprintForTasks?.id === sprint.id) {
        setSelectedSprintForTasks(null);
      }
      showNotification('success', `Successfully deleted sprint "${sprint.name}"!`);
    } catch (err: any) {
      console.error('Error deleting sprint:', err);
      showNotification('error', err.message || 'Unable to delete sprint.');
    }
  };

  const handleTaskCreated = (newTask: Task) => {
    setTasks((prev) => [newTask, ...prev]);
    showNotification('success', `Successfully created task "${newTask.title}"!`);
  };

  const canChangeTaskStatus = (task: Task) => {
    if (isPM) return true;
    if (!user) return false;
    return Boolean(
      task.assigneeId === user.id ||
      (task.assigneeName && task.assigneeName.trim().toLowerCase() === user.name?.trim().toLowerCase())
    );
  };

  const handleUpdateTaskStatus = async (taskId: number, newStatus: string) => {
    const targetTask = tasks.find((t) => t.id === taskId);
    if (targetTask && !canChangeTaskStatus(targetTask)) {
      showErrorModal(
        'Only Project Managers or the assigned member can change task status.',
        'Access Denied',
        'FORBIDDEN'
      );
      return;
    }
    try {
      await TaskService.updateTask(taskId, { status: newStatus });
      setTasks((prev) => prev.map((t) => (t.id === taskId ? { ...t, status: newStatus } : t)));
      showNotification('success', 'Task status updated successfully!');
    } catch (err: any) {
      console.error('Error updating task status:', err);
      // Keep previous UI state
      setTasks((prev) => [...prev]);
      showErrorModal(
        err.message || 'Only Project Managers or the assigned member can change task status.',
        'Access Denied',
        err.code || (err.status === 403 ? 'FORBIDDEN' : 'ERROR')
      );
      showNotification('error', err.message || 'Unable to update status.');
    }
  };

  const handleDeleteTask = async (taskId: number, taskTitle?: string) => {
    if (!isPM) {
      showErrorModal('Only Project Managers (PM) have permission to delete tasks.', 'Access Denied', 'FORBIDDEN');
      return;
    }
    if (!window.confirm(`Are you sure you want to move task "${taskTitle || 'this task'}" to trash?`)) {
      return;
    }
    try {
      await TaskService.deleteTask(taskId);
      const deletedTask = tasks.find((t) => t.id === taskId);
      setTasks((prev) => prev.filter((t) => t.id !== taskId));
      if (deletedTask) {
        setTrashTasks((prev) => [{ ...deletedTask, isDeleted: true, deletedById: user?.id, deletedByName: user?.name }, ...prev]);
      } else {
        loadTrashTasks();
      }
      showNotification('success', 'Task moved to trash!');
    } catch (err: any) {
      console.error('Error deleting task:', err);
      showErrorModal(err.message || 'Unable to delete task.', 'Operation Error', err.code);
      showNotification('error', err.message || 'Unable to delete task.');
    }
  };

  const handleRestoreTask = async (taskId: number) => {
    try {
      await TaskService.restoreTask(taskId);
      const restoredTask = trashTasks.find((t) => t.id === taskId);
      setTrashTasks((prev) => prev.filter((t) => t.id !== taskId));
      if (restoredTask) {
        setTasks((prev) => [{ ...restoredTask, isDeleted: false }, ...prev]);
      } else if (selectedProjectId) {
        loadProjectDetails(selectedProjectId);
      }
      showNotification('success', 'Task restored successfully!');
    } catch (err: any) {
      console.error('Error restoring task:', err);
      showNotification('error', err.message || 'Unable to restore task.');
    }
  };

  const handlePermanentDeleteTask = async (taskId: number, taskTitle?: string) => {
    if (!isPM) {
      alert('Only Project Managers (PM) have permission to permanently delete tasks.');
      return;
    }
    if (!window.confirm(`This action CANNOT be undone. Are you sure you want to permanently delete task "${taskTitle || 'this task'}"?`)) {
      return;
    }
    try {
      await TaskService.permanentDeleteTask(taskId);
      setTrashTasks((prev) => prev.filter((t) => t.id !== taskId));
      showNotification('success', 'Task permanently deleted!');
    } catch (err: any) {
      console.error('Error permanently deleting task:', err);
      showNotification('error', err.message || 'Unable to permanently delete task.');
    }
  };

  const handleEmptyTrash = async () => {
    if (!selectedProjectId) return;
    if (trashTasks.length === 0) {
      showNotification('success', 'Trash bin is already empty.');
      return;
    }

    const confirmMessage = isPM
      ? `This action CANNOT be undone. Are you sure you want to permanently delete all ${trashTasks.length} task(s) in the trash bin?`
      : `This action CANNOT be undone. Are you sure you want to permanently delete all your task(s) in the trash bin?`;

    if (!window.confirm(confirmMessage)) {
      return;
    }

    setIsClearingTrash(true);
    try {
      await TaskService.emptyTrash(selectedProjectId, isPM);
      setTrashTasks([]);
      showNotification('success', 'Trash bin emptied successfully!');
    } catch (err: any) {
      console.error('Error emptying trash bin:', err);
      showNotification('error', err.message || 'Unable to empty trash bin.');
    } finally {
      setIsClearingTrash(false);
    }
  };

  const handleMemberAdded = (newMember: ProjectMember) => {
    setMembers((prev) => [newMember, ...prev]);
    showNotification('success', `Added member "${newMember.userName || 'New'}" with role ${newMember.roleName || 'Member'}!`);
  };

  const handleProjectCreated = (newProject: Project) => {
    setProjects((prev) => [newProject, ...prev]);
    setSelectedProjectId(newProject.id);
    showNotification('success', `Successfully created project "${newProject.name}"! You are the Project Manager of this project.`);
  };

  const handleRemoveMember = async (userId: number, userName?: string) => {
    if (!selectedProjectId) return;
    if (!isPM) {
      showErrorModal('Only Project Managers (PM) have permission to remove members from the project.', 'Access Denied', 'FORBIDDEN');
      return;
    }
    if (userId === user?.id) {
      alert('You are the project creator (Project Manager) and cannot remove yourself.');
      return;
    }
    if (!window.confirm(`Are you sure you want to remove member "${userName || 'this member'}" from the project?`)) {
      return;
    }

    try {
      await ProjectMemberService.removeMember(selectedProjectId, userId);
      setMembers((prev) => prev.filter((m) => m.userId !== userId));
      showNotification('success', `Member removed from project.`);
    } catch (err: any) {
      console.error('Error removing member:', err);
      showNotification('error', err.message || 'Unable to remove member.');
    }
  };

  const toggleTaskSubtasks = (taskId: number) => {
    setExpandedTaskIds((prev) => ({
      ...prev,
      [taskId]: !prev[taskId],
    }));
  };

  const showNotification = (type: 'success' | 'error', message: string) => {
    setNotification({ type, message });
    setTimeout(() => {
      setNotification(null);
    }, 4000);
  };

  // Chuẩn hóa trạng thái
  const normalizeStatus = (status?: string): 'TO_DO' | 'IN_PROGRESS' | 'DONE' | 'CLOSED' => {
    if (!status) return 'TO_DO';
    const s = status.trim().toUpperCase();
    if (s === 'CLOSED' || s === 'ĐÃ ĐÓNG') return 'CLOSED';
    if (s === 'DONE' || s === 'COMPLETED' || s === 'ĐÃ XONG' || s === 'HOÀN THÀNH') return 'DONE';
    if (s === 'IN_PROGRESS' || s === 'PROGRESS' || s === 'ĐANG LÀM') return 'IN_PROGRESS';
    return 'TO_DO';
  };

  // Lọc tasks trong Sprint được chọn (Requirement 3: click vào sprint hiện tất cả task & filter bằng mã task)
  const sprintTasks = selectedSprintForTasks
    ? tasks.filter((t) => t.sprintId === selectedSprintForTasks.id)
    : [];

  const filteredSprintTasks = sprintTasks.filter((t) => {
    // 1. Filter bằng mã task (hoặc tiêu đề)
    const query = sprintTaskSearch.toLowerCase().trim();
    const matchCode =
      !query ||
      `#${t.id}`.includes(query) ||
      `task-${t.id}`.toLowerCase().includes(query) ||
      (t.taskCode && t.taskCode.toLowerCase().includes(query)) ||
      t.title.toLowerCase().includes(query);

    // 2. Filter thành viên
    let matchMember = true;
    if (sprintMemberFilter === 'UNASSIGNED') {
      matchMember = !t.assigneeId;
    } else if (sprintMemberFilter !== 'ALL') {
      matchMember = t.assigneeId === Number(sprintMemberFilter);
    }

    // 3. Filter trạng thái
    let matchStatus = true;
    if (sprintStatusFilter !== 'ALL') {
      matchStatus = normalizeStatus(t.status) === sprintStatusFilter;
    }

    return matchCode && matchMember && matchStatus;
  });

  // Lọc tasks trong tab Assignments (Requirement 5: filter sprint và thành viên)
  const filteredAssignmentsTasks = tasks.filter((t) => {
    // 1. Filter Sprint
    let matchSprint = true;
    if (assignmentSprintFilter === 'NO_SPRINT') {
      matchSprint = !t.sprintId;
    } else if (assignmentSprintFilter !== 'ALL') {
      matchSprint = t.sprintId === Number(assignmentSprintFilter);
    }

    // 2. Filter Thành viên
    let matchMember = true;
    if (assignmentMemberFilter === 'UNASSIGNED') {
      matchMember = !t.assigneeId;
    } else if (assignmentMemberFilter !== 'ALL') {
      matchMember = t.assigneeId === Number(assignmentMemberFilter);
    }

    // 3. Filter Trạng thái
    let matchStatus = true;
    if (assignmentStatusFilter !== 'ALL') {
      matchStatus = normalizeStatus(t.status) === assignmentStatusFilter;
    }

    // 4. Tìm kiếm từ khóa / mã task
    const query = assignmentSearchQuery.toLowerCase().trim();
    const matchSearch =
      !query ||
      `#${t.id}`.includes(query) ||
      `task-${t.id}`.toLowerCase().includes(query) ||
      (t.taskCode && t.taskCode.toLowerCase().includes(query)) ||
      t.title.toLowerCase().includes(query) ||
      (t.description && t.description.toLowerCase().includes(query));

    return matchSprint && matchMember && matchStatus && matchSearch;
  });

  // Xác định sprint để hiển thị ở tab "Sprint hiện tại & Burndown" (Dữ liệu 100% thực tế)
  const activeSprint = sprints.find((s) => s.status === 'ACTIVE');
  const currentSprint = (selectedBurndownSprintId ? sprints.find((s) => s.id === selectedBurndownSprintId) : null)
    || activeSprint
    || (sprints.length > 0 ? sprints[0] : null);

  // Số liệu thực tế của currentSprint
  const currentSprintTasks = currentSprint ? tasks.filter((t) => t.sprintId === currentSprint.id) : [];
  const currentSprintTotalTasks = currentSprintTasks.length;
  const currentSprintTodoTasks = currentSprintTasks.filter((t) => t.status === 'TO_DO' || (!t.status));
  const currentSprintInProgressTasks = currentSprintTasks.filter((t) => t.status === 'IN_PROGRESS');
  const currentSprintDoneTasks = currentSprintTasks.filter((t) => t.status === 'DONE');
  const currentSprintRemainingTasks = currentSprintTotalTasks - currentSprintDoneTasks.length;

  const currentSprintProgress = currentSprintTotalTasks > 0
    ? Math.round((currentSprintDoneTasks.length / currentSprintTotalTasks) * 100)
    : (currentSprint?.status === 'COMPLETED' ? 100 : 0);

  // Calculate duration and remaining days
  let sprintDurationDays = 0;
  let sprintDurationText = 'No schedule set';
  let daysRemainingText = 'No deadline';
  let daysRemainingCount: number | null = null;
  let isSprintOverdue = false;
  let elapsedDays = 1;

  if (currentSprint) {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    if (currentSprint.startDate && currentSprint.endDate) {
      const start = new Date(currentSprint.startDate);
      start.setHours(0, 0, 0, 0);
      const end = new Date(currentSprint.endDate);
      end.setHours(0, 0, 0, 0);

      sprintDurationDays = Math.max(1, Math.round((end.getTime() - start.getTime()) / (1000 * 3600 * 24)) + 1);
      sprintDurationText = `${sprintDurationDays} days (${start.toLocaleDateString('en-US')} - ${end.toLocaleDateString('en-US')})`;

      if (today < start) {
        elapsedDays = 0;
      } else {
        elapsedDays = Math.min(sprintDurationDays, Math.max(1, Math.round((today.getTime() - start.getTime()) / (1000 * 3600 * 24)) + 1));
      }
    }

    if (currentSprint.status === 'COMPLETED') {
      daysRemainingText = 'Completed';
    } else if (currentSprint.endDate) {
      const end = new Date(currentSprint.endDate);
      end.setHours(0, 0, 0, 0);
      daysRemainingCount = Math.ceil((end.getTime() - today.getTime()) / (1000 * 3600 * 24));

      if (daysRemainingCount < 0) {
        isSprintOverdue = true;
        daysRemainingText = `Overdue by ${Math.abs(daysRemainingCount)} days`;
      } else if (daysRemainingCount === 0) {
        daysRemainingText = 'Ends today';
      } else {
        daysRemainingText = `${daysRemainingCount} days remaining`;
      }
    }
  }

  // Dynamic Burndown chart data
  const burndownChartBars = useMemo(() => {
    if (!currentSprint || currentSprintTotalTasks === 0) return [];
    
    const barCount = sprintDurationDays > 0 ? Math.min(Math.max(sprintDurationDays, 5), 14) : 10;
    const bars = [];
    
    for (let i = 1; i <= barCount; i++) {
      let remainingEst = 0;
      let isHighlight = false;

      if (elapsedDays === 0) {
        remainingEst = currentSprintTotalTasks;
      } else if (i <= elapsedDays) {
        const step = (currentSprintTotalTasks - currentSprintRemainingTasks) / Math.max(1, elapsedDays);
        remainingEst = Math.max(0, Math.round(currentSprintTotalTasks - (step * (i - 1))));
        if (i === elapsedDays) {
          remainingEst = currentSprintRemainingTasks;
          isHighlight = true;
        }
      } else {
        const remainingSteps = barCount - elapsedDays;
        const futureStep = currentSprintRemainingTasks / Math.max(1, remainingSteps);
        remainingEst = Math.max(0, Math.round(currentSprintRemainingTasks - (futureStep * (i - elapsedDays))));
      }

      const heightPercent = currentSprintTotalTasks > 0
        ? Math.max(8, Math.round((remainingEst / currentSprintTotalTasks) * 100))
        : 8;

      bars.push({
        day: i,
        remaining: remainingEst,
        height: `${heightPercent}%`,
        isHighlight,
      });
    }
    return bars;
  }, [currentSprint, currentSprintTotalTasks, currentSprintRemainingTasks, sprintDurationDays, elapsedDays]);

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      {/* Floating Notification Toast */}
      {notification && (
        <div
          className={`fixed top-6 right-6 z-[90] max-w-md p-4 rounded-2xl flex items-center gap-3 text-xs shadow-2xl border backdrop-blur-md animate-in slide-in-from-top-4 duration-300 ${
            notification.type === 'success'
              ? 'bg-emerald-50/95 border-emerald-300 text-emerald-800'
              : 'bg-red-50/95 border-red-300 text-red-800'
          }`}
        >
          {notification.type === 'success' ? (
            <CheckCircle2 size={18} className="shrink-0 text-emerald-600" />
          ) : (
            <AlertCircle size={18} className="shrink-0 text-red-600" />
          )}
          <span className="font-semibold">{notification.message}</span>
        </div>
      )}

      {/* Banner / Project Selection Bar */}
      <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs mb-8 flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-0.5 text-[10px] font-bold tracking-wider rounded-md bg-emerald-100 text-emerald-800 uppercase">
              Project Manager Workspace
            </span>
            {isPM ? (
              <span className="px-2 py-0.5 text-[10px] font-semibold rounded-md bg-purple-50 text-purple-700 border border-purple-100 flex items-center gap-1">
                <ShieldCheck size={12} />
                <span>Role: Project Manager (Full Sprint permissions)</span>
              </span>
            ) : (
              <span className="px-2 py-0.5 text-[10px] font-semibold rounded-md bg-gray-100 text-gray-600 border border-gray-200 flex items-center gap-1">
                <Lock size={12} />
                <span>Role: Member (View only)</span>
              </span>
            )}
          </div>
          <h1 className="text-xl sm:text-2xl font-bold text-gray-900 tracking-tight flex items-center gap-2">
            {currentProject?.name || 'Your Project'}
          </h1>
          <p className="text-xs text-gray-500 max-w-xl">
            {currentProject?.description || 'Manage sprints, coordinate team members, and monitor project velocity.'}
          </p>
        </div>

        {/* Project Selector & Actions */}
        <div className="flex flex-wrap items-center gap-3 shrink-0">
          {loadingProjects && (
            <div className="flex items-center gap-1.5 text-xs text-gray-400">
              <Loader2 size={14} className="animate-spin text-emerald-600" />
              <span>Loading projects...</span>
            </div>
          )}
          {projects.length > 1 && (
            <select
              value={selectedProjectId || ''}
              onChange={(e) => setSelectedProjectId(Number(e.target.value))}
              className="px-3.5 py-2 bg-gray-50 border border-gray-200 rounded-xl text-xs font-semibold text-gray-700 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 cursor-pointer"
            >
              {projects.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          )}

          <button
            onClick={() => setIsCreateProjectOpen(true)}
            className="px-3.5 py-2 bg-emerald-50 hover:bg-emerald-100 text-emerald-800 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer border border-emerald-200"
          >
            <FolderPlus size={15} />
            <span>New Project</span>
          </button>

          {onSwitchToUserView && (
            <button
              onClick={onSwitchToUserView}
              className="px-3.5 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer"
            >
              <ArrowLeft size={14} />
              <span>Back to Member Workspace</span>
            </button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
        {/* Left Sidebar */}
        <div className="lg:col-span-3 space-y-4">
          <div className="bg-white rounded-3xl p-5 border border-gray-100 shadow-xs space-y-1">
            <div className="text-[11px] font-bold text-gray-400 tracking-wider px-3 py-2 uppercase">
              Sprint & Team Management
            </div>
            {sidebarItems.map((item) => {
              const isActive = activeTab === item.id;
              const Icon = item.icon;
              return (
                <button
                  key={item.id}
                  onClick={() => {
                    setActiveTab(item.id as any);
                    if (item.id !== 'sprints') {
                      setSelectedSprintForTasks(null);
                    }
                  }}
                  className={`w-full text-left px-3.5 py-2.5 rounded-xl text-xs font-medium transition-all flex items-center justify-between cursor-pointer ${
                    isActive
                      ? 'bg-emerald-50 text-emerald-800 font-semibold'
                      : 'text-gray-600 hover:bg-gray-50'
                  }`}
                >
                  <div className="flex items-center gap-2.5">
                    <Icon size={16} className={isActive ? 'text-emerald-600' : 'text-gray-400'} />
                    <span>{item.label}</span>
                  </div>
                  {item.badge !== undefined && item.badge > 0 && (
                    <span className="px-2 py-0.5 text-[10px] font-bold rounded-full bg-red-100 text-red-700">
                      {item.badge}
                    </span>
                  )}
                </button>
              );
            })}
          </div>

          {/* Quick Metrics Card */}
          <div className="bg-white rounded-3xl p-5 border border-gray-100 shadow-xs space-y-3">
            <div className="text-[11px] font-bold text-gray-400 tracking-wider uppercase">
              Project Overview
            </div>
            <div className="space-y-2 text-xs">
              <div className="flex justify-between py-1 border-b border-gray-50">
                <span className="text-gray-500">Total Sprints:</span>
                <span className="font-bold text-gray-900">{sprints.length}</span>
              </div>
              <div className="flex justify-between py-1 border-b border-gray-50">
                <span className="text-gray-500">Members:</span>
                <span className="font-bold text-gray-900">{members.length}</span>
              </div>
              <div className="flex justify-between py-1 border-b border-gray-50">
                <span className="text-gray-500">Total Tasks:</span>
                <span className="font-bold text-gray-900">{tasks.length}</span>
              </div>
              <div className="flex justify-between py-1">
                <span className="text-gray-500">Your Role:</span>
                <span className={`font-bold ${isPM ? 'text-purple-600' : 'text-emerald-600'}`}>
                  {isPM ? 'Project Manager' : 'Member'}
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Main Content Area */}
        <div className="lg:col-span-9 space-y-6">
          {/* TAB 1: SPRINT MANAGEMENT */}
          {activeTab === 'sprints' && (
            <div className="space-y-6">
              {/* IF SPECIFIC SPRINT SELECTED: DISPLAY ALL SPRINT TASKS + TASK CODE FILTER */}
              {selectedSprintForTasks ? (
                <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-6 animate-in fade-in">
                  {/* Sprint Detail Header */}
                  <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 pb-4 border-b border-gray-100">
                    <div className="space-y-2">
                      <button
                        onClick={() => setSelectedSprintForTasks(null)}
                        className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 hover:text-emerald-800 bg-emerald-50 hover:bg-emerald-100 px-3 py-1.5 rounded-xl transition-all cursor-pointer mb-1"
                      >
                        <ArrowLeft size={14} />
                        <span>Back to Sprint List</span>
                      </button>

                      <div className="flex items-center gap-2.5 flex-wrap">
                        <h2 className="text-xl font-bold text-gray-900 tracking-tight">
                          {selectedSprintForTasks.name}
                        </h2>
                        <span
                          className={`px-2.5 py-0.5 text-[10px] font-semibold rounded-full ${
                            selectedSprintForTasks.status === 'ACTIVE'
                              ? 'bg-emerald-100 text-emerald-800 border border-emerald-200'
                              : selectedSprintForTasks.status === 'COMPLETED'
                              ? 'bg-gray-100 text-gray-600'
                              : 'bg-blue-50 text-blue-700 border border-blue-100'
                          }`}
                        >
                          {selectedSprintForTasks.status === 'ACTIVE'
                            ? 'Active'
                            : selectedSprintForTasks.status === 'COMPLETED'
                            ? 'Completed'
                            : 'Planned'}
                        </span>
                      </div>

                      <p className="text-xs text-gray-500 max-w-xl">
                        {selectedSprintForTasks.goal || 'Goal: No specific goal notes provided'}
                      </p>

                      <div className="flex items-center gap-3 text-xs text-gray-400 pt-1">
                        <span className="flex items-center gap-1 text-gray-500">
                          <Calendar size={13} className="text-gray-400" />
                          {selectedSprintForTasks.startDate} → {selectedSprintForTasks.endDate}
                        </span>
                        <span>•</span>
                        <span>
                          Total: <strong className="text-gray-700">{sprintTasks.length} tasks</strong>
                        </span>
                      </div>
                    </div>

                    {/* Sprint Header Actions */}
                    <div className="flex items-center gap-2 shrink-0">
                      {isPM ? (
                        <button
                          onClick={() => {
                            setEditingSprint(selectedSprintForTasks);
                            setIsEditSprintOpen(true);
                          }}
                          className="px-3.5 py-2 bg-blue-50 hover:bg-blue-100 text-blue-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer border border-blue-200"
                          title="Edit Sprint (PM Only)"
                        >
                          <Edit3 size={14} />
                          <span>Edit Sprint</span>
                        </button>
                      ) : (
                        <div
                          className="px-3 py-2 bg-gray-50 text-gray-400 rounded-xl text-xs font-medium flex items-center gap-1.5 cursor-not-allowed border border-gray-200"
                          title="Only PMs can edit sprints"
                        >
                          <Lock size={13} />
                          <span>PM Only</span>
                        </div>
                      )}

                      <button
                        onClick={() => {
                          setCreateTaskSprintId(selectedSprintForTasks.id);
                          setIsCreateTaskOpen(true);
                        }}
                        className="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 shadow-xs transition-all cursor-pointer"
                      >
                        <Plus size={15} />
                        <span>Add Task to Sprint</span>
                      </button>
                    </div>
                  </div>

                  {/* Filter Bar: Task ID, Member, Status */}
                  <div className="grid grid-cols-1 sm:grid-cols-12 gap-3 p-4 bg-gray-50/70 rounded-2xl border border-gray-100">
                    {/* Filter by task ID / task title */}
                    <div className="sm:col-span-5 relative">
                      <Search size={15} className="absolute left-3.5 top-2.5 text-gray-400" />
                      <input
                        type="text"
                        value={sprintTaskSearch}
                        onChange={(e) => setSprintTaskSearch(e.target.value)}
                        placeholder="Filter by task ID (e.g., #1, TASK-1) or title..."
                        className="w-full pl-9 pr-3 py-2 bg-white border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500 text-gray-800"
                      />
                    </div>

                    {/* Filter by member */}
                    <div className="sm:col-span-4">
                      <select
                        value={sprintMemberFilter}
                        onChange={(e) => setSprintMemberFilter(e.target.value)}
                        className="w-full px-3 py-2 bg-white border border-gray-200 rounded-xl text-xs font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 cursor-pointer"
                      >
                        <option value="ALL">All Members</option>
                        {members.map((m) => (
                          <option key={m.userId} value={m.userId}>
                            {m.userName || `User #${m.userId}`} ({m.roleName || 'Member'})
                          </option>
                        ))}
                        <option value="UNASSIGNED">Unassigned</option>
                      </select>
                    </div>

                    {/* Filter by status */}
                    <div className="sm:col-span-3">
                      <select
                        value={sprintStatusFilter}
                        onChange={(e) => setSprintStatusFilter(e.target.value)}
                        className="w-full px-3 py-2 bg-white border border-gray-200 rounded-xl text-xs font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 cursor-pointer"
                      >
                        <option value="ALL">All Statuses</option>
                        <option value="TO_DO">To Do</option>
                        <option value="IN_PROGRESS">In Progress</option>
                        <option value="DONE">Done</option>
                        <option value="CLOSED">Closed</option>
                      </select>
                    </div>
                  </div>

                  {/* Sprint Tasks List */}
                  <div className="space-y-3">
                    <div className="flex items-center justify-between text-xs text-gray-500 px-1">
                      <span>
                        Showing <strong>{filteredSprintTasks.length}</strong> / {sprintTasks.length} tasks
                      </span>
                      {sprintTaskSearch && (
                        <button
                          onClick={() => setSprintTaskSearch('')}
                          className="text-emerald-700 hover:underline cursor-pointer"
                        >
                          Clear filter
                        </button>
                      )}
                    </div>

                    {filteredSprintTasks.length === 0 ? (
                      <div className="py-12 text-center text-xs text-gray-400 space-y-3 border-2 border-dashed border-gray-100 rounded-2xl">
                        <Layers size={28} className="mx-auto text-gray-300" />
                        <p>
                          {sprintTasks.length === 0
                            ? 'There are currently no tasks in this sprint.'
                            : 'No tasks found matching the selected filters.'}
                        </p>
                        <button
                          onClick={() => {
                            setCreateTaskSprintId(selectedSprintForTasks.id);
                            setIsCreateTaskOpen(true);
                          }}
                          className="px-3.5 py-1.5 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 rounded-xl text-xs font-semibold cursor-pointer"
                        >
                          + Assign Task to Sprint
                        </button>
                      </div>
                    ) : (
                      <div className="divide-y divide-gray-100">
                        {filteredSprintTasks.map((task) => {
                          const assignee = members.find((m) => m.userId === task.assigneeId);
                          const isDone = normalizeStatus(task.status) === 'DONE';
                          const isInProgress = normalizeStatus(task.status) === 'IN_PROGRESS';
                          const isClosed = normalizeStatus(task.status) === 'CLOSED';
                          const isExpanded = !!expandedTaskIds[task.id];

                          return (
                            <div
                              key={task.id}
                              className="py-4 px-3 rounded-2xl hover:bg-gray-50/70 transition-colors space-y-3"
                            >
                              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                                <div className="min-w-0 space-y-1">
                                  <div className="flex items-center gap-2 flex-wrap">
                                    {/* Task ID */}
                                    <span
                                      onClick={() => handleOpenTaskDetail(task)}
                                      className="px-2 py-0.5 text-[10px] font-mono font-bold rounded-md bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-200 cursor-pointer transition-colors"
                                      title="View task details"
                                    >
                                      #{task.id}
                                    </span>

                                    <span
                                      onClick={() => handleOpenTaskDetail(task)}
                                      className={`text-xs font-bold cursor-pointer hover:text-emerald-700 transition-colors ${isDone || isClosed ? 'line-through text-gray-400' : 'text-gray-900'}`}
                                      title="Click to view details"
                                    >
                                      {task.title}
                                    </span>

                                    <span
                                      className={`px-2 py-0.5 text-[10px] font-semibold rounded-md border ${
                                        task.priority === 'HIGH'
                                          ? 'bg-red-50 text-red-700 border-red-200'
                                          : task.priority === 'MEDIUM'
                                          ? 'bg-amber-50 text-amber-700 border-amber-200'
                                          : 'bg-blue-50 text-blue-700 border-blue-200'
                                      }`}
                                    >
                                      {task.priority || 'MEDIUM'}
                                    </span>
                                  </div>

                                  {task.description && (
                                    <p className="text-xs text-gray-500 line-clamp-2">
                                      {task.description}
                                    </p>
                                  )}

                                  <div className="flex items-center gap-4 text-[11px] text-gray-400 pt-0.5">
                                    {task.dueDate && (
                                      <span className="flex items-center gap-1">
                                        <Calendar size={12} />
                                        Due: {task.dueDate}
                                      </span>
                                    )}

                                    {/* Toggle Subtasks */}
                                    <button
                                      type="button"
                                      onClick={() => toggleTaskSubtasks(task.id)}
                                      className="text-emerald-700 hover:text-emerald-800 font-semibold flex items-center gap-1 cursor-pointer"
                                    >
                                      <ListChecks size={13} />
                                      <span>Subtasks</span>
                                      {isExpanded ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
                                    </button>
                                  </div>
                                </div>

                                <div className="flex items-center gap-3 shrink-0 self-end sm:self-center">
                                  {/* Assignee */}
                                  {assignee || task.assigneeName ? (
                                    <div className="flex items-center gap-2 bg-gray-50 border border-gray-200 px-2.5 py-1 rounded-xl text-xs">
                                      <div className="w-6 h-6 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-[10px]">
                                        {(task.assigneeName || assignee?.userName || 'U').substring(0, 2).toUpperCase()}
                                      </div>
                                      <div className="text-left">
                                        <div className="text-[11px] font-semibold text-gray-800 leading-tight">
                                          {task.assigneeName || assignee?.userName}
                                        </div>
                                        <div className="text-[9px] text-gray-400 leading-tight">
                                          {assignee?.roleName || 'Member'}
                                        </div>
                                      </div>
                                    </div>
                                  ) : (
                                    <span className="px-2.5 py-1 text-[11px] rounded-lg bg-gray-100 text-gray-500 italic">
                                      Unassigned
                                    </span>
                                  )}

                                  {/* Task Status */}
                                  <select
                                    value={task.status}
                                    disabled={!canChangeTaskStatus(task)}
                                    title={!canChangeTaskStatus(task) ? 'Only PM or the assignee can change task status' : undefined}
                                    onChange={(e) => handleUpdateTaskStatus(task.id, e.target.value)}
                                    className={`px-2.5 py-1 text-[11px] font-semibold rounded-lg border focus:outline-none ${
                                      !canChangeTaskStatus(task) ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'
                                    } ${
                                      isDone
                                        ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                                        : isInProgress
                                        ? 'bg-blue-50 text-blue-700 border-blue-200'
                                        : isClosed
                                        ? 'bg-gray-100 text-gray-500 border-gray-300'
                                        : 'bg-amber-50 text-amber-700 border-amber-200'
                                    }`}
                                  >
                                    <option value="TO_DO">To Do</option>
                                    <option value="IN_PROGRESS">In Progress</option>
                                    <option value="DONE">Done</option>
                                    <option value="CLOSED">Closed</option>
                                  </select>

                                  {/* View Detail */}
                                  <button
                                    type="button"
                                    onClick={() => handleOpenTaskDetail(task)}
                                    className="p-1.5 text-gray-400 hover:text-emerald-700 hover:bg-emerald-50 rounded-lg transition-colors cursor-pointer"
                                    title="View task details"
                                  >
                                    <Eye size={15} />
                                  </button>

                                  {/* Edit Task */}
                                  <button
                                    type="button"
                                    onClick={() => handleOpenEditTask(task)}
                                    className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors cursor-pointer"
                                    title="Edit task"
                                  >
                                    <Edit3 size={15} />
                                  </button>

                                  {/* Delete Task (PM Only) */}
                                  {isPM && (
                                    <button
                                      onClick={() => handleDeleteTask(task.id, task.title)}
                                      className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors cursor-pointer"
                                      title="Move to trash (PM only)"
                                    >
                                      <Trash2 size={15} />
                                    </button>
                                  )}
                                </div>
                              </div>

                              {/* Subtasks area */}
                              {isExpanded && (
                                <div className="pt-2 pl-4 animate-in fade-in">
                                  <SubtaskList
                                    taskId={task.id}
                                    members={members}
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
                </div>
              ) : (
                /* SPRINT LIST OVERVIEW */
                <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-6">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    <div>
                      <h2 className="text-lg font-bold text-gray-900">Sprint List</h2>
                      <p className="text-xs text-gray-500 mt-0.5">
                        Plan and track development cycles. Click on a Sprint to view its tasks.
                      </p>
                    </div>

                    {selectedProjectId && (
                      isPM ? (
                        <button
                          onClick={() => setIsCreateSprintOpen(true)}
                          className="px-4 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-all cursor-pointer shrink-0"
                        >
                          <Plus size={16} />
                          <span>Create New Sprint</span>
                        </button>
                      ) : (
                        <div
                          className="px-3.5 py-2 bg-gray-50 text-gray-400 rounded-xl text-xs font-medium flex items-center gap-1.5 cursor-not-allowed border border-gray-200 shrink-0"
                          title="Only PM can create sprints"
                        >
                          <Lock size={14} />
                          <span>PM Only</span>
                        </div>
                      )
                    )}
                  </div>

                  {loadingDetails ? (
                    <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
                      <Loader2 size={24} className="animate-spin text-emerald-600" />
                      <span>Loading sprint list...</span>
                    </div>
                  ) : sprints.length === 0 ? (
                    <div className="py-12 text-center text-xs text-gray-400 space-y-3 border-2 border-dashed border-gray-100 rounded-2xl">
                      <p>No sprints have been created for this project yet.</p>
                      {isPM && (
                        <button
                          onClick={() => setIsCreateSprintOpen(true)}
                          className="px-3.5 py-1.5 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 rounded-xl text-xs font-semibold cursor-pointer"
                        >
                          + Click here to create the first sprint
                        </button>
                      )}
                    </div>
                  ) : (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      {sprints.map((sprint) => {
                        const isActive = sprint.status === 'ACTIVE';
                        const isCompleted = sprint.status === 'COMPLETED';
                        const countInSprint = tasks.filter((t) => t.sprintId === sprint.id).length;

                        return (
                          <div
                            key={sprint.id}
                            onClick={() => setSelectedSprintForTasks(sprint)}
                            className="p-5 rounded-2xl border border-gray-100 hover:border-emerald-300 bg-white hover:shadow-md transition-all space-y-4 cursor-pointer group flex flex-col justify-between"
                          >
                            <div className="space-y-2">
                              <div className="flex items-start justify-between gap-3">
                                <div>
                                  <h3 className="text-sm font-bold text-gray-900 group-hover:text-emerald-700 transition-colors">
                                    {sprint.name}
                                  </h3>
                                  <p className="text-xs text-gray-500 mt-1 line-clamp-2">
                                    {sprint.goal || 'No specific goal provided'}
                                  </p>
                                </div>
                                <span
                                  className={`px-2.5 py-0.5 text-[10px] font-semibold rounded-full shrink-0 ${
                                    isActive
                                      ? 'bg-emerald-100 text-emerald-800 border border-emerald-200'
                                      : isCompleted
                                      ? 'bg-gray-100 text-gray-600'
                                      : 'bg-blue-50 text-blue-700 border border-blue-100'
                                  }`}
                                >
                                  {isActive ? 'Active' : isCompleted ? 'Completed' : 'Planned'}
                                </span>
                              </div>

                              <div className="flex items-center gap-2 pt-1">
                                <span className="px-2 py-0.5 rounded-md text-[10px] font-semibold bg-gray-100 text-gray-700">
                                  {countInSprint} tasks
                                </span>
                                <span className="text-[11px] text-emerald-700 font-semibold group-hover:underline flex items-center gap-0.5">
                                  <span>View tasks</span>
                                  <ArrowRight size={12} />
                                </span>
                              </div>
                            </div>

                            <div className="pt-3 border-t border-gray-50 flex items-center justify-between text-[11px] text-gray-500">
                              <span className="flex items-center gap-1">
                                <Calendar size={13} className="text-gray-400" />
                                {sprint.startDate} → {sprint.endDate}
                              </span>

                              {/* Sprint Actions: PM only */}
                              <div className="flex items-center gap-1" onClick={(e) => e.stopPropagation()}>
                                {isPM ? (
                                  <>
                                    <button
                                      onClick={() => {
                                        setEditingSprint(sprint);
                                        setIsEditSprintOpen(true);
                                      }}
                                      className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors cursor-pointer"
                                      title="Edit Sprint (PM only)"
                                    >
                                      <Edit3 size={14} />
                                    </button>
                                    <button
                                      onClick={() => handleDeleteSprint(sprint)}
                                      className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors cursor-pointer"
                                      title="Delete Sprint"
                                    >
                                      <Trash2 size={14} />
                                    </button>
                                  </>
                                ) : (
                                  <span
                                    className="p-1.5 text-gray-300 rounded-lg cursor-not-allowed"
                                    title="Only PM can edit sprints"
                                  >
                                    <Lock size={14} />
                                  </span>
                                )}
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {/* TAB 2: MEMBERS & PERMISSIONS */}
          {activeTab === 'members' && (
            <div className="space-y-6">
              <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-6">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Members & Roles</h2>
                    <p className="text-xs text-gray-500 mt-0.5">
                      Add members to the project and assign specialized roles.
                    </p>
                  </div>
                  {selectedProjectId && (
                    <button
                      onClick={() => setIsAddMemberOpen(true)}
                      className="px-4 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-all cursor-pointer shrink-0"
                    >
                      <UserPlus size={16} />
                      <span>Add Member</span>
                    </button>
                  )}
                </div>

                {loadingDetails ? (
                  <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
                    <Loader2 size={24} className="animate-spin text-emerald-600" />
                    <span>Loading members list...</span>
                  </div>
                ) : members.length === 0 ? (
                  <div className="py-12 text-center text-xs text-gray-400">
                    No members in this project yet.
                  </div>
                ) : (
                  <div className="divide-y divide-gray-100">
                    {members.map((member) => {
                      const isTargetPMOrSelf = (member.roleName || '').toUpperCase() === 'PROJECT_MANAGER' || 
                                               member.userId === user?.id || 
                                               (currentProject && member.userId === currentProject.ownerId);
                      return (
                        <div
                          key={`${member.projectId}-${member.userId}`}
                          className="py-4 flex items-center justify-between gap-4 group hover:bg-gray-50/60 px-3 rounded-xl transition-colors"
                        >
                          <div className="flex items-center gap-3 min-w-0">
                            <div className="w-10 h-10 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-sm shrink-0">
                              {(member.userName || 'U').substring(0, 2).toUpperCase()}
                            </div>
                            <div className="min-w-0">
                              <div className="flex items-center gap-2">
                                <p className="text-xs font-bold text-gray-900 truncate">
                                  {member.userName || `Member #${member.userId}`}
                                </p>
                                {member.userId === user?.id && (
                                  <span className="text-[10px] text-gray-400 font-normal">
                                    (You)
                                  </span>
                                )}
                              </div>
                              <p className="text-[11px] text-gray-400 truncate">
                                {member.userEmail || `User ID: ${member.userId}`}
                              </p>
                            </div>
                          </div>

                          <div className="flex items-center gap-3 shrink-0">
                            <span
                              className={`px-3 py-1 text-[11px] font-bold rounded-lg border flex items-center gap-1.5 ${
                                member.roleName === 'PROJECT_MANAGER'
                                    ? 'bg-purple-50 text-purple-700 border-purple-200'
                                  : member.roleName === 'DEVELOPER'
                                  ? 'bg-blue-50 text-blue-700 border-blue-200'
                                  : member.roleName === 'TESTER'
                                  ? 'bg-amber-50 text-amber-700 border-amber-200'
                                  : 'bg-emerald-50 text-emerald-700 border-emerald-200'
                              }`}
                            >
                              <ShieldCheck size={13} />
                              <span>{member.roleName || 'MEMBER'}</span>
                            </span>

                            {isPM && !isTargetPMOrSelf && (
                              <button
                                onClick={() => handleRemoveMember(member.userId, member.userName)}
                                className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors cursor-pointer"
                                title="Remove from project"
                              >
                                <Trash2 size={15} />
                              </button>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* TAB 3: CURRENT SPRINT & BURNDOWN */}
          {activeTab === 'currentSprint' && (
            <div className="space-y-6">
              {!currentSprint ? (
                <div className="bg-white rounded-3xl p-12 text-center border border-gray-100 shadow-xs space-y-4">
                  <div className="w-16 h-16 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center mx-auto">
                    <Layers size={32} />
                  </div>
                  <div>
                    <h3 className="text-lg font-bold text-gray-900">Project has no Sprints yet</h3>
                    <p className="text-xs text-gray-500 mt-1 max-w-md mx-auto">
                      Create a Sprint to organize work cycles, manage sprints, and track progress with the Burndown chart.
                    </p>
                  </div>
                  <button
                    onClick={() => setIsCreateSprintOpen(true)}
                    className="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold inline-flex items-center gap-2 shadow-xs transition-all cursor-pointer"
                  >
                    <Plus size={16} />
                    <span>Create First Sprint</span>
                  </button>
                </div>
              ) : (
                <>
                  {/* Sprint Header & Selector Bar */}
                  <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0">
                        <Layers size={20} />
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h2 className="text-base font-bold text-gray-900">{currentSprint.name}</h2>
                          <span
                            className={`px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider ${
                              currentSprint.status === 'ACTIVE'
                                ? 'bg-emerald-100 text-emerald-800'
                                : currentSprint.status === 'COMPLETED'
                                ? 'bg-gray-100 text-gray-700'
                                : 'bg-blue-100 text-blue-800'
                            }`}
                          >
                            {currentSprint.status === 'ACTIVE'
                              ? 'Active'
                              : currentSprint.status === 'COMPLETED'
                              ? 'Completed'
                              : 'Planned'}
                          </span>
                        </div>
                        <p className="text-xs text-gray-500 mt-0.5">
                          {currentSprint.goal || 'No specific goal set'}
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2.5 shrink-0">
                      {sprints.length > 1 && (
                        <div className="flex items-center gap-1.5 text-xs text-gray-600">
                          <span className="text-[11px] text-gray-400 font-medium">Select sprint:</span>
                          <select
                            value={currentSprint.id}
                            onChange={(e) => setSelectedBurndownSprintId(Number(e.target.value))}
                            className="px-3 py-1.5 bg-gray-50 border border-gray-200 rounded-xl text-xs font-semibold text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 cursor-pointer"
                          >
                            {sprints.map((sp) => (
                              <option key={sp.id} value={sp.id}>
                                {sp.name} ({sp.status === 'ACTIVE' ? 'Active' : sp.status === 'COMPLETED' ? 'Completed' : 'Planned'})
                              </option>
                            ))}
                          </select>
                        </div>
                      )}

                      <button
                        onClick={() => {
                          setCreateTaskSprintId(currentSprint.id);
                          setIsCreateTaskOpen(true);
                        }}
                        className="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 shadow-xs transition-all cursor-pointer"
                      >
                        <Plus size={14} />
                        <span>Add Task to Sprint</span>
                      </button>
                    </div>
                  </div>

                  {/* Metric KPI Cards */}
                  <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                    <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
                      <div className="text-xl sm:text-2xl font-bold text-gray-900 truncate">{currentSprint.name}</div>
                      <div className="text-[11px] text-gray-500 mt-1">{sprintDurationText}</div>
                    </div>
                    <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
                      <div className={`text-xl sm:text-2xl font-bold ${
                        currentSprintProgress >= 70
                          ? 'text-emerald-600'
                          : currentSprintProgress >= 30
                          ? 'text-amber-600'
                          : 'text-blue-600'
                      }`}>
                        {currentSprintProgress}%
                      </div>
                      <div className="text-[11px] text-gray-500 mt-1">
                        Progress ({currentSprintDoneTasks.length}/{currentSprintTotalTasks} tasks)
                      </div>
                    </div>
                    <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
                      <div className="text-xl sm:text-2xl font-bold text-gray-900">{currentSprintTotalTasks}</div>
                      <div className="text-[11px] text-gray-500 mt-1">
                        To Do: {currentSprintTodoTasks.length} · In Progress: {currentSprintInProgressTasks.length} · Done: {currentSprintDoneTasks.length}
                      </div>
                    </div>
                    <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
                      <div className={`text-xl sm:text-2xl font-bold ${
                        isSprintOverdue
                          ? 'text-red-600'
                          : daysRemainingCount !== null && daysRemainingCount <= 3
                          ? 'text-amber-600'
                          : 'text-gray-900'
                      }`}>
                        {daysRemainingText}
                      </div>
                      <div className="text-[11px] text-gray-500 mt-1">
                        Deadline: {currentSprint.endDate || 'Not set'}
                      </div>
                    </div>
                  </div>

                  {/* Burndown Chart Card */}
                  <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-6">
                    <div className="flex items-center justify-between">
                      <div>
                        <h3 className="text-sm font-bold text-gray-900">Burndown Chart — Actual Completion Progress</h3>
                        <p className="text-xs text-gray-400 mt-0.5">
                          Remaining tasks over the sprint cycle for {currentSprint.name}
                        </p>
                      </div>
                      <button
                        onClick={() => setActiveTab('sprints')}
                        className="px-4 py-1.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-full text-xs font-semibold shadow-xs cursor-pointer transition-colors"
                      >
                        View all sprints
                      </button>
                    </div>

                    {currentSprintTotalTasks === 0 ? (
                      <div className="py-12 text-center border border-dashed border-gray-200 rounded-2xl space-y-3">
                        <div className="w-12 h-12 rounded-xl bg-gray-50 text-gray-400 flex items-center justify-center mx-auto">
                          <CheckCircle2 size={24} />
                        </div>
                        <div>
                          <p className="text-xs font-semibold text-gray-700">This sprint has no tasks yet</p>
                          <p className="text-[11px] text-gray-400 mt-0.5">
                            Assign tasks to this sprint to track the Burndown chart.
                          </p>
                        </div>
                        <button
                          onClick={() => {
                            setCreateTaskSprintId(currentSprint.id);
                            setIsCreateTaskOpen(true);
                          }}
                          className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold inline-flex items-center gap-1.5 transition-all cursor-pointer"
                        >
                          <Plus size={14} />
                          <span>Assign Task Now</span>
                        </button>
                      </div>
                    ) : (
                      <>
                        <div className="h-48 flex items-end justify-between gap-1.5 sm:gap-3 px-2 pt-6">
                          {burndownChartBars.map((bar) => (
                            <div key={bar.day} className="flex-1 flex flex-col items-center gap-2 h-full justify-end group relative">
                              {/* Hover Tooltip */}
                              <div className="opacity-0 group-hover:opacity-100 transition-opacity absolute -top-8 px-2 py-1 bg-gray-900 text-white text-[10px] font-medium rounded-md whitespace-nowrap pointer-events-none z-10 shadow-md">
                                Day {bar.day}: {bar.remaining} tasks remaining
                              </div>

                              <div
                                style={{ height: bar.height }}
                                className={`w-full max-w-[42px] rounded-lg transition-all duration-300 ${
                                  bar.isHighlight
                                    ? 'bg-emerald-600 shadow-md ring-2 ring-emerald-600/30'
                                    : 'bg-emerald-200/90 hover:bg-emerald-300'
                                }`}
                              ></div>
                              <span className={`text-[10px] font-medium ${
                                bar.isHighlight ? 'text-emerald-700 font-bold' : 'text-gray-400'
                              }`}>
                                D{bar.day}
                              </span>
                            </div>
                          ))}
                        </div>

                        {/* Breakdown pills & conclusion */}
                        <div className="pt-4 border-t border-gray-100 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="px-2.5 py-1 bg-gray-100 text-gray-700 rounded-lg font-medium text-[11px]">
                              To Do: <strong className="text-gray-900">{currentSprintTodoTasks.length}</strong>
                            </span>
                            <span className="px-2.5 py-1 bg-blue-50 text-blue-700 rounded-lg font-medium text-[11px]">
                              In Progress: <strong className="text-blue-900">{currentSprintInProgressTasks.length}</strong>
                            </span>
                            <span className="px-2.5 py-1 bg-emerald-50 text-emerald-700 rounded-lg font-medium text-[11px]">
                              Done: <strong className="text-emerald-900">{currentSprintDoneTasks.length}</strong>
                            </span>
                          </div>

                          <div className="text-gray-500 font-medium text-right">
                            {currentSprintRemainingTasks === 0 ? (
                              <span className="text-emerald-700 font-semibold">
                                🎉 Great job! All tasks in this sprint are completed.
                              </span>
                            ) : isSprintOverdue ? (
                              <span className="text-red-600 font-semibold">
                                ⚠️ Sprint is overdue with {currentSprintRemainingTasks} incomplete tasks remaining.
                              </span>
                            ) : (
                              <span>
                                <strong className="text-gray-900 font-semibold">{currentSprintRemainingTasks} tasks</strong> remaining ({currentSprintProgress}% completed).
                              </span>
                            )}
                          </div>
                        </div>
                      </>
                    )}
                  </div>

                  {/* Sprint Tasks List */}
                  <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-4">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <CheckCircle2 size={18} className="text-emerald-600" />
                        <h3 className="text-sm font-bold text-gray-900">
                          Tasks in {currentSprint.name} ({currentSprintTasks.length})
                        </h3>
                      </div>
                      <button
                        onClick={() => {
                          setCreateTaskSprintId(currentSprint.id);
                          setIsCreateTaskOpen(true);
                        }}
                        className="px-3 py-1.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-800 rounded-xl text-xs font-semibold flex items-center gap-1 transition-all cursor-pointer shadow-2xs"
                      >
                        <Plus size={14} />
                        <span>+ Add Task</span>
                      </button>
                    </div>

                    {currentSprintTasks.length === 0 ? (
                      <div className="py-6 text-center text-xs text-gray-400">
                        No tasks in this sprint yet.
                      </div>
                    ) : (
                      <div className="space-y-2">
                        {currentSprintTasks.map((t) => (
                          <div
                            key={t.id}
                            className="p-3.5 bg-gray-50/70 hover:bg-emerald-50/40 rounded-2xl border border-gray-100 transition-all flex items-center justify-between gap-3 group"
                          >
                            <div
                              className="flex items-center gap-3 min-w-0 flex-1 cursor-pointer"
                              onClick={() => handleOpenTaskDetail(t)}
                            >
                              <div className="w-8 h-8 rounded-xl bg-white border border-gray-200 flex items-center justify-center shrink-0 text-gray-500 font-mono text-[10px] font-bold">
                                #{t.id}
                              </div>
                              <div className="min-w-0 flex-1">
                                <div className="flex items-center gap-2">
                                  <span className="text-xs font-bold text-gray-900 group-hover:text-emerald-700 transition-colors truncate">
                                    {t.title}
                                  </span>
                                  {t.taskCode && (
                                    <span className="text-[10px] font-mono text-gray-400">
                                      [{t.taskCode}]
                                    </span>
                                  )}
                                </div>
                                <div className="flex flex-wrap items-center gap-2 mt-1 text-[11px] text-gray-500">
                                  {t.assigneeName && (
                                    <span className="text-gray-600">👤 {t.assigneeName}</span>
                                  )}
                                  {t.estimate && (
                                    <span className="text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded font-medium">
                                      ⏱️ {t.estimate}
                                    </span>
                                  )}
                                  {t.dueDate && (
                                    <span>📅 {t.dueDate}</span>
                                  )}
                                </div>
                              </div>
                            </div>

                            <div className="flex items-center gap-2 shrink-0">
                              <span
                                className={`px-2.5 py-1 rounded-xl text-[10px] font-semibold ${
                                  t.status === 'DONE'
                                    ? 'bg-emerald-100 text-emerald-800'
                                    : t.status === 'IN_PROGRESS'
                                    ? 'bg-blue-100 text-blue-800'
                                    : t.status === 'CLOSED'
                                    ? 'bg-gray-200 text-gray-700'
                                    : 'bg-amber-100 text-amber-800'
                                }`}
                              >
                                {t.status === 'DONE'
                                  ? 'Done'
                                  : t.status === 'IN_PROGRESS'
                                  ? 'In Progress'
                                  : t.status === 'CLOSED'
                                  ? 'Closed'
                                  : 'To Do'}
                              </span>

                              <button
                                onClick={() => handleOpenEditTask(t)}
                                className="p-1.5 text-gray-400 hover:text-emerald-700 hover:bg-emerald-50 rounded-lg transition-colors cursor-pointer"
                                title="Edit task"
                              >
                                <Edit3 size={15} />
                              </button>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </>
              )}
            </div>
          )}

          {/* TAB 4: TASK ASSIGNMENTS */}
          {activeTab === 'assignments' && (
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-6">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div>
                  <h2 className="text-lg font-bold text-gray-900">Task Assignments</h2>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Assign tasks directly to project members and track implementation progress.
                  </p>
                </div>
                {selectedProjectId && (
                  <button
                    onClick={() => {
                      setCreateTaskSprintId(null);
                      setIsCreateTaskOpen(true);
                    }}
                    className="px-4 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-2 shadow-xs transition-all cursor-pointer shrink-0"
                  >
                    <Plus size={16} />
                    <span>Create New Task</span>
                  </button>
                )}
              </div>

              {/* Assignment Filters */}
              <div className="p-4 bg-gray-50/70 rounded-2xl border border-gray-100 space-y-3">
                <div className="flex items-center gap-2 text-xs font-bold text-gray-700">
                  <Filter size={14} className="text-emerald-600" />
                  <span>Advanced Filters</span>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                  {/* 1. Filter by Sprint */}
                  <div>
                    <label className="block text-[11px] font-semibold text-gray-500 mb-1">
                      Filter by Sprint
                    </label>
                    <select
                      value={assignmentSprintFilter}
                      onChange={(e) => setAssignmentSprintFilter(e.target.value)}
                      className="w-full px-3 py-2 bg-white border border-gray-200 rounded-xl text-xs font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 cursor-pointer"
                    >
                      <option value="ALL">All Sprints</option>
                      {sprints.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.name}
                        </option>
                      ))}
                      <option value="NO_SPRINT">No Sprint (Backlog)</option>
                    </select>
                  </div>

                  {/* 2. Filter by Member */}
                  <div>
                    <label className="block text-[11px] font-semibold text-gray-500 mb-1">
                      Filter by Member
                    </label>
                    <select
                      value={assignmentMemberFilter}
                      onChange={(e) => setAssignmentMemberFilter(e.target.value)}
                      className="w-full px-3 py-2 bg-white border border-gray-200 rounded-xl text-xs font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 cursor-pointer"
                    >
                      <option value="ALL">All Members</option>
                      {members.map((m) => (
                        <option key={m.userId} value={m.userId}>
                          {m.userName || `User #${m.userId}`} ({m.roleName || 'Member'})
                        </option>
                      ))}
                      <option value="UNASSIGNED">Unassigned</option>
                    </select>
                  </div>

                  {/* 3. Filter by Status */}
                  <div>
                    <label className="block text-[11px] font-semibold text-gray-500 mb-1">
                      Status
                    </label>
                    <select
                      value={assignmentStatusFilter}
                      onChange={(e) => setAssignmentStatusFilter(e.target.value)}
                      className="w-full px-3 py-2 bg-white border border-gray-200 rounded-xl text-xs font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 cursor-pointer"
                    >
                      <option value="ALL">All Statuses</option>
                      <option value="TO_DO">To Do</option>
                      <option value="IN_PROGRESS">In Progress</option>
                      <option value="DONE">Done</option>
                      <option value="CLOSED">Closed</option>
                    </select>
                  </div>

                  {/* 4. Filter by Task ID / Title */}
                  <div>
                    <label className="block text-[11px] font-semibold text-gray-500 mb-1">
                      Task ID or title
                    </label>
                    <div className="relative">
                      <Search size={14} className="absolute left-3 top-2.5 text-gray-400" />
                      <input
                        type="text"
                        value={assignmentSearchQuery}
                        onChange={(e) => setAssignmentSearchQuery(e.target.value)}
                        placeholder="Search #1, TASK-1..."
                        className="w-full pl-8 pr-3 py-2 bg-white border border-gray-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500/20 text-gray-800"
                      />
                    </div>
                  </div>
                </div>
              </div>

              {loadingDetails ? (
                <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
                  <Loader2 size={24} className="animate-spin text-emerald-600" />
                  <span>Loading task list...</span>
                </div>
              ) : filteredAssignmentsTasks.length === 0 ? (
                <div className="py-12 text-center text-xs text-gray-400 space-y-3 border-2 border-dashed border-gray-100 rounded-2xl">
                  <p>
                    {tasks.length === 0
                      ? 'No tasks assigned in this project yet.'
                      : 'No tasks match the filters.'}
                  </p>
                  <button
                    onClick={() => {
                      setCreateTaskSprintId(null);
                      setIsCreateTaskOpen(true);
                    }}
                    className="px-3.5 py-1.5 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 rounded-xl text-xs font-semibold cursor-pointer"
                  >
                    + Click here to create the first task
                  </button>
                </div>
              ) : (
                <div className="divide-y divide-gray-100">
                  {filteredAssignmentsTasks.map((task) => {
                    const assignee = members.find((m) => m.userId === task.assigneeId);
                    const sprint = sprints.find((s) => s.id === task.sprintId);
                    const isDone = normalizeStatus(task.status) === 'DONE';
                    const isInProgress = normalizeStatus(task.status) === 'IN_PROGRESS';
                    const isClosed = normalizeStatus(task.status) === 'CLOSED';
                    const isExpanded = !!expandedTaskIds[task.id];

                    return (
                      <div
                        key={task.id}
                        className="py-4 px-3 rounded-2xl hover:bg-gray-50/70 transition-colors space-y-3"
                      >
                        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                          <div className="min-w-0 space-y-1">
                            <div className="flex items-center gap-2 flex-wrap">
                              {/* Task ID */}
                              <span
                                onClick={() => handleOpenTaskDetail(task)}
                                className="px-2 py-0.5 text-[10px] font-mono font-bold rounded-md bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-200 cursor-pointer transition-colors"
                                title="View task details"
                              >
                                #{task.id}
                              </span>

                              <span
                                onClick={() => handleOpenTaskDetail(task)}
                                className={`text-xs font-bold cursor-pointer hover:text-emerald-700 transition-colors ${isDone || isClosed ? 'line-through text-gray-400' : 'text-gray-900'}`}
                                title="Click to view details"
                              >
                                {task.title}
                              </span>

                              {sprint && (
                                <span className="px-2 py-0.5 text-[10px] font-semibold rounded-md bg-purple-50 text-purple-700 border border-purple-100">
                                  {sprint.name}
                                </span>
                              )}

                              <span
                                className={`px-2 py-0.5 text-[10px] font-semibold rounded-md border ${
                                  task.priority === 'HIGH'
                                    ? 'bg-red-50 text-red-700 border-red-200'
                                    : task.priority === 'MEDIUM'
                                    ? 'bg-amber-50 text-amber-700 border-amber-200'
                                    : 'bg-blue-50 text-blue-700 border-blue-200'
                                }`}
                              >
                                {task.priority || 'MEDIUM'}
                              </span>
                            </div>

                            {task.description && (
                              <p className="text-xs text-gray-500 line-clamp-2">
                                {task.description}
                              </p>
                            )}

                            <div className="flex items-center gap-4 text-[11px] text-gray-400 pt-0.5">
                              {task.dueDate && (
                                <span className="flex items-center gap-1">
                                  <Calendar size={12} />
                                  Due: {task.dueDate}
                                </span>
                              )}

                              {/* Toggle Subtasks */}
                              <button
                                type="button"
                                onClick={() => toggleTaskSubtasks(task.id)}
                                className="text-emerald-700 hover:text-emerald-800 font-semibold flex items-center gap-1 cursor-pointer"
                              >
                                <ListChecks size={13} />
                                <span>Subtasks</span>
                                {isExpanded ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
                              </button>
                            </div>
                          </div>

                          <div className="flex items-center gap-3 shrink-0 self-end sm:self-center">
                            {/* Assignee */}
                            {assignee || task.assigneeName ? (
                              <div className="flex items-center gap-2 bg-gray-50 border border-gray-200 px-2.5 py-1 rounded-xl text-xs">
                                <div className="w-6 h-6 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-[10px]">
                                  {(task.assigneeName || assignee?.userName || 'U').substring(0, 2).toUpperCase()}
                                </div>
                                <div className="text-left">
                                  <div className="text-[11px] font-semibold text-gray-800 leading-tight">
                                    {task.assigneeName || assignee?.userName}
                                  </div>
                                  <div className="text-[9px] text-gray-400 leading-tight">
                                    {assignee?.roleName || 'Member'}
                                  </div>
                                </div>
                              </div>
                            ) : (
                              <span className="px-2.5 py-1 text-[11px] rounded-lg bg-gray-100 text-gray-500 italic">
                                Unassigned
                              </span>
                            )}

                            {/* Task Status */}
                            <select
                              value={task.status}
                              disabled={!canChangeTaskStatus(task)}
                              title={!canChangeTaskStatus(task) ? 'Only PM or the assignee can change task status' : undefined}
                              onChange={(e) => handleUpdateTaskStatus(task.id, e.target.value)}
                              className={`px-2.5 py-1 text-[11px] font-semibold rounded-lg border focus:outline-none ${
                                !canChangeTaskStatus(task) ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'
                              } ${
                                isDone
                                  ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                                  : isInProgress
                                  ? 'bg-blue-50 text-blue-700 border-blue-200'
                                  : isClosed
                                  ? 'bg-gray-100 text-gray-500 border-gray-300'
                                  : 'bg-amber-50 text-amber-700 border-amber-200'
                              }`}
                            >
                              <option value="TO_DO">To Do</option>
                              <option value="IN_PROGRESS">In Progress</option>
                              <option value="DONE">Done</option>
                              <option value="CLOSED">Closed</option>
                            </select>

                            {/* View Detail */}
                            <button
                              type="button"
                              onClick={() => handleOpenTaskDetail(task)}
                              className="p-1.5 text-gray-400 hover:text-emerald-700 hover:bg-emerald-50 rounded-lg transition-colors cursor-pointer"
                              title="View task details"
                            >
                              <Eye size={15} />
                            </button>

                            {/* Edit Task */}
                            <button
                              type="button"
                              onClick={() => handleOpenEditTask(task)}
                              className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors cursor-pointer"
                              title="Edit task"
                            >
                              <Edit3 size={15} />
                            </button>

                            {/* Delete Task (PM Only) */}
                            {isPM && (
                              <button
                                onClick={() => handleDeleteTask(task.id, task.title)}
                                className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors cursor-pointer"
                                title="Move to trash (PM only)"
                              >
                                <Trash2 size={15} />
                              </button>
                            )}
                          </div>
                        </div>

                        {/* Subtasks area */}
                        {isExpanded && (
                          <div className="pt-2 pl-4 animate-in fade-in">
                            <SubtaskList
                              taskId={task.id}
                              members={members}
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

          {/* TAB 5: TASK TRASH BIN */}
          {activeTab === 'trash' && (
            <div className="bg-white rounded-3xl p-6 border border-gray-100 shadow-xs space-y-6">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-gray-100">
                <div>
                  <div className="flex items-center gap-2">
                    <h2 className="text-lg font-bold text-gray-900">Task Trash Bin</h2>
                    <span className="px-2.5 py-0.5 text-[11px] font-bold rounded-full bg-red-50 text-red-700 border border-red-200">
                      {trashTasks.length} deleted items
                    </span>
                  </div>
                  <p className="text-xs text-gray-500 mt-1">
                    Tasks deleted by you in this project. You can restore them to active status or permanently delete them.
                  </p>
                </div>

                <div className="flex items-center gap-2 shrink-0">
                  {trashTasks.length > 0 && (
                    <button
                      onClick={handleEmptyTrash}
                      disabled={isClearingTrash || loadingTrash}
                      className="px-3.5 py-2 bg-red-50 hover:bg-red-100 text-red-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer border border-red-200"
                      title="Permanently delete all tasks in trash bin"
                    >
                      {isClearingTrash ? (
                        <Loader2 size={14} className="animate-spin" />
                      ) : (
                        <Trash2 size={14} />
                      )}
                      <span>{isClearingTrash ? 'Emptying...' : 'Empty Trash'}</span>
                    </button>
                  )}

                  <button
                    onClick={loadTrashTasks}
                    disabled={loadingTrash || isClearingTrash}
                    className="px-3.5 py-2 bg-gray-50 hover:bg-gray-100 text-gray-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer border border-gray-200"
                  >
                    <RotateCcw size={14} className={loadingTrash ? 'animate-spin' : ''} />
                    <span>Refresh Trash</span>
                  </button>
                </div>
              </div>

              {loadingTrash ? (
                <div className="py-12 flex flex-col items-center justify-center gap-2 text-gray-400 text-xs">
                  <Loader2 size={24} className="animate-spin text-emerald-600" />
                  <span>Loading trash bin...</span>
                </div>
              ) : trashTasks.length === 0 ? (
                <div className="py-16 text-center text-xs text-gray-400 space-y-3 border-2 border-dashed border-gray-100 rounded-2xl">
                  <Trash2 size={36} className="mx-auto text-gray-300" />
                  <p className="text-sm font-medium text-gray-600">Trash Bin is Empty</p>
                  <p className="text-gray-400 max-w-sm mx-auto">
                    You have not deleted any tasks in this project yet.
                  </p>
                </div>
              ) : (
                <div className="divide-y divide-gray-100">
                  {trashTasks.map((task) => {
                    const assignee = members.find((m) => m.userId === task.assigneeId);
                    const sprint = sprints.find((s) => s.id === task.sprintId);

                    return (
                      <div
                        key={task.id}
                        className="py-4 px-3 rounded-2xl hover:bg-gray-50/70 transition-colors flex flex-col sm:flex-row sm:items-center justify-between gap-4"
                      >
                        <div className="min-w-0 space-y-1">
                          <div className="flex items-center gap-2 flex-wrap">
                            <span className="px-2 py-0.5 text-[10px] font-mono font-bold rounded-md bg-gray-100 text-gray-600 border border-gray-200">
                              #{task.id}
                            </span>

                            <span className="text-xs font-bold text-gray-800 line-through">
                              {task.title}
                            </span>

                            {sprint && (
                              <span className="px-2 py-0.5 text-[10px] font-semibold rounded-md bg-gray-100 text-gray-600 border border-gray-200">
                                {sprint.name}
                              </span>
                            )}

                            <span className="px-2 py-0.5 text-[10px] font-semibold rounded-md bg-red-50 text-red-600 border border-red-200">
                              Deleted
                            </span>
                          </div>

                          {task.description && (
                            <p className="text-xs text-gray-400 line-clamp-1">
                              {task.description}
                            </p>
                          )}

                          <div className="flex items-center gap-3 text-[11px] text-gray-400">
                            {task.dueDate && (
                              <span className="flex items-center gap-1">
                                <Calendar size={12} /> Due: {task.dueDate}
                              </span>
                            )}
                            {assignee && (
                              <span>Assignee: {assignee.userName}</span>
                            )}
                            {task.deletedByName && (
                              <span className="text-red-500 font-medium">Deleted by: {task.deletedByName}</span>
                            )}
                          </div>
                        </div>

                        {/* Actions: Restore & Permanently Delete */}
                        <div className="flex items-center gap-2 shrink-0 self-end sm:self-center">
                          <button
                            onClick={() => handleRestoreTask(task.id)}
                            className="px-3.5 py-1.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer border border-emerald-200"
                            title="Restore task"
                          >
                            <RotateCcw size={13} />
                            <span>Restore</span>
                          </button>

                          {isPM && (
                            <button
                              onClick={() => handlePermanentDeleteTask(task.id, task.title)}
                              className="px-3 py-1.5 bg-red-50 hover:bg-red-100 text-red-700 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-all cursor-pointer border border-red-200"
                              title="Permanently delete from database"
                            >
                              <Trash2 size={13} />
                              <span>Permanently Delete</span>
                            </button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Modal Create Sprint (PM only) */}
      {selectedProjectId && (
        <CreateSprintModal
          projectId={selectedProjectId}
          isOpen={isCreateSprintOpen}
          onClose={() => setIsCreateSprintOpen(false)}
          onSuccess={handleSprintCreated}
        />
      )}

      {/* Modal Edit Sprint */}
      <EditSprintModal
        sprint={editingSprint}
        isOpen={isEditSprintOpen}
        isPM={isPM}
        onClose={() => {
          setIsEditSprintOpen(false);
          setEditingSprint(null);
        }}
        onSuccess={handleSprintUpdated}
      />

      {/* Modal Create Task */}
      {selectedProjectId && (
        <CreateTaskModal
          projectId={selectedProjectId}
          members={members}
          sprints={sprints}
          initialSprintId={createTaskSprintId}
          isOpen={isCreateTaskOpen}
          onClose={() => {
            setIsCreateTaskOpen(false);
            setCreateTaskSprintId(null);
          }}
          onSuccess={handleTaskCreated}
        />
      )}

      {/* Modal Add Member */}
      {selectedProjectId && (
        <AddProjectMemberModal
          projectId={selectedProjectId}
          isOpen={isAddMemberOpen}
          onClose={() => setIsAddMemberOpen(false)}
          onSuccess={handleMemberAdded}
          existingMemberUserIds={members.map((m) => m.userId)}
        />
      )}

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
        members={members}
        sprints={sprints}
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
        onDeleteTask={async (taskId) => {
          const t = tasks.find((tk) => tk.id === taskId);
          await handleDeleteTask(taskId, t?.title || 'task');
          setIsDetailModalOpen(false);
        }}
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
        members={members}
        sprints={sprints}
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
          members={members}
          isOpen={isSubtaskFormOpen}
          onClose={() => {
            setIsSubtaskFormOpen(false);
            setSubtaskFormTaskId(null);
            setEditingSubtaskItem(null);
          }}
          onSuccess={(saved) => {
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
