# Java ↔ .NET endpoint parity review

Source: `C:\Users\ADMIN\Downloads\WorkFlowHub\WorkFlowHub-BE\WorkFlowHub`; target: this .NET solution.
This is a source comparison, not a live PostgreSQL or frontend contract test. The Java inventory contains 71 controller routes; .NET implements all 71 route/method pairs (9 auth + 5 roles + 5 users + 6 projects + 5 project-members + 5 activity-logs + 5 notifications + 5 sprints + 8 tasks + 5 attachments + 5 comments + 6 subtasks + 2 dashboard).

## Shared wire conventions

| Contract item | Java | .NET currently |
|---|---|---|
| Prefix and JSON naming | `/api/v1`; Jackson camelCase | `/api/v1`; System.Text.Json camelCase |
| Success envelope | `success`, `code`, `message`, `data`, `timestamp`; null properties omitted | Same fields and null omission |
| Error envelope | `success:false`, `code`, `message`, optional `errors:[{field,message}]`, `timestamp`, `path`; 4xx/5xx mapped by Java `GlobalExceptionHandler` | Same shape. Invalid numeric path/query values now return 400 `INVALID_REQUEST` with Java-style parameter messages; validation, RSQL and security errors may still differ as noted below |
| Date/time | `LocalDate` is ISO `yyyy-MM-dd`; `LocalDateTime` is ISO local date-time without offset (fractional seconds only when present) | `DateOnly`/custom `DateTime` converter; no zone suffix. `DateTime` has 100 ns precision, while Java can represent nanoseconds |
| Nullable fields | Jackson `NON_NULL` omits null response properties | JSON options omit null properties |
| Enums / numeric values | Workflow status/priority/type fields are Java `String`; IDs/long counts are JSON integers; project completion rate is JSON number (`double`) | Workflow fields are strings; IDs/counts are integers and project completion rate remains a JSON number (`double`) |
| Authorization header | `Authorization: Bearer <JWT>` for authenticated routes; auth public routes as noted | Same for protected MVC routes; fallback policy requires authentication |
| Content headers | JSON requests use `Content-Type: application/json`; responses JSON except OAuth redirects | Same for JSON endpoints and `Location` redirects |
| CORS | All origins pattern `*`, methods GET/POST/PUT/DELETE/OPTIONS/PATCH/HEAD, all request headers, exposes Authorization/Content-Type, credentials enabled | Same configuration defaults |
| Page body | `data:{content,page,size,totalElements,totalPages,first,last}` inside success envelope | Same PageResponse shape on all paged business endpoints |
| Page query | Spring `Pageable`: `page`, `size`, repeatable/comma-form `sort=property,asc|desc`; defaults page 0/size 20, negative page clamps to 0, non-positive size falls back to 20, and the resolver caps size at 2000 | Same query names/defaults and normalization; page size capped at 2000; no explicit sort when omitted. Invalid sort direction/unsupported property maps to the generic 500 envelope, as Java's uncaught sort/property failure does |

`ApiResponse` success defaults are `SUCCESS` / `Request successful`; created responses use `Resource created successfully`; delete helpers return HTTP **200 with an envelope**, not 204.

## Per-route comparison

`DTO fields` below are exact Java JSON property names (camelCase); types and response DTO fields are expanded in the schema appendix. `JWT` means bearer required; `Public` means permitAll in Java. Unless specified, an authenticated route has no controller-level role restriction; service checks are only those stated.

| # | Method | URL | Path/query parameters | Request JSON (Java) | Response JSON (Java) | Java status | .NET parity / access / page |
|---:|---|---|---|---|---|---:|---|
| 1 | GET | `/api/v1/auth/health` | — | — | `ApiResponse<String>`; data `"OK"` | 200 | Match; public |
| 2 | POST | `/api/v1/auth/login` | — | `LoginRequest{email,password}` | `ApiResponse<AuthResponse>` | 200 | Match; public |
| 3 | POST | `/api/v1/auth/login-google` | — | `GoogleLoginRequest{idToken}` | `ApiResponse<AuthResponse>` | 200 | Match; public |
| 4 | GET | `/api/v1/auth/google/login` | optional `client_url` | — | 302 `Location` to Google OAuth | 302 | Match with security fix: external/untrusted client origin is replaced by configured frontend origin; public |
| 5 | GET | `/api/v1/auth/google/callback` | required `code`, optional `state` | — | 302 `Location` to frontend; success query contains `token`, `refreshToken`, URL-encoded `user`; failure query contains `error` | 302 | Route/shape match; external `state` origin is now rejected. Token query parameters remain for FE compatibility; public |
| 6 | POST | `/api/v1/auth/logout` | `Authorization` header optional | — | `ApiResponse<Void>`; null `data` omitted | 200 | Match; public; valid supplied bearer may be blacklisted |
| 7 | POST | `/api/v1/auth/change-password` | — | `ChangePasswordRequest{currentPassword,newPassword,confirmPassword}` | `ApiResponse<Void>`; null `data` omitted | 200 | Match; JWT required |
| 8 | POST | `/api/v1/auth/register` | — | `RegisterRequest{email,password,name,avatar}` | `ApiResponse<UserResponse>` | 201 | Route/shape/status match; public |
| 9 | POST | `/api/v1/auth/refresh-token` | — | `RefreshTokenRequest{refreshToken}` | `ApiResponse<AuthResponse>` | 200 | Match; public |
| 10 | GET | `/api/v1/roles` | optional `search`; `page`, `size`, repeatable/comma `sort` | — | `ApiResponse<PageResponse<RoleResponse>>` | 200 | Implemented; JWT required; query parsing/invalid RSQL error text not byte-for-byte Java-equivalent |
| 11 | GET | `/api/v1/roles/{id}` | path `id: Long` | — | `ApiResponse<RoleResponse>` | 200 | Implemented; JWT required |
| 12 | POST | `/api/v1/roles` | — | `CreateRoleRequest{name:String}` | `ApiResponse<RoleResponse>` | 201 | Implemented; JWT required; intentionally no admin check to preserve Java |
| 13 | PUT | `/api/v1/roles/{id}` | path `id: Long` | `UpdateRoleRequest{name:String|null}` | `ApiResponse<RoleResponse>` | 200 | Implemented; JWT required; intentionally no admin check to preserve Java |
| 14 | DELETE | `/api/v1/roles/{id}` | path `id: Long` | — | `ApiResponse<Void>`; null `data` omitted | 200 | Implemented; JWT required; intentionally no admin check to preserve Java |
| 15 | GET | `/api/v1/users` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<UserResponse>>` | 200 | Implemented; authenticated; `password` excluded from RSQL; page contract preserved |
| 16 | GET | `/api/v1/users/{id}` | path `id: Long` | — | `ApiResponse<UserResponse>` | 200 | Implemented; authenticated |
| 17 | POST | `/api/v1/users` | — | `CreateUserRequest{email,password,name,avatar,status}` | `ApiResponse<UserResponse>` | 201 | Implemented; authenticated; validation and BCrypt cost 10 |
| 18 | PUT | `/api/v1/users/{id}` | path `id: Long` | `UpdateUserRequest{email,password,name,avatar,status,roleId}` | `ApiResponse<UserResponse>` | 200 | Implemented; authenticated; Java null-ignore mapping and email casing behavior retained |
| 19 | DELETE | `/api/v1/users/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; authenticated |
| 20 | GET | `/api/v1/projects` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<ProjectResponse>>` | 200 | Implemented; authenticated; `ownerId` RSQL alias and page shape retained |
| 21 | GET | `/api/v1/projects/{id}` | path `id: Long` | — | `ApiResponse<ProjectResponse>` | 200 | Implemented; authenticated |
| 22 | POST | `/api/v1/projects` | — | `CreateProjectRequest{name,description,ownerId}` | `ApiResponse<ProjectResponse>` | 201 | Implemented; authenticated; project and owner-PM membership save in a shared transaction |
| 23 | PUT | `/api/v1/projects/{id}` | path `id: Long` | `UpdateProjectRequest{name,description,ownerId}` | `ApiResponse<ProjectResponse>` | 200 | Implemented; authenticated; Java null-ignore update behavior retained |
| 24 | DELETE | `/api/v1/projects/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; authenticated |
| 25 | GET | `/api/v1/projects/my-project` | `page,size,sort` | — | `ApiResponse<PageResponse<ProjectResponse>>` | 200 | Implemented; authenticated; caller ID, membership role and owner fallback retained |
| 26 | GET | `/api/v1/tasks` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<TaskResponse>>` | 200 | Implemented; JWT required; applies Java's case-sensitive `isDeleted` substring/default `isDeleted==false`; RSQL whitelist retained |
| 27 | GET | `/api/v1/tasks/{id}` | path `id: Long` | — | `ApiResponse<TaskResponse>` | 200 | Implemented; JWT required; includes soft-deleted tasks like Java `findById` |
| 28 | POST | `/api/v1/tasks` | — | `CreateTaskRequest{projectId,sprintId,assigneeId,title,description,estimate,priority,status,dueDate,taskCode}` | `ApiResponse<TaskResponse>` | 201 | Implemented; JWT required; generated code `TASK-<epochMillis mod 100000>` when absent/blank; assignment notifier only when assignee exists |
| 29 | PUT | `/api/v1/tasks/{id}` | path `id: Long` | `UpdateTaskRequest{projectId,sprintId,assigneeId,title,description,estimate,priority,status,dueDate,taskCode,isDeleted}` | `ApiResponse<TaskResponse>` | 200 | Implemented; JWT required; status/assignee changes require admin, original project owner, PM member or current assignee; notification only for a newly changed non-null assignee; no Redis |
| 30 | DELETE | `/api/v1/tasks/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; soft delete; Java's non-null `Long`/`ObjectUtils.isEmpty` ternary leaves `deletedById` null |
| 31 | PUT | `/api/v1/tasks/{id}/restore` | path `id: Long` | — | `ApiResponse<TaskResponse>` | 200 | Implemented; JWT required; clears `isDeleted` and `deletedById`; no cache |
| 32 | DELETE | `/api/v1/tasks/{id}/permanent` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; physically deletes any existing task |
| 33 | DELETE | `/api/v1/tasks/projects/{projectId}/trash` | path `projectId`; optional `all:boolean` (default false) | — | `ApiResponse<Void>` | 200 | Implemented; `all=false` deletes trash rows for caller `deletedById`; `all=true` allows exact `ADMIN` or PM member only (no owner bypass); no cache |
| 34 | GET | `/api/v1/subtasks` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<SubtaskResponse>>` | 200 | Implemented; JWT required; RSQL whitelist (`taskId,assigneeId,title,priority,isCompleted`) and page envelope retained |
| 35 | GET | `/api/v1/subtasks/task/{taskId}` | path `taskId: Long` | — | `ApiResponse<List<SubtaskResponse>>` | 200 | Implemented; JWT required; returns an unpaged list |
| 36 | GET | `/api/v1/subtasks/{id}` | path `id: Long` | — | `ApiResponse<SubtaskResponse>` | 200 | Implemented; JWT required; Java not-found message retained |
| 37 | POST | `/api/v1/subtasks` | — | `CreateSubtaskRequest{taskId,title,description,isCompleted,assigneeId,estimate,priority,dueDate}` | `ApiResponse<SubtaskResponse>` | 201 | Implemented; JWT required; defaults completion false and notifies only for an assigned user distinct from parent task assignee |
| 38 | PUT | `/api/v1/subtasks/{id}` | path `id: Long` | `UpdateSubtaskRequest{title,description,isCompleted,assigneeId,estimate,priority,dueDate}` | `ApiResponse<SubtaskResponse>` | 200 | Implemented; JWT required; completion/assignee changes enforce Java permission branches; preserved null-assignee failure |
| 39 | DELETE | `/api/v1/subtasks/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; no added permission check; 200 success envelope retained |
| 40 | GET | `/api/v1/sprints` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<SprintResponse>>` | 200 | Implemented; JWT required; RSQL whitelist (`projectId,name,status,startDate,endDate`), date-only fields and page shape retained |
| 41 | GET | `/api/v1/sprints/{id}` | path `id: Long` | — | `ApiResponse<SprintResponse>` | 200 | Implemented; JWT required; Java not-found message retained |
| 42 | POST | `/api/v1/sprints` | — | `CreateSprintRequest{projectId,name,goal,startDate,endDate,status}` | `ApiResponse<SprintResponse>` | 201 | Implemented; JWT required; no project/member authorization or date-order rule added |
| 43 | PUT | `/api/v1/sprints/{id}` | path `id: Long` | `UpdateSprintRequest{projectId,name,goal,startDate,endDate,status}` | `ApiResponse<SprintResponse>` | 200 | Implemented; JWT required; only original project owner or PM member; no admin bypass; Java's authorization-before-projectId-update behavior retained |
| 44 | DELETE | `/api/v1/sprints/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; no service-level permission check; 200 success envelope retained |
| 45 | GET | `/api/v1/project-members` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<ProjectMemberResponse>>` | 200 | Implemented; authenticated; Java RSQL aliases and composite-key page mapping |
| 46 | GET | `/api/v1/project-members/projects/{projectId}/users/{userId}` | path `projectId,userId: Long` | — | `ApiResponse<ProjectMemberResponse>` | 200 | Implemented; authenticated; composite key lookup |
| 47 | POST | `/api/v1/project-members` | — | `CreateProjectMemberRequest{projectId,userId,roleId}` | `ApiResponse<ProjectMemberResponse>` | 201 | Implemented; authenticated; no service-level permission check, as in Java |
| 48 | PUT | `/api/v1/project-members/projects/{projectId}/users/{userId}` | path `projectId,userId: Long` | `UpdateProjectMemberRequest{roleId}` | `ApiResponse<ProjectMemberResponse>` | 200 | Implemented; authenticated; no service-level permission check, as in Java |
| 49 | DELETE | `/api/v1/project-members/projects/{projectId}/users/{userId}` | path `projectId,userId: Long` | — | `ApiResponse<Void>` | 200 | Implemented; authenticated; Java self/owner/admin/PM checks and order retained |
| 50 | GET | `/api/v1/notifications` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<NotificationResponse>>` | 200 | Implemented; JWT required; RSQL whitelist (`userId,type,isRead`), page shape and recipient name mapped |
| 51 | GET | `/api/v1/notifications/{id}` | path `id: Long` | — | `ApiResponse<NotificationResponse>` | 200 | Implemented; JWT required; Java not-found message retained |
| 52 | POST | `/api/v1/notifications` | — | `CreateNotificationRequest{userId,type,content,isRead}` | `ApiResponse<NotificationResponse>` | 201 | Implemented; JWT required; create validation/default `isRead=false` retained; ID-only Java mapper omits `userName`; no service-level ownership check |
| 53 | PUT | `/api/v1/notifications/{id}` | path `id: Long` | `UpdateNotificationRequest{content,isRead}` | `ApiResponse<NotificationResponse>` | 200 | Implemented; JWT required; null-ignore update semantics retained |
| 54 | DELETE | `/api/v1/notifications/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; 200 success envelope retained |
| 55 | GET | `/api/v1/dashboard/user` | — | — | `ApiResponse<UserDashboardResponse>` | 200 | Implemented; JWT required; direct DB metrics, membership-only project count, no cache |
| 56 | GET | `/api/v1/dashboard/project/{projectId}` | path `projectId: Long` | — | `ApiResponse<ProjectDashboardResponse>` | 200 | Implemented; JWT required; no membership check/cache; Java completion rounding and first ACTIVE sprint behavior retained |
| 57 | GET | `/api/v1/comments` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<CommentResponse>>` | 200 | Implemented; JWT required; RSQL whitelist (`taskId,userId`), Java paging envelope retained |
| 58 | GET | `/api/v1/comments/{id}` | path `id: Long` | — | `ApiResponse<CommentResponse>` | 200 | Implemented; JWT required; Java not-found message retained |
| 59 | POST | `/api/v1/comments` | — | `CreateCommentRequest{taskId,userId,content}` | `ApiResponse<CommentResponse>` | 201 | Implemented; JWT required; ID-only relations leave `userName` null in create response |
| 60 | PUT | `/api/v1/comments/{id}` | path `id: Long` | `UpdateCommentRequest{content}` | `ApiResponse<CommentResponse>` | 200 | Implemented; JWT required; null-ignore content update retained |
| 61 | DELETE | `/api/v1/comments/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; 200 success envelope retained |
| 62 | GET | `/api/v1/attachments` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<AttachmentResponse>>` | 200 | Implemented; JWT required; RSQL whitelist (`taskId,fileName`), page shape retained |
| 63 | GET | `/api/v1/attachments/{id}` | path `id: Long` | — | `ApiResponse<AttachmentResponse>` | 200 | Implemented; JWT required |
| 64 | POST | `/api/v1/attachments` | — | `CreateAttachmentRequest{taskId,fileUrl,fileName}` | `ApiResponse<AttachmentResponse>` | 201 | Implemented; JWT required; metadata only, FK errors come from database |
| 65 | PUT | `/api/v1/attachments/{id}` | path `id: Long` | `UpdateAttachmentRequest{fileUrl,fileName}` | `ApiResponse<AttachmentResponse>` | 200 | Implemented; JWT required; null-ignore update semantics retained |
| 66 | DELETE | `/api/v1/attachments/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; 200 success envelope retained |
| 67 | GET | `/api/v1/activity-logs` | optional `search`; `page,size,sort` | — | `ApiResponse<PageResponse<ActivityLogResponse>>` | 200 | Implemented; JWT required; RSQL whitelist (`userId,projectId,action`), page/read related names mapped; create response omits ID-only related names |
| 68 | GET | `/api/v1/activity-logs/{id}` | path `id: Long` | — | `ApiResponse<ActivityLogResponse>` | 200 | Implemented; JWT required; Java not-found message retained |
| 69 | POST | `/api/v1/activity-logs` | — | `CreateActivityLogRequest{userId,projectId,action}` | `ApiResponse<ActivityLogResponse>` | 201 | Implemented; JWT required; Java field validation, ID-only related-name omission and no service-level permission check retained |
| 70 | PUT | `/api/v1/activity-logs/{id}` | path `id: Long` | `UpdateActivityLogRequest{action}` | `ApiResponse<ActivityLogResponse>` | 200 | Implemented; JWT required; null-ignore update changes only `action` |
| 71 | DELETE | `/api/v1/activity-logs/{id}` | path `id: Long` | — | `ApiResponse<Void>` | 200 | Implemented; JWT required; 200 success envelope retained |

## DTO wire schema appendix

All names are JSON camelCase; Java wrapper/reference types may be null. Null response fields are omitted. `Long` is a JSON integer; `String` is a JSON string; `Boolean` is JSON boolean; `LocalDate` is `yyyy-MM-dd`; `LocalDateTime` is local ISO date-time, without `Z` or offset.

| Java DTO | JSON fields and types |
|---|---|
| `LoginRequest` | `email:string`, `password:string` |
| `GoogleLoginRequest` | `idToken:string` |
| `RegisterRequest` | `email:string`, `password:string`, `name:string`, `avatar:string|null` |
| `RefreshTokenRequest` | `refreshToken:string` |
| `ChangePasswordRequest` | `currentPassword:string`, `newPassword:string`, `confirmPassword:string|null` |
| `AuthResponse` | `accessToken:string`, `refreshToken:string`, `tokenType:string`, `expiresIn:Long`, `user:UserResponse` |
| `CreateRoleRequest` / `UpdateRoleRequest` | `name:string`; update name nullable/optional |
| `RoleResponse` | `id:Long`, `name:string`, `createdAt:LocalDateTime`, `updatedAt:LocalDateTime` |
| `CreateUserRequest` | `email:string`, `password:string`, `name:string`, `avatar:string|null`, `status:string|null` |
| `UpdateUserRequest` | `email:string`, `password:string`, `name:string`, `avatar:string|null`, `status:string|null`, `roleId:Long|null` |
| `UserResponse` | `id:Long`, `email:string`, `name:string`, `avatar:string|null`, `status:string|null`, `roleId:Long|null`, `roleName:string|null`, `createdAt:LocalDateTime`, `updatedAt:LocalDateTime` |
| `CreateProjectRequest` / `UpdateProjectRequest` | `name:string`, `description:string|null`, `ownerId:Long` |
| `ProjectResponse` | `id:Long`, `name:string`, `description:string|null`, `ownerId:Long`, `ownerName:string|null`, `currentUserRole:string|null`, `createdAt:LocalDateTime`, `updatedAt:LocalDateTime` |
| `CreateTaskRequest` | `projectId:Long`, `sprintId:Long|null`, `assigneeId:Long|null`, `title:string`, `description:string|null`, `estimate:string|null`, `priority:string|null`, `status:string|null`, `dueDate:LocalDate|null`, `taskCode:string|null` |
| `UpdateTaskRequest` | Same task fields plus `isDeleted:Boolean|null` |
| `TaskResponse` | `id,projectId,sprintId,assigneeId,deletedById:Long`; `projectName,sprintName,assigneeName,title,description,estimate,priority,status,taskCode,deletedByName:string`; `isDeleted:Boolean`; `dueDate:LocalDate`; `createdAt,updatedAt:LocalDateTime` |
| `CreateSubtaskRequest` | `taskId:Long`, `title:string`, `description:string|null`, `isCompleted:Boolean|null`, `assigneeId:Long|null`, `estimate:string|null`, `priority:string|null`, `dueDate:LocalDate|null` |
| `UpdateSubtaskRequest` | `title:string|null`, `description:string|null`, `isCompleted:Boolean|null`, `assigneeId:Long|null`, `estimate:string|null`, `priority:string|null`, `dueDate:LocalDate|null` |
| `SubtaskResponse` | `id,taskId,assigneeId:Long`; `title,description,assigneeName,estimate,priority:string`; `isCompleted:Boolean`; `dueDate:LocalDate`; `createdAt,updatedAt:LocalDateTime` |
| `CreateSprintRequest` / `UpdateSprintRequest` | `projectId:Long`, `name:string`, `goal:string|null`, `startDate,endDate:LocalDate`, `status:string` |
| `SprintResponse` | `id,projectId:Long`; `projectName,name,goal,status:string`; `startDate,endDate:LocalDate`; `createdAt,updatedAt:LocalDateTime` |
| `CreateProjectMemberRequest` | `projectId,userId:Long`, `roleId:Long|null` |
| `UpdateProjectMemberRequest` | `roleId:Long|null` |
| `ProjectMemberResponse` | `projectId,userId,roleId:Long`; `projectName,userName,roleName:string`; `createdAt,updatedAt:LocalDateTime` |
| `CreateNotificationRequest` | `userId:Long`, `type,content:string`, `isRead:Boolean` (default false; Java mapper uses an ID-only user reference, so create response omits userName) |
| `UpdateNotificationRequest` | `content:string|null`, `isRead:Boolean|null` |
| `NotificationResponse` | `id,userId:Long`; `userName,type,content:string`; `isRead:Boolean`; `createdAt,updatedAt:LocalDateTime` |
| `CreateCommentRequest` | `taskId,userId:Long`, `content:string` |
| `UpdateCommentRequest` | `content:string|null` |
| `CommentResponse` | `id,taskId,userId:Long`; `userName,content:string`; `createdAt,updatedAt:LocalDateTime` |
| `CreateAttachmentRequest` | `taskId:Long`, `fileUrl,fileName:string` |
| `UpdateAttachmentRequest` | `fileUrl,fileName:string|null` |
| `AttachmentResponse` | `id,taskId:Long`; `fileUrl,fileName:string`; `createdAt,updatedAt:LocalDateTime` |
| `CreateActivityLogRequest` | `userId:Long`, `projectId:Long|null`, `action:string` (the Java mapper uses ID-only relation references; create response omits related names) |
| `UpdateActivityLogRequest` | `action:string|null` |
| `ActivityLogResponse` | `id,userId,projectId:Long`; `userName,projectName,action:string`; `createdAt,updatedAt:LocalDateTime` |
| `UserDashboardResponse` | `userId:Long`, `totalProjects,totalAssignedTasks,overdueTasks:long`, `tasksByStatus:Map<string,long>` |
| `ProjectDashboardResponse` | `projectId:Long`, `projectName,projectDescription,ownerName,activeSprintName:string`; `totalMembers,totalTasks,completedTasks,overdueTasks:long`; `completionRate:double`; `tasksByStatus:Map<string,long>` |
| `PageResponse<T>` | `content:List<T>`, `page,size,totalPages:int`, `totalElements:long`, `first,last:boolean` |

Workflow fields `status`, `priority`, `type`, and `roleName` are strings in these DTOs, not JSON enum numbers. The dashboard uses `double` for `completionRate` and rounds to two decimal places in both source-equivalent implementations.

## Migration completion

All 71 Java controller routes are represented in .NET. Kafka delivery and Redis caching remain intentionally excluded; Subtask assignment notifications use the synchronous database notifier. The additional Java common/support classes without a direct port, and remaining contract differences, are listed below.

## Review deltas and fixes

1. **Fixed OAuth arbitrary-origin redirect:** `/auth/google/login` and callback now accept only the configured `Frontend:Url` origin; an untrusted `client_url`/OAuth `state` cannot receive the returned tokens. Access/refresh tokens remain in the configured frontend redirect query to preserve the existing FE flow; moving them to a one-time code or fragment would require a separately agreed FE contract change.
2. **Preserved Role behavior by decision:** Java allows any authenticated user to mutate roles, and the user selected Java parity over adding admin-only authorization. This leaves the reported role-escalation risk intentionally present pending a separate authorization change.
3. **Error differences remain:** invalid numeric route/query binding now reaches the action model binder and returns Java-style 400 `INVALID_REQUEST`; a focused test covers the response. Missing Google callback `code` also uses the Java message. Java business exceptions can carry module-specific `ErrorCode`s, while .NET `BadRequestException` uses the common `INVALID_REQUEST`; malformed RSQL parser details and some validation messages are not byte-identical. Spring authentication/denial messages can vary by exception; .NET uses fixed 401/403 messages.
4. **Fixed a valid RSQL `=in=` defect:** `Enumerable.Contains` was incorrectly composed as an instance call, and nullable selectors used the wrong generic element type. It now builds the static call with the property type; a nullable-assignee query test covers it.
5. **No live database contract comparison:** business endpoints and services are now present, but PostgreSQL-backed verification of database collation, DateOnly filters, paging/sorting, relation loading and N+1 behavior has not been run. Persisted audit columns use PostgreSQL `timestamp without time zone`; EF audit values are normalized to UTC clock time without an offset. No blocking calls (`.Result`, `.Wait`, `async void`) were found; writes in the current repositories use `SaveChangesAsync`.
6. **Business transactions:** Migrated business repository operations use explicit EF transactions. The exact Java class/method transaction annotation matrix and provider-level rollback effects still need PostgreSQL-backed verification.
7. **Path binding, paging and sort errors corrected:** Removed numeric route constraints so invalid numeric IDs produce 400 conversion errors instead of route-miss 404s; page normalization matches Spring `Pageable` defaults/clamps; invalid sort directions and unsupported sort properties use the generic 500 error envelope.

## Java classes/methods without a direct .NET port

The 71 controller methods are all mapped in the route table above. This inventory covers shared/support Java APIs, not additional HTTP endpoints. “Equivalent” means behavior was folded into a differently shaped .NET component; it does not imply identical implementation.

| Java class / methods | .NET status |
|---|---|
| `RequestIdFilter.doFilterInternal` | Not migrated: .NET does not currently echo/generate `X-Request-ID` or add the request ID to scoped log context. |
| `OpenApiConfig.customOpenAPI` | Partially migrated: Swagger UI exists, but the Java title/description/version and named BearerAuth security scheme/security requirement are not configured in .NET. |
| `BasePageRequest` validation/getters/setters; `BaseSortRequest` getters/setters | Not migrated as standalone DTOs. None of the 71 Java controllers bind these classes; they use Spring `Pageable` instead. |
| `RsqlUtils.pageable`, `defaultPageable`, `orAll` | No direct utility port. The 71 controller routes use `Pageable`; page normalization is in `PageRequestParser`, RSQL execution is in `RsqlFilter<TEntity>`, and nullable filters are handled in repositories. |
| `RsqlSearchVisitor.visit(AndNode)`, `visit(OrNode)`, `visit(ComparisonNode)` | Functionality is represented by `RsqlFilter<TEntity>` and the infrastructure parser/operator implementation. Library-specific malformed-query diagnostics are still different. |
| `BaseServiceImpl.findById`, `findAll()` overloads, `existsById`, `save`, `deleteById` | No generic base-service class. Endpoint behavior is implemented explicitly in domain application services and repositories; no route depends on the Java generic base class. |
| `AppConstants`, `ApiConstants`, `MessageConstants`, `SecurityConstants`, `DateTimeConstants`, `CharacterConstants`, `RegexConstants` | No 1:1 constants classes. Endpoint/API/security values used by migrated routes have .NET equivalents; unused Java formatting/regex constants were not ported as APIs. |

Kafka- and Redis-specific classes/configuration are deliberately omitted from this unported list per scope.

Final validation: `dotnet build PRN232.sln` succeeded with 0 warnings/errors; `dotnet test PRN232.sln --no-build` passed **84/84** tests.
