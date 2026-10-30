import type { 
  ApiResponse, 
  AuthResponse, 
  User, 
  PageResponse, 
  Project, 
  ProjectMember, 
  Sprint, 
  Role, 
  CreateProjectPayload, 
  CreateProjectMemberPayload,
  CreateSprintPayload,
  Task,
  CreateTaskPayload,
  CreateSubtaskPayload,
  UpdateSubtaskPayload,
  ActivityLog,
  AppNotification
} from '../types';

export class ApiError extends Error {
  public code?: string;
  public status?: number;
  constructor(message: string, code?: string, status?: number) {
    super(message);
    this.name = 'ApiError';
    this.code = code;
    this.status = status;
  }
}

const ERROR_CODE_TRANSLATIONS: Record<string, string> = {
  bad_request_data_missed_field: 'Please fill in all required fields.',
  bad_request_data_length_invalid: 'Input character length is invalid or exceeds the limit.',
  'Only the Project Manager or the person assigned the task has the authority to change the task status.':
    'Only the Project Manager or the person assigned to the task has permission to change task status.',
  'Chỉ Project Manager hoặc người được giao việc mới có quyền thay đổi trạng thái công việc.':
    'Only the Project Manager or the person assigned to the task has permission to change task status.',
  FORBIDDEN: 'You do not have permission to perform this action.',
  UNAUTHORIZED: 'Your session has expired. Please log in again.',
};

const ENV_API_URL = import.meta.env.VITE_API_URL;
const BASE_URL = ENV_API_URL ? `${ENV_API_URL.replace(/\/$/, '')}/api/v1` : '/api/v1';

import { TokenStorage } from '../utils/tokenStorage';

export class ApiClient {
  public static getToken(): string | null {
    return TokenStorage.getToken();
  }

  public static setToken(token: string) {
    sessionStorage.setItem('workflowhub_token', token);
  }

  public static removeToken() {
    TokenStorage.clear();
  }

  public static async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const token = this.getToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const cleanEndpoint = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
    const url = `${BASE_URL}${cleanEndpoint}`;

    const response = await fetch(url, {
      ...options,
      headers,
    });

    if (!response.ok) {
      // 401 Unauthorized Interceptor: auto-clear session and dispatch event if authenticated session expired
      // Do NOT trigger on any /auth/ endpoints (login, register, logout, health, refresh-token) to prevent infinite loops
      if (response.status === 401 && !cleanEndpoint.startsWith('/auth/')) {
        TokenStorage.clear();
        if (typeof window !== 'undefined') {
          window.dispatchEvent(new CustomEvent('workflowhub:auth_expired', {
            detail: { message: 'Your session has expired. Please log in again.' }
          }));
        }
      }

      let errorMessage = `Request failed (${response.status})`;
      let errorCode: string | undefined = undefined;
      try {
        const errorData = await response.json();
        errorCode = errorData.code;
        const rawMsg = errorData.message || errorData.error;
        if (rawMsg) {
          // Prioritize specific message from backend. Only translate if rawMsg explicitly has a translation mapping
          errorMessage = ERROR_CODE_TRANSLATIONS[rawMsg] || rawMsg;
        } else if (errorCode && ERROR_CODE_TRANSLATIONS[errorCode]) {
          if (cleanEndpoint.startsWith('/auth/') && errorCode === 'UNAUTHORIZED') {
            errorMessage = 'Invalid email or password. Please try again.';
          } else {
            errorMessage = ERROR_CODE_TRANSLATIONS[errorCode];
          }
        }
      } catch {
        // Body is not json
      }
      throw new ApiError(errorMessage, errorCode, response.status);
    }

    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
      return response.json();
    }
    return {} as T;
  }

  public static get<T>(endpoint: string) {
    return this.request<T>(endpoint, { method: 'GET' });
  }

  public static post<T>(endpoint: string, body?: any) {
    return this.request<T>(endpoint, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  public static put<T>(endpoint: string, body?: any) {
    return this.request<T>(endpoint, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  public static delete<T>(endpoint: string) {
    return this.request<T>(endpoint, { method: 'DELETE' });
  }
}

// Dedicated Authentication Service mapped directly to Backend APIs
export const AuthService = {
  /**
   * POST /api/v1/auth/login
   */
  async login(credentials: { email: string; password: string }, rememberMe = false): Promise<ApiResponse<AuthResponse>> {
    const res = await ApiClient.post<ApiResponse<AuthResponse>>('/auth/login', credentials);
    if (res.data?.accessToken) {
      TokenStorage.saveAuth(res.data.accessToken, res.data.user, res.data.refreshToken, rememberMe);
    }
    return res;
  },

  /**
   * POST /api/v1/auth/login-google
   */
  async loginGoogle(idToken: string, rememberMe = false): Promise<ApiResponse<AuthResponse>> {
    const res = await ApiClient.post<ApiResponse<AuthResponse>>('/auth/login-google', { idToken });
    if (res.data?.accessToken) {
      TokenStorage.saveAuth(res.data.accessToken, res.data.user, res.data.refreshToken, rememberMe);
    }
    return res;
  },

  /**
   * Ping backend to check if the server is awake and ready
   * @param timeoutMs Timeout in milliseconds per attempt
   * @param signal Optional AbortSignal
   */
  async checkHealth(timeoutMs = 5000, signal?: AbortSignal): Promise<boolean> {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

      if (signal) {
        signal.addEventListener('abort', () => controller.abort(), { once: true });
      }

      const res = await fetch(`${BASE_URL}/auth/health`, {
        method: 'GET',
        headers: { Accept: 'application/json' },
        signal: controller.signal,
      });

      clearTimeout(timeoutId);
      // Status < 500 means Spring Boot backend is active and replying (200 OK or 4xx).
      // Render 502/503/504 edge errors have status >= 500 or throw CORS/network exceptions.
      return res.status < 500;
    } catch {
      return false;
    }
  },

  /**
   * Poll backend until awake or timed out
   * @param onProgress Callback to notify elapsed seconds
   * @param maxWaitSec Maximum wait time in seconds (default 60s)
   * @param signal Optional AbortSignal
   */
  async waitForServerWakeUp(
    onProgress?: (elapsedSec: number) => void,
    maxWaitSec = 60,
    signal?: AbortSignal
  ): Promise<boolean> {
    const startTime = Date.now();
    const intervalMs = 2500;

    while (Date.now() - startTime < maxWaitSec * 1000) {
      if (signal?.aborted) return false;

      const isAlive = await this.checkHealth(5000, signal);
      if (isAlive) return true;

      const elapsed = Math.round((Date.now() - startTime) / 1000);
      onProgress?.(elapsed);

      // Delay before next check
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }

    return false;
  },

  /**
   * GET /api/v1/auth/google/login URL for OAuth Server Redirect Flow
   */
  getGoogleLoginUrl(): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    return `${BASE_URL}/auth/google/login${origin ? `?client_url=${encodeURIComponent(origin)}` : ''}`;
  },

  /**
   * POST /api/v1/auth/register
   */
  async register(data: { email: string; password: string; name: string }): Promise<ApiResponse<User>> {
    return ApiClient.post<ApiResponse<User>>('/auth/register', data);
  },

  /**
   * POST /api/v1/auth/logout
   */
  async logout(): Promise<ApiResponse<void>> {
    try {
      return await ApiClient.post<ApiResponse<void>>('/auth/logout');
    } catch (error) {
      console.warn('API logout failed on server, clearing client session anyway:', error);
      return {
        data: undefined as unknown as void,
        timestamp: new Date().toISOString(),
      };
    } finally {
      ApiClient.removeToken();
    }
  },

  /**
   * POST /api/v1/auth/refresh-token
   */
  async refreshToken(refreshToken: string): Promise<ApiResponse<AuthResponse>> {
    return ApiClient.post<ApiResponse<AuthResponse>>('/auth/refresh-token', { refreshToken });
  },
};

export const RoleService = {
  /**
   * GET /api/v1/roles
   */
  async getRoles(page = 0, size = 100): Promise<Role[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<Role>>>(`/roles?page=${page}&size=${size}`);
      const content = res.data?.content || (Array.isArray(res.data) ? res.data : []);
      return content;
    } catch (err) {
      console.warn('Failed to fetch roles from API, using fallback roles:', err);
      return [
        { id: 1, name: 'ADMIN' },
        { id: 2, name: 'User' },
        { id: 3, name: 'PROJECT_MANAGER' },
        { id: 4, name: 'DEVELOPER' },
        { id: 5, name: 'TESTER' },
        { id: 6, name: 'VIEWER' },
      ];
    }
  },

  /**
   * Lọc và ẩn 2 role: USER và ADMIN
   */
  filterAssignableRoles(roles: Role[]): Role[] {
    return roles.filter((r) => {
      const name = (r.name || '').trim().toUpperCase();
      return name !== 'USER' && name !== 'ADMIN';
    });
  },
};

export const ProjectService = {
  /**
   * GET /api/v1/projects
   */
  async getProjects(search?: string, page = 0, size = 50): Promise<Project[]> {
    try {
      const query = search ? `?search=${encodeURIComponent(search)}&page=${page}&size=${size}` : `?page=${page}&size=${size}`;
      const res = await ApiClient.get<ApiResponse<PageResponse<Project>>>(`/projects${query}`);
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn('Failed to fetch projects:', err);
      return [];
    }
  },

  /**
   * GET /api/v1/projects/my-project
   */
  async getMyProjects(page = 0, size = 50): Promise<Project[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<Project>>>(`/projects/my-project?page=${page}&size=${size}`);
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn('Failed to fetch my projects:', err);
      return [];
    }
  },

  /**
   * GET /api/v1/projects/{id}
   */
  async getProjectById(id: number): Promise<Project> {
    const res = await ApiClient.get<ApiResponse<Project>>(`/projects/${id}`);
    return res.data;
  },

  /**
   * POST /api/v1/projects
   */
  async createProject(payload: CreateProjectPayload): Promise<Project> {
    const res = await ApiClient.post<ApiResponse<Project>>('/projects', payload);
    return res.data;
  },

  /**
   * PUT /api/v1/projects/{id}
   */
  async updateProject(id: number, payload: Partial<CreateProjectPayload>): Promise<Project> {
    const res = await ApiClient.put<ApiResponse<Project>>(`/projects/${id}`, payload);
    return res.data;
  },

  /**
   * DELETE /api/v1/projects/{id}
   */
  async deleteProject(id: number): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/projects/${id}`);
  },
};

export const ProjectMemberService = {
  /**
   * GET /api/v1/project-members?search=projectId=={id}
   */
  async getMembersByProjectId(projectId: number): Promise<ProjectMember[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<ProjectMember>>>(
        `/project-members?search=projectId==${projectId}&page=0&size=100`
      );
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn(`Failed to fetch members for projectId=${projectId}:`, err);
      return [];
    }
  },

  /**
   * POST /api/v1/project-members
   */
  async addMember(payload: CreateProjectMemberPayload): Promise<ProjectMember> {
    const res = await ApiClient.post<ApiResponse<ProjectMember>>('/project-members', payload);
    return res.data;
  },

  /**
   * PUT /api/v1/project-members/projects/{projectId}/users/{userId}
   */
  async updateMemberRole(projectId: number, userId: number, roleId: number): Promise<ProjectMember> {
    const res = await ApiClient.put<ApiResponse<ProjectMember>>(
      `/project-members/projects/${projectId}/users/${userId}`,
      { roleId }
    );
    return res.data;
  },

  /**
   * DELETE /api/v1/project-members/projects/{projectId}/users/{userId}
   */
  async removeMember(projectId: number, userId: number): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/project-members/projects/${projectId}/users/${userId}`);
  },
};

export const SprintService = {
  /**
   * GET /api/v1/sprints?search=projectId=={id}
   */
  async getSprintsByProjectId(projectId: number): Promise<Sprint[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<Sprint>>>(
        `/sprints?search=projectId==${projectId}&page=0&size=100`
      );
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn(`Failed to fetch sprints for projectId=${projectId}:`, err);
      return [];
    }
  },

  /**
   * POST /api/v1/sprints
   */
  async createSprint(payload: CreateSprintPayload): Promise<Sprint> {
    const res = await ApiClient.post<ApiResponse<Sprint>>('/sprints', payload);
    return res.data;
  },

  /**
   * PUT /api/v1/sprints/{id}
   */
  async updateSprint(id: number, payload: Partial<CreateSprintPayload>): Promise<Sprint> {
    const res = await ApiClient.put<ApiResponse<Sprint>>(`/sprints/${id}`, payload);
    return res.data;
  },

  /**
   * DELETE /api/v1/sprints/{id}
   */
  async deleteSprint(id: number): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/sprints/${id}`);
  },
};

export const UserService = {
  /**
   * GET /api/v1/users
   */
  async getUsers(search?: string, page = 0, size = 100): Promise<User[]> {
    try {
      const query = search ? `?search=${encodeURIComponent(search)}&page=${page}&size=${size}` : `?page=${page}&size=${size}`;
      const res = await ApiClient.get<ApiResponse<PageResponse<User>>>(`/users${query}`);
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn('Failed to fetch users:', err);
      return [];
    }
  },

  /**
   * PUT /api/v1/users/{id}
   */
  async updateUserRole(id: number, roleId: number): Promise<User> {
    const res = await ApiClient.put<ApiResponse<User>>(`/users/${id}`, { roleId });
    return res.data;
  },
};

export const TaskService = {
  /**
   * GET /api/v1/tasks?search=projectId=={id}
   */
  async getTasksByProjectId(projectId: number): Promise<Task[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<Task>>>(
        `/tasks?search=projectId==${projectId}&page=0&size=100`
      );
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn(`Failed to fetch tasks for projectId=${projectId}:`, err);
      return [];
    }
  },

  /**
   * GET /api/v1/tasks?search=sprintId=={id}
   */
  async getTasksBySprintId(sprintId: number): Promise<Task[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<Task>>>(
        `/tasks?search=sprintId==${sprintId}&page=0&size=100`
      );
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn(`Failed to fetch tasks for sprintId=${sprintId}:`, err);
      return [];
    }
  },

  /**
   * GET /api/v1/tasks?search=assigneeId=={userId}
   */
  async getMyTasks(userId: number): Promise<Task[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<Task>>>(
        `/tasks?search=assigneeId==${userId}&page=0&size=100`
      );
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn(`Failed to fetch tasks for assigneeId=${userId}:`, err);
      return [];
    }
  },

  /**
   * GET /api/v1/tasks
   */
  async getAllTasks(search?: string, page = 0, size = 100): Promise<Task[]> {
    try {
      const query = search ? `?search=${encodeURIComponent(search)}&page=${page}&size=${size}` : `?page=${page}&size=${size}`;
      const res = await ApiClient.get<ApiResponse<PageResponse<Task>>>(`/tasks${query}`);
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn('Failed to fetch tasks:', err);
      return [];
    }
  },

  /**
   * POST /api/v1/tasks
   */
  async createTask(payload: CreateTaskPayload): Promise<Task> {
    const res = await ApiClient.post<ApiResponse<Task>>('/tasks', payload);
    return res.data;
  },

  /**
   * GET /api/v1/tasks/{id}
   */
  async getTaskById(id: number): Promise<Task> {
    const res = await ApiClient.get<ApiResponse<Task>>(`/tasks/${id}`);
    return res.data;
  },

  /**
   * PUT /api/v1/tasks/{id}
   */
  async updateTask(id: number, payload: Partial<CreateTaskPayload>): Promise<Task> {
    const res = await ApiClient.put<ApiResponse<Task>>(`/tasks/${id}`, payload);
    return res.data;
  },

  /**
   * DELETE /api/v1/tasks/{id} (Soft delete)
   */
  async deleteTask(id: number): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/tasks/${id}`);
  },

  /**
   * PUT /api/v1/tasks/{id}/restore
   */
  async restoreTask(id: number): Promise<Task> {
    const res = await ApiClient.put<ApiResponse<Task>>(`/tasks/${id}/restore`);
    return res.data;
  },

  /**
   * DELETE /api/v1/tasks/{id}/permanent
   */
  async permanentDeleteTask(id: number): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/tasks/${id}/permanent`);
  },

  /**
   * GET /api/v1/tasks?search=projectId=={id};isDeleted==true;deletedById=={userId}
   */
  async getTrashTasks(projectId: number, deletedById?: number): Promise<Task[]> {
    try {
      // 1. If deletedById is provided, try querying backend with it first
      if (deletedById) {
        try {
          const res = await ApiClient.get<ApiResponse<PageResponse<Task>>>(
            `/tasks?search=projectId==${projectId};isDeleted==true;deletedById==${deletedById}&page=0&size=100`
          );
          const list = res.data?.content || (Array.isArray(res.data) ? res.data : []);
          return list;
        } catch (err) {
          // If BE RSQL builder does not support deletedById yet, fall back to base trash query
          console.warn('Backend does not support deletedById in RSQL yet, falling back to base trash query:', err);
        }
      }

      // Base query
      const res = await ApiClient.get<ApiResponse<PageResponse<Task>>>(
        `/tasks?search=projectId==${projectId};isDeleted==true&page=0&size=100`
      );
      const list = res.data?.content || (Array.isArray(res.data) ? res.data : []);

      // Client-side safeguard: filter only tasks deleted by current user if deletedById is present on task
      if (deletedById) {
        return list.filter((t: Task) => t.deletedById === undefined || t.deletedById === null || t.deletedById === deletedById);
      }
      return list;
    } catch (err) {
      console.warn(`Failed to fetch trash tasks for projectId=${projectId}:`, err);
      return [];
    }
  },

  /**
   * DELETE /api/v1/tasks/projects/{projectId}/trash?all={all}
   */
  async emptyTrash(projectId: number, all = false): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/tasks/projects/${projectId}/trash?all=${all}`);
  },
};

export const SubtaskApiService = {
  /**
   * GET /api/v1/subtasks/task/{taskId}
   */
  async getSubtasksByTaskId(taskId: number): Promise<any[]> {
    try {
      const res = await ApiClient.get<ApiResponse<any[]>>(`/subtasks/task/${taskId}`);
      return res.data || [];
    } catch (err) {
      console.warn(`Failed to fetch subtasks for taskId=${taskId}:`, err);
      return [];
    }
  },

  /**
   * GET /api/v1/subtasks/{id}
   */
  async getSubtaskById(id: number | string): Promise<any> {
    const res = await ApiClient.get<ApiResponse<any>>(`/subtasks/${id}`);
    return res.data;
  },

  /**
   * POST /api/v1/subtasks
   */
  async createSubtask(payload: CreateSubtaskPayload): Promise<any> {
    const res = await ApiClient.post<ApiResponse<any>>('/subtasks', payload);
    return res.data;
  },

  /**
   * PUT /api/v1/subtasks/{id}
   */
  async updateSubtask(id: number | string, payload: UpdateSubtaskPayload): Promise<any> {
    const res = await ApiClient.put<ApiResponse<any>>(`/subtasks/${id}`, payload);
    return res.data;
  },

  /**
   * DELETE /api/v1/subtasks/{id}
   */
  async deleteSubtask(id: number | string): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/subtasks/${id}`);
  },
};

export const ActivityLogService = {
  /**
   * GET /api/v1/activity-logs
   */
  async getActivityLogs(page = 0, size = 20): Promise<ActivityLog[]> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<ActivityLog>>>(
        `/activity-logs?page=${page}&size=${size}`
      );
      return res.data?.content || (Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      console.warn('Failed to fetch activity logs:', err);
      return [];
    }
  },
};

export interface UserDashboardData {
  userId: number;
  totalProjects: number;
  totalAssignedTasks: number;
  overdueTasks: number;
  tasksByStatus: Record<string, number>;
}

export interface ProjectDashboardData {
  projectId: number;
  projectName: string;
  projectDescription: string;
  ownerName: string | null;
  totalMembers: number;
  totalTasks: number;
  completedTasks: number;
  completionRate: number;
  overdueTasks: number;
  activeSprintName: string | null;
  tasksByStatus: Record<string, number>;
}

export const DashboardService = {
  /**
   * GET /api/v1/dashboard/user
   */
  async getUserDashboard(): Promise<UserDashboardData> {
    const res = await ApiClient.get<ApiResponse<UserDashboardData>>('/dashboard/user');
    return res.data;
  },

  /**
   * GET /api/v1/dashboard/project/{projectId}
   */
  async getProjectDashboard(projectId: number | string): Promise<ProjectDashboardData> {
    const res = await ApiClient.get<ApiResponse<ProjectDashboardData>>(`/dashboard/project/${projectId}`);
    return res.data;
  },
};

export const NotificationService = {
  /**
   * GET /api/v1/notifications?search=userId=={userId}&page={page}&size={size}&sort=createdAt,desc
   */
  async getMyNotifications(userId: number, page = 0, size = 20): Promise<PageResponse<AppNotification>> {
    try {
      const res = await ApiClient.get<ApiResponse<PageResponse<AppNotification>>>(
        `/notifications?search=userId==${userId}&page=${page}&size=${size}&sort=createdAt,desc`
      );
      return res.data || {
        content: [],
        pageNumber: 0,
        pageSize: size,
        totalElements: 0,
        totalPages: 0,
        last: true
      };
    } catch (err) {
      console.warn('Failed to fetch notifications:', err);
      return {
        content: [],
        pageNumber: 0,
        pageSize: size,
        totalElements: 0,
        totalPages: 0,
        last: true
      };
    }
  },

  /**
   * PUT /api/v1/notifications/{id}
   */
  async markAsRead(id: number): Promise<AppNotification> {
    const res = await ApiClient.put<ApiResponse<AppNotification>>(`/notifications/${id}`, {
      isRead: true,
    });
    return res.data;
  },

  /**
   * DELETE /api/v1/notifications/{id}
   */
  async deleteNotification(id: number): Promise<void> {
    await ApiClient.delete<ApiResponse<void>>(`/notifications/${id}`);
  },
};


