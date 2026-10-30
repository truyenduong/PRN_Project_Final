import type { Subtask, CreateSubtaskPayload, UpdateSubtaskPayload } from '../types';
import { SubtaskApiService } from './api';

export const SubtaskService = {
  STORAGE_PREFIX: 'workflowhub_subtasks_',

  getLocalSubtasks(taskId: number): Subtask[] {
    try {
      const data = localStorage.getItem(`${this.STORAGE_PREFIX}${taskId}`);
      return data ? JSON.parse(data) : [];
    } catch {
      return [];
    }
  },

  saveLocalSubtasks(taskId: number, subtasks: Subtask[]): void {
    try {
      localStorage.setItem(`${this.STORAGE_PREFIX}${taskId}`, JSON.stringify(subtasks));
    } catch (e) {
      console.warn('Failed to save subtasks to localStorage:', e);
    }
  },

  async fetchSubtasks(taskId: number): Promise<Subtask[]> {
    const local = this.getLocalSubtasks(taskId);
    const localMap = new Map<string | number, Subtask>(local.map((s) => [s.id, s]));

    try {
      const remote = await SubtaskApiService.getSubtasksByTaskId(taskId);
      if (remote && remote.length > 0) {
        const formatted: Subtask[] = remote.map((r: any) => {
          const cached = localMap.get(r.id);
          return {
            id: r.id,
            taskId: r.taskId || taskId,
            title: r.title,
            description: r.description ?? cached?.description,
            isCompleted: Boolean(r.isCompleted),
            assigneeId: r.assigneeId !== undefined ? r.assigneeId : cached?.assigneeId,
            assigneeName: r.assigneeName ?? cached?.assigneeName,
            estimate: r.estimate !== undefined ? r.estimate : cached?.estimate,
            priority: r.priority ?? cached?.priority,
            dueDate: r.dueDate !== undefined ? r.dueDate : cached?.dueDate,
            createdAt: r.createdAt || cached?.createdAt,
            updatedAt: r.updatedAt || cached?.updatedAt,
          };
        });
        this.saveLocalSubtasks(taskId, formatted);
        return formatted;
      }
    } catch (err) {
      console.warn('Failed to fetch remote subtasks, fallback to local:', err);
    }
    return local;
  },

  async addSubtask(taskId: number, payload: string | CreateSubtaskPayload): Promise<Subtask> {
    const isString = typeof payload === 'string';
    const title = isString ? payload.trim() : payload.title.trim();
    const description = isString ? undefined : payload.description?.trim();
    const assigneeId = isString ? null : payload.assigneeId;
    const assigneeName = isString ? undefined : payload.assigneeName;
    const estimate = isString ? undefined : payload.estimate?.trim();
    const priority = isString ? 'MEDIUM' : payload.priority || 'MEDIUM';
    const dueDate = isString ? undefined : payload.dueDate;
    const isCompleted = isString ? false : Boolean(payload.isCompleted);

    try {
      const remote = await SubtaskApiService.createSubtask({
        taskId,
        title,
        description,
        assigneeId,
        estimate,
        priority,
        dueDate,
        isCompleted,
      });

      if (remote && remote.id) {
        const newSt: Subtask = {
          id: remote.id,
          taskId,
          title: remote.title || title,
          description: remote.description !== undefined ? remote.description : description,
          isCompleted: remote.isCompleted !== undefined ? Boolean(remote.isCompleted) : isCompleted,
          assigneeId: remote.assigneeId !== undefined ? remote.assigneeId : assigneeId,
          assigneeName: remote.assigneeName || assigneeName,
          estimate: remote.estimate !== undefined ? remote.estimate : estimate,
          priority: remote.priority || priority,
          dueDate: remote.dueDate !== undefined ? remote.dueDate : dueDate,
          createdAt: remote.createdAt || new Date().toISOString(),
          updatedAt: remote.updatedAt || new Date().toISOString(),
        };
        const current = this.getLocalSubtasks(taskId);
        this.saveLocalSubtasks(taskId, [...current, newSt]);
        return newSt;
      }
    } catch (err) {
      console.warn('API addSubtask failed, saving locally:', err);
    }

    const current = this.getLocalSubtasks(taskId);
    const newSubtask: Subtask = {
      id: Date.now(),
      taskId,
      title,
      description,
      isCompleted,
      assigneeId,
      assigneeName,
      estimate,
      priority,
      dueDate,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    const updated = [...current, newSubtask];
    this.saveLocalSubtasks(taskId, updated);
    return newSubtask;
  },

  async updateSubtaskDetails(
    taskId: number,
    subtaskId: number | string,
    payload: UpdateSubtaskPayload
  ): Promise<Subtask | null> {
    const current = this.getLocalSubtasks(taskId);
    const existing = current.find((s) => s.id === subtaskId);

    try {
      await SubtaskApiService.updateSubtask(subtaskId, {
        title: payload.title,
        description: payload.description,
        isCompleted: payload.isCompleted,
        assigneeId: payload.assigneeId,
        estimate: payload.estimate,
        priority: payload.priority,
        dueDate: payload.dueDate,
      });
    } catch (err) {
      console.warn('API updateSubtask failed, updating locally:', err);
    }

    let updatedSubtask: Subtask | null = null;
    const updatedList = current.map((s) => {
      if (s.id === subtaskId) {
        updatedSubtask = {
          ...s,
          ...payload,
          updatedAt: new Date().toISOString(),
        };
        return updatedSubtask;
      }
      return s;
    });

    if (!updatedSubtask && existing) {
      updatedSubtask = {
        ...existing,
        ...payload,
        updatedAt: new Date().toISOString(),
      };
      updatedList.push(updatedSubtask);
    }

    this.saveLocalSubtasks(taskId, updatedList);
    return updatedSubtask;
  },

  async toggleSubtask(taskId: number, subtaskId: number | string): Promise<Subtask[]> {
    const current = this.getLocalSubtasks(taskId);
    const target = current.find((s) => s.id === subtaskId);
    const nextCompleted = target ? !target.isCompleted : true;

    try {
      await SubtaskApiService.updateSubtask(subtaskId, { isCompleted: nextCompleted });
    } catch (err) {
      console.warn('API updateSubtask failed, updating locally:', err);
    }

    const updated = current.map((st) =>
      st.id === subtaskId ? { ...st, isCompleted: nextCompleted, updatedAt: new Date().toISOString() } : st
    );
    this.saveLocalSubtasks(taskId, updated);
    return updated;
  },

  async deleteSubtask(taskId: number, subtaskId: number | string): Promise<Subtask[]> {
    try {
      await SubtaskApiService.deleteSubtask(subtaskId);
    } catch (err) {
      console.warn('API deleteSubtask failed, removing locally:', err);
    }

    const current = this.getLocalSubtasks(taskId);
    const updated = current.filter((st) => st.id !== subtaskId);
    this.saveLocalSubtasks(taskId, updated);
    return updated;
  },
};
