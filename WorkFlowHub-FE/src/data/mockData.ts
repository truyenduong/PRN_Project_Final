import type { User, Task, ActivityLog, Attachment, Comment, NotificationItem } from '../types';

export const mockUsers: User[] = [
  { id: 1, name: 'Minh Trần', email: 'minh@workflowhub.vn', role: 'ADMIN', status: 'active', avatar: 'MI' },
  { id: 2, name: 'Lan Nguyễn', email: 'lan@workflowhub.vn', role: 'PROJECT_MANAGER', status: 'active', avatar: 'LA' },
  { id: 3, name: 'Hải Phạm', email: 'hai@workflowhub.vn', role: 'DEVELOPER', status: 'active', avatar: 'HẢ' },
  { id: 4, name: 'Trầm Lê', email: 'tram@workflowhub.vn', role: 'VIEWER', status: 'invited', avatar: 'TR' },
];

export const mockTasks: Task[] = [
  { id: 101, title: 'Design dashboard interface', status: 'TO_DO', category: 'UI/UX', assigneeName: 'Lan Nguyen', assigneeAvatar: 'LA', progress: 0, dueDate: 'Friday', priority: 'MEDIUM' },
  { id: 102, title: 'Write API documentation', status: 'TO_DO', category: 'Documentation', assigneeName: 'Lan Nguyen', assigneeAvatar: 'LA', progress: 10, dueDate: 'Friday', priority: 'LOW' },
  { id: 103, title: 'Optimize database queries', status: 'TO_DO', category: 'Database', assigneeName: 'Minh Tran', assigneeAvatar: 'MI', progress: 0, dueDate: 'Monday', priority: 'HIGH' },
  { id: 104, title: 'Connect Kafka for notifications', status: 'IN_PROGRESS', category: 'Infrastructure', assigneeName: 'Hai Pham', assigneeAvatar: 'HA', progress: 70, dueDate: 'Today', priority: 'HIGH' },
  { id: 105, title: 'Benchmark API performance', status: 'IN_PROGRESS', category: 'Performance', assigneeName: 'Minh Tran', assigneeAvatar: 'MI', progress: 40, dueDate: 'Thursday', priority: 'MEDIUM' },
  { id: 106, title: 'Configure CI/CD pipeline', status: 'DONE', category: 'DevOps', assigneeName: 'Hai Pham', assigneeAvatar: 'HA', progress: 100, dueDate: 'Completed', priority: 'MEDIUM' },
  { id: 107, title: 'Migrate to PostgreSQL', status: 'DONE', category: 'Database', assigneeName: 'Hai Pham', assigneeAvatar: 'HA', progress: 100, dueDate: 'Completed', priority: 'HIGH' },
  { id: 108, title: 'Fix authentication bug', status: 'DONE', category: 'Security', assigneeName: 'Minh Tran', assigneeAvatar: 'MI', progress: 100, dueDate: 'Completed', priority: 'HIGH' },
  { id: 109, title: 'Write security regression tests', status: 'DONE', category: 'Security', assigneeName: 'Minh Tran', assigneeAvatar: 'MI', progress: 100, dueDate: 'Completed', priority: 'MEDIUM' },
];

export const mockActivityLogs: ActivityLog[] = [
  { id: 1, userName: 'Minh', action: 'moved task', target: '"Optimize database queries" to In Progress', timeAgo: 'Just now' },
  { id: 2, userName: 'Lan', action: 'attached', target: 'mockup-dashboard.fig', timeAgo: '15 minutes ago' },
  { id: 3, userName: 'Hai', action: 'commented on', target: '"Connect Kafka"', timeAgo: '1 hour ago' },
];

export const mockAttachments: Attachment[] = [
  { id: 1, fileName: 'kafka-topology.drawio', fileSize: '48 KB', uploaderName: 'Lan' },
  { id: 2, fileName: 'consumer-retry.patch', fileSize: '12 KB', uploaderName: 'Me' },
  { id: 3, fileName: 'perf-baseline.csv', fileSize: '210 KB', uploaderName: 'Minh' },
];

export const mockComments: Comment[] = [
  { id: 1, userName: 'Lan', time: '09:12', content: 'How is retry logic for Kafka consumer handled?' },
  { id: 2, userName: 'Me', time: '09:20', content: 'Added dead-letter queue, writing unit tests now.', isSelf: true },
  { id: 3, userName: 'Lan', time: '09:24', content: 'Great, remember to tag it for Sprint 32.' },
];

export const mockNotifications: NotificationItem[] = [
  { id: 1, title: 'Sprint 32 ends in 3 days', initials: 'SP', time: '10:02' },
  { id: 2, title: 'Lan mentioned you in mockup task', initials: 'LA', time: '09:15' },
  { id: 3, title: 'Week 31 report is ready', initials: 'RP', time: 'Yesterday' },
];
