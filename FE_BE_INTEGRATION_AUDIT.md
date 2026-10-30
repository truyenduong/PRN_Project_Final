# FE - .NET BE integration audit

**Date:** 2026-10-30  
**Scope:** Read-only source review of `WorkFlowHub-FE`, the `PRN232` API controllers and DTOs, and `ENDPOINT_PARITY.md`, `MIGRATION_PROGRESS.md`, and `KNOWN_ISSUES.md`. No FE or BE code was changed.  
**Limitations:** Static review only. No FE-to-.NET end-to-end request, Google Console configuration, deployed environment, or live PostgreSQL behavior was verified. Claims about configured values below distinguish checked-in defaults from runtime values.

**Follow-up:** Per the user's backend-only scope, `ProjectMemberResponse` now additively includes nullable `userEmail`, mapped from the already-loaded User entity. FE files remain unchanged. Other FE-side or semantically uncertain mismatches remain open. The API process that was already running during this change still has its previous assembly loaded; restart it to serve the updated response.

## Executive summary

- The FE's observed API paths and HTTP methods are represented by .NET controller actions; **no confirmed `BE-MISSING` endpoint** was found.
- Most used route and response-envelope contracts match. Confirmed integration differences are:
  1. FE task payloads declare `parentTaskId`; .NET task request DTOs do not.
  2. FE subtask payloads declare `assigneeName`; .NET subtask request DTOs do not.
  3. FE project-member UI reads `userEmail`; this was absent in the initial audit and is now included by the additive BE response property.
  4. FE activity UI reads `target` and `timeAgo`, which are not present in the .NET `ActivityLogResponse`.
  5. FE's declared page metadata uses `pageNumber`/`pageSize`; .NET sends `page`/`size`. The FE currently consumes `data.content`, not these metadata fields.
  6. FE does not read or render the .NET `errors[]` validation details.
- FE has no automatic refresh-token request. In the password-login path the refresh token is saved, then cleared by the auth context; protected-route 401s clear auth and log out instead of refreshing.
- The FE example environment and Vite proxy target the hosted backend, not this local .NET instance. The FE's default dev port and .NET `Frontend:Url` are both 3000, but the backend API listener settings disagree (8080 in `appsettings.json`, 5101 in launch settings; a prior run in this workspace logged 8080).
- Several controller routes are not used by the FE; see [Backend routes not called by FE](#backend-routes-not-called-by-fe). These are not missing backend implementations.

## Connection and runtime configuration

| Surface | Observed configuration | Integration implication |
|---|---|---|
| FE API base | `VITE_API_URL` with a trailing slash removed, then `/api/v1`; if unset, relative `/api/v1`. See [`api.ts`](./WorkFlowHub-FE/src/services/api.ts#L43). | Setting `VITE_API_URL` to `http://localhost:8080` targets this local BE; otherwise FE's env/proxy behavior applies. |
| FE example env | `.env.example` sets `VITE_API_URL=https://workflowhub-1imx.onrender.com`. No actual `.env` file was found in the FE scan. See [`.env.example`](./WorkFlowHub-FE/.env.example#L1). | Copying the example unchanged sends FE calls to the hosted service, not local `PRN232`. |
| FE dev server/proxy | Vite serves on port **3000** and proxies `/api` to `https://workflowhub-1imx.onrender.com`; see [`vite.config.ts`](./WorkFlowHub-FE/vite.config.ts#L9). | With `VITE_API_URL` unset, browser requests are relative and use that remote proxy target. It is not a local .NET proxy. |
| FE CORS | No FE CORS configuration. Standard API fetches do not set `credentials`; they use JSON and, when logged in, the `Authorization` header. | Direct cross-origin calls rely on BE CORS allowing the FE origin and preflight headers. |
| BE API URL/port | `appsettings.json` has `Urls: http://localhost:8080`; `launchSettings.json` profile `http` specifies `http://localhost:5101`. The previously run process logged `http://localhost:8080`. `SERVER_PORT` can override `Urls` in [`JavaEnvironmentOverrides.cs`](./PRN232.Api/Configuration/JavaEnvironmentOverrides.cs). | There is a source/runtime port discrepancy. Confirm the effective listener before setting `VITE_API_URL` or a proxy target. The last observed run listened on 8080; that does not prove a future launch will use the same environment. |
| BE frontend origin | `Frontend:Url` defaults to `http://localhost:3000`; `FRONTEND_URL` can override it. | Matches the FE Vite default locally. Production OAuth redirects require the configured value to be the actual FE origin. |
| BE CORS | Defaults: allowed origin pattern `*`, methods GET/POST/PUT/DELETE/OPTIONS/PATCH/HEAD, all request headers, exposed `Authorization` and `Content-Type`, credentials enabled; wired in [`Program.cs`](./PRN232.Api/Program.cs#L32). | Browser JSON/Authorization preflights are allowed. The permissive origin policy should be narrowed for production; FE currently does not use cookie credentials. |
| BE OAuth config | `Google:ClientId` and `Google:ClientSecret` are empty in checked-in `appsettings.json`; `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` can override them. | Google flows cannot be considered configured from checked-in defaults alone. Google Console callback URI and deployed proxy scheme/host were not verified. |
| BE JWT config | `Security:Jwt:SecretKey` is not set in checked-in settings; BE requires `JWT_SECRET_KEY` or config secret of at least 32 UTF-8 bytes. | Local startup requires a secret. The random key generated for the earlier local run was process-scoped and is not a persistent configuration. |

## Shared FE transport, response, and error behavior

FE uses the custom fetch client in [`api.ts`](./WorkFlowHub-FE/src/services/api.ts#L61):

- All regular client requests set `Content-Type: application/json`, JSON-stringify supplied bodies, and add the current access token in the `Authorization` request header when available. There is no `credentials` option. The health-check fetch uses `Accept: application/json` and does not use this auth-header path.
- The shared success type expects a `data` field. Services generally return/unpack `res.data`. For pages they generally use `data.content`, with raw-array fallbacks; they do not validate the envelope's `success`, `code`, `message`, or `timestamp`.
- The .NET success envelope is `{success,code,message,data,timestamp}` with camelCase. A page is `data:{content,page,size,totalElements,totalPages,first,last}`. The FE does not use page metadata in the scanned list views.
- FE `PageResponse` declares `pageNumber` and `pageSize` while .NET sends `page` and `size`; this is a metadata naming mismatch, currently masked because the FE consumes `content` and does not read those declared metadata fields. `totalElements`, `totalPages`, and `last` are also not used by the scanned list views.
- On a non-2xx response FE attempts to read `code`, `message`, or `error`, applies limited message translations, and throws `ApiError(message, code, status)`. It **does not inspect `errors[]`**. Callers commonly display `err.message`; selected task handlers also branch on `err.code`/`err.status`.
- A 401 from a non-`/auth/` route clears stored auth and dispatches `workflowhub:auth_expired`; `AuthContext` logs the user out. Auth paths are excluded from this handler. No 401 refresh/retry is implemented. A 403 is propagated as an `ApiError`; no shared refresh or special retry behavior was found.
- The .NET error envelope is `{success:false,code,message,errors?,timestamp,path}`. Validation errors may include `errors:[{field,message}]`; those field-specific messages are dropped by FE. Null response properties, including null `data` and null `errors`, are omitted by .NET JSON settings.
- FE stores token, user, role and refresh token in both `sessionStorage` and `localStorage` when “Remember Me” is enabled. Reads check JWT expiry; expired state is cleared. Auth context also logs out after 15 minutes of inactivity. See [`tokenStorage.ts`](./WorkFlowHub-FE/src/utils/tokenStorage.ts#L9) and [`AuthContext.tsx`](./WorkFlowHub-FE/src/context/AuthContext.tsx#L25).
- Password login currently saves the refresh token in `AuthService.login`, then `AuthContext.login` calls `saveAuth(..., null, ...)`, which clears/re-saves auth without that refresh token. The refresh wrapper exists but no callsite or automatic refresh flow was found.

## FE API call inventory and .NET comparison

All paths in this table are appended to `/api/v1`. Shared request headers are as described above; “auth” means the client adds a bearer token when one exists. A 200/201 API response is normally unwrapped from `data`; pages use `data.content` or an array fallback. `FE-UNUSED` denotes a wrapper without a FE callsite, not an absent BE route.

| FE file/function (and caller where observed) | Method, path and query | Request body / headers | FE response use and error handling | Comparison |
|---|---|---|---|---|
| [`AuthService.login`](./WorkFlowHub-FE/src/services/api.ts#L149); [`AuthModal.handleSubmit`](./WorkFlowHub-FE/src/components/AuthModal.tsx#L53) | POST `/auth/login` | `{email:string,password:string}`; JSON, no required bearer | Reads `data.accessToken`, `data.user`; service stores `data.refreshToken`; UI shows `err.message`. | **OK** route/body/response DTO match. Refresh token is later cleared by context (lifecycle risk below). |
| [`AuthService.register`](./WorkFlowHub-FE/src/services/api.ts#L238); `AuthModal.handleSubmit` | POST `/auth/register` | `{email:string,password:string,name:string}`; JSON | Uses success as registration confirmation, then returns to login; errors display `err.message`. | **OK**; .NET also accepts optional `avatar`, not sent by FE. .NET returns 201 envelope with user in `data`. |
| [`AuthService.loginGoogle`](./WorkFlowHub-FE/src/services/api.ts#L160) | POST `/auth/login-google` | `{idToken:string}`; JSON | Auth response type matches; no UI callsite found. | **FE-UNUSED** wrapper; .NET route exists. |
| [`AuthService.checkHealth` / `waitForServerWakeUp`](./WorkFlowHub-FE/src/services/api.ts#L173) | GET `/auth/health` | No body; `Accept: application/json`; no bearer | Any status below 500 means “alive”; network/abort errors return false. Polls every 2.5 seconds, default max 60 seconds. | **OK**; endpoint is public and returns success envelope. |
| [`AuthService.getGoogleLoginUrl`](./WorkFlowHub-FE/src/services/api.ts#L230); `AuthModal.triggerGooglePrompt` | Browser navigation GET `/auth/google/login?client_url=<encodeURIComponent(window.location.origin)>` | No fetch body or bearer header; browser navigation. | Full-page redirect to Google; FE OAuth callback parser later reads query. | **OK locally** if `Frontend:Url` matches FE origin; deployment conditional on BE `FRONTEND_URL`, Google configuration and callback URL registration. |
| BE OAuth callback (provider browser redirect; FE parser [`AuthContext.parseInitialOAuthRedirect`](./WorkFlowHub-FE/src/context/AuthContext.tsx#L27)) | GET `/auth/google/callback?code=...&state=...` | No FE JSON body; browser redirect to BE. | FE reads `token`, `refreshToken`, and `user` query values; parses/stores them and replaces URL with pathname. | **OK shape**: .NET redirects with these query keys and URL-encoded user JSON. FE does not directly call this endpoint. |
| [`AuthService.logout`](./WorkFlowHub-FE/src/services/api.ts#L245); `AuthContext.logout` | POST `/auth/logout` | No body; auth header if a token exists. | Expects void data; on failure still clears client auth. | **OK**; .NET route is public and accepts optional bearer. |
| [`AuthService.refreshToken`](./WorkFlowHub-FE/src/services/api.ts#L262) | POST `/auth/refresh-token` | `{refreshToken:string}`; JSON | Auth response expected; no callsite found. | **FE-UNUSED** wrapper; .NET route/body match. No automatic 401 refresh. |
| [`ProjectService.getProjects`](./WorkFlowHub-FE/src/services/api.ts#L304) | GET `/projects?page=0&size=50`; optional URL-encoded `search` | No body; auth | Reads `data.content`/array; list failures become `[]`. | **OK**; page and search query supported. |
| [`ProjectService.getMyProjects`](./WorkFlowHub-FE/src/services/api.ts#L304) | GET `/projects/my-project?page=0&size=50` | No body; auth | Uses page content; list failures become `[]`. | **OK** route/page shape match. |
| [`ProjectService.getProjectById`](./WorkFlowHub-FE/src/services/api.ts#L304) | GET `/projects/{id}` | No body; auth | Expects project in `data`; no FE caller found. | **FE-UNUSED** wrapper; .NET route exists. |
| [`ProjectService.createProject`](./WorkFlowHub-FE/src/services/api.ts#L339); [`CreateProjectModal.handleSubmit`](./WorkFlowHub-FE/src/components/CreateProjectModal.tsx#L27) | POST `/projects` | `{name:string,description?:string,ownerId:number}`; JSON, auth | Reads `data` as Project (notably `id`,`name`); errors propagate. | **OK** request/DTO; .NET returns 201. |
| [`ProjectService.updateProject`](./WorkFlowHub-FE/src/services/api.ts#L347) | PUT `/projects/{id}` | `Partial<CreateProjectPayload>`: optional `name:string`, `description?:string`, `ownerId?:number`; auth | Expects Project in `data`; no caller found. | **FE-UNUSED** wrapper; .NET route/DTO exists. |
| [`ProjectService.deleteProject`](./WorkFlowHub-FE/src/services/api.ts#L347) | DELETE `/projects/{id}` | No body; auth | Expects success; no caller found. | **FE-UNUSED** wrapper; .NET route exists. |
| [`ProjectMemberService.getMembersByProjectId`](./WorkFlowHub-FE/src/services/api.ts#L364); Add member modal / dashboard loaders | GET `/project-members?search=projectId=={id}&page=0&size=100` | No body; auth | Reads page content; list failure becomes `[]`. | **OK** after BE follow-up: response now additively includes nullable `userEmail` from the member's User relation. Route, RSQL `projectId`, and page shape match. |
| [`ProjectMemberService.addMember`](./WorkFlowHub-FE/src/services/api.ts#L379); `AddProjectMemberModal.handleSubmit` | POST `/project-members` | `{projectId:number,userId:number,roleId?:number}`; auth | Reads returned member, including `userName` and `roleName`; mutation errors propagate. | **OK** body/DTO match. |
| [`ProjectMemberService.updateMemberRole`](./WorkFlowHub-FE/src/services/api.ts#L387) | PUT `/project-members/projects/{projectId}/users/{userId}` | `{roleId:number}`; auth | Expects updated member; no caller found. | **FE-UNUSED** wrapper; .NET route/body match. |
| [`ProjectMemberService.removeMember`](./WorkFlowHub-FE/src/services/api.ts#L387) | DELETE `/project-members/projects/{projectId}/users/{userId}` | No body; auth | Expects success; errors propagate. | **OK** route/method match. |
| [`SprintService.getSprintsByProjectId`](./WorkFlowHub-FE/src/services/api.ts#L407); dashboard loaders | GET `/sprints?search=projectId=={id}&page=0&size=100` | No body; auth | Reads page content; errors become `[]`. | **OK** `projectId` RSQL field and page contract match. |
| [`SprintService.createSprint`](./WorkFlowHub-FE/src/services/api.ts#L422); [`CreateSprintModal.handleSubmit`](./WorkFlowHub-FE/src/components/CreateSprintModal.tsx#L36) | POST `/sprints` | `{projectId:number,name:string,goal?:string,startDate:string,endDate:string,status:string}`; auth | Reads result name/data; errors propagate. | **OK** fields match; date strings must be `yyyy-MM-dd` for .NET `DateOnly`. |
| [`SprintService.updateSprint`](./WorkFlowHub-FE/src/services/api.ts#L430); [`EditSprintModal.handleSubmit`](./WorkFlowHub-FE/src/components/EditSprintModal.tsx#L50) | PUT `/sprints/{id}` | `Partial<CreateSprintPayload>`; auth | Reads result; errors propagate. | **OK** fields match; date formatting caveat above. |
| [`SprintService.deleteSprint`](./WorkFlowHub-FE/src/services/api.ts#L430) | DELETE `/sprints/{id}` | No body; auth | Expects success. | **OK** route/method match. |
| [`UserService.getUsers`](./WorkFlowHub-FE/src/services/api.ts#L447); `AddProjectMemberModal.loadUsersAndRoles` | GET `/users?page=0&size=100`; optional URL-encoded `search` | No body; auth | Reads page content; errors become `[]`. UI uses `id`, `name`, `email`, `roleName`, `avatar` as available. | **OK** route and core user fields match; null fields may be omitted. |
| [`UserService.updateUserRole`](./WorkFlowHub-FE/src/services/api.ts#L461) | PUT `/users/{id}` | `{roleId:number}`; auth | Expects updated user; no FE caller found. | **FE-UNUSED** wrapper; .NET `UpdateUserRequest` accepts roleId and optional other fields. |
| [`TaskService.getTasksByProjectId`](./WorkFlowHub-FE/src/services/api.ts#L471); dashboard/project loaders | GET `/tasks?search=projectId=={id}&page=0&size=100` | No body; auth | Reads page content; list errors become `[]`. | **OK** route/filter/page match. |
| [`TaskService.getTasksBySprintId`](./WorkFlowHub-FE/src/services/api.ts#L471) | GET `/tasks?search=sprintId=={id}&page=0&size=100` | No body; auth | Page content expected; no FE caller found. | **FE-UNUSED** wrapper; `sprintId` is in .NET task RSQL whitelist. |
| [`TaskService.getMyTasks`](./WorkFlowHub-FE/src/services/api.ts#L471); `UserDashboard.loadUserTasks` | GET `/tasks?search=assigneeId=={userId}&page=0&size=100` | No body; auth | Reads page content; errors become `[]`. | **OK** route/filter/page match. |
| [`TaskService.getAllTasks`](./WorkFlowHub-FE/src/services/api.ts#L471) | GET `/tasks?page=0&size=100`; optional URL-encoded `search` | No body; auth | Reads page content; errors become `[]`; no FE caller found. | **FE-UNUSED** wrapper; .NET route exists. |
| [`TaskService.createTask`](./WorkFlowHub-FE/src/services/api.ts#L530); [`CreateTaskModal.handleSubmit`](./WorkFlowHub-FE/src/components/CreateTaskModal.tsx#L46) | POST `/tasks` | `projectId:number`, `title:string`; optional `sprintId?:number|null`, `assigneeId?:number|null`, `description?:string`, `priority?:string`, `status?:string`, `dueDate?:string`, `estimate?:string`, `taskCode?:string`, `parentTaskId?:number|null`; auth | Reads returned task fields for UI; errors propagate. | **MISMATCH**: `parentTaskId` has no .NET request DTO property and is ignored. Other fields match; `dueDate` must be `yyyy-MM-dd`. |
| [`TaskService.getTaskById`](./WorkFlowHub-FE/src/services/api.ts#L546); task detail loader | GET `/tasks/{id}` | No body; auth | Uses task detail fields including `description`,`estimate`,`taskCode`; errors propagate. | **OK** route/response shape. |
| [`TaskService.updateTask`](./WorkFlowHub-FE/src/services/api.ts#L546); `EditTaskModal.handleSubmit` and task handlers | PUT `/tasks/{id}` | `Partial<CreateTaskPayload>`; auth | Reads returned task; errors propagate or selected handlers branch on code/status. | **MISMATCH**: FE type permits `parentTaskId` (ignored by .NET); it omits `isDeleted`, which .NET accepts, though dedicated delete/restore calls exist. |
| [`TaskService.deleteTask`](./WorkFlowHub-FE/src/services/api.ts#L546); task handlers | DELETE `/tasks/{id}` | No body; auth | Expects success; errors propagate. | **OK** .NET soft-delete route returns 200 envelope. |
| [`TaskService.restoreTask`](./WorkFlowHub-FE/src/services/api.ts#L546); trash handlers | PUT `/tasks/{id}/restore` | No body; auth | Reads returned task. | **OK** route/method match. |
| [`TaskService.permanentDeleteTask`](./WorkFlowHub-FE/src/services/api.ts#L546); trash handlers | DELETE `/tasks/{id}/permanent` | No body; auth | Expects success. | **OK** route/method match. |
| [`TaskService.getTrashTasks`](./WorkFlowHub-FE/src/services/api.ts#L546); trash loaders | GET `/tasks?search=projectId=={id};isDeleted==true;deletedById=={userId}&page=0&size=100`; retries without `deletedById` when applicable | No body; auth | Reads page content, then may locally filter; failure/fallback behavior can hide service differences. | **OK** RSQL conjunction and fields are supported. **Functional risk:** normal .NET soft delete leaves `deletedById` null per documented preserved behavior, so user-specific trash may be empty. |
| [`TaskService.emptyTrash`](./WorkFlowHub-FE/src/services/api.ts#L615); trash handlers | DELETE `/tasks/projects/{projectId}/trash?all={boolean}` | No body; auth | Expects success. | **OK** route/query match. Java/.NET known trash semantics still apply. |
| [`SubtaskApiService.getSubtasksByTaskId`](./WorkFlowHub-FE/src/services/api.ts#L621); `SubtaskService.fetchSubtasks` | GET `/subtasks/task/{taskId}` | No body; auth | Maps remote subtask fields to local storage/cache; failures return `[]`. | **OK** route returns list. |
| [`SubtaskApiService.getSubtaskById`](./WorkFlowHub-FE/src/services/api.ts#L621) | GET `/subtasks/{id}` | No body; auth | Expects a subtask; no FE caller found. | **FE-UNUSED** wrapper; .NET route exists. |
| [`SubtaskApiService.createSubtask`](./WorkFlowHub-FE/src/services/api.ts#L642); subtask form | POST `/subtasks` | `taskId:number,title:string`; optional `description?:string,assigneeId?:number|null,assigneeName?:string,estimate?:string,priority?:string,dueDate?:string,isCompleted?:boolean`; auth | Maps returned fields including assignee name into local state/cache. | **MISMATCH**: request `assigneeName` is not in .NET DTO and is ignored; the response can provide a derived `assigneeName`. Date must be `yyyy-MM-dd`. |
| [`SubtaskApiService.updateSubtask`](./WorkFlowHub-FE/src/services/api.ts#L650); subtask handlers | PUT `/subtasks/{id}` | Optional `description,assigneeId,assigneeName,estimate,priority,dueDate,isCompleted`; auth | Maps result where used; some update results are ignored. | **MISMATCH**: request `assigneeName` is not accepted. |
| [`SubtaskApiService.deleteSubtask`](./WorkFlowHub-FE/src/services/api.ts#L650); subtask handlers | DELETE `/subtasks/{id}` | No body; auth | Local state updated; failures may be warned while local cache changes. | **OK** route/method match; local/remote divergence on error is possible. |
| [`ActivityLogService.getActivityLogs`](./WorkFlowHub-FE/src/services/api.ts#L667); dashboard activity loader | GET `/activity-logs?page=0&size=20` | No body; auth | List stored/rendered fields include `id`,`userName`,`action`,`target`,`timeAgo`; failure returns `[]`. | **MISMATCH** response: .NET supplies `projectName`,`createdAt`,`updatedAt`, not `target`/`timeAgo`. |
| [`DashboardService.getUserDashboard`](./WorkFlowHub-FE/src/services/api.ts#L706); `UserDashboard.loadDashboardStats` | GET `/dashboard/user` | No body; auth | Reads `totalProjects`,`totalAssignedTasks`,`tasksByStatus`; `overdueTasks` is declared but not visibly used. | **OK** fields match .NET `UserDashboardResponse`. |
| [`DashboardService.getProjectDashboard`](./WorkFlowHub-FE/src/services/api.ts#L706) | GET `/dashboard/project/{projectId}` | No body; auth | Wrapper declares project summary fields; no FE callsite found. | **FE-UNUSED** wrapper; .NET route exists. |
| [`NotificationService.getMyNotifications`](./WorkFlowHub-FE/src/services/api.ts#L724); `NotificationBell.fetchNotifications` | GET `/notifications?search=userId=={userId}&page=0&size=20&sort=createdAt,desc` | No body; auth | Reads page `.content`; bell uses `content`,`isRead`,`type`,`id`,`createdAt`; error returns empty-page fallback. | **OK** route, allowed RSQL field, sorting and response properties match. |
| [`NotificationService.markAsRead`](./WorkFlowHub-FE/src/services/api.ts#L753); `NotificationBell` | PUT `/notifications/{id}` | `{isRead:true}`; auth | Updates local read state after success. | **OK** DTO/route match. |
| [`NotificationService.deleteNotification`](./WorkFlowHub-FE/src/services/api.ts#L753) | DELETE `/notifications/{id}` | No body; auth | Expects success; no FE caller found. | **FE-UNUSED** wrapper; .NET route exists. |

### Request fields and formats

- Auth: login `{email:string,password:string}`; registration `{email:string,password:string,name:string}`; Google token `{idToken:string}`; refresh `{refreshToken:string}`. These match .NET DTOs. FE registration does not supply optional `avatar`.
- Project: create `{name:string,description?:string,ownerId:number}`; update is partial. Member create `{projectId:number,userId:number,roleId?:number}`; role update `{roleId:number}`. These match .NET DTOs.
- Sprint: `{projectId:number,name:string,goal?:string,startDate:string,endDate:string,status:string}`; update is partial. .NET uses `DateOnly?`; expected wire format is `yyyy-MM-dd`. FE types say `string`; actual runtime date-string generation was not separately exercised.
- Task: FE `CreateTaskPayload` has required `projectId:number,title:string` and optional numeric IDs, text fields, `dueDate:string`, and `parentTaskId?:number|null`. .NET request DTO supports all listed task fields except `parentTaskId`; .NET update additionally accepts `isDeleted?:boolean`, absent from FE's `Partial<CreateTaskPayload>`.
- Subtask: FE create has `taskId:number,title:string` and optional description, numeric assignee, `assigneeName`, estimate, priority, date string, and completion boolean. .NET supports those except request `assigneeName`.
- Notification update `{isRead:true}` matches. FE has no create-notification method.
- Most `.NET` nullable response properties are omitted, not emitted as explicit `null`. FE type declarations should treat optional response properties as absent-capable. The FE source scan did not establish runtime handling for every nullable field.

## Authentication and OAuth details

- **Password login:** POST `/auth/login`; FE reads `data.accessToken` and `data.user`. `AuthResponse` also has `refreshToken`, `tokenType`, `expiresIn`; the latter two are not used. FE login handler saves refresh token once, but auth context subsequently clears it by saving with `null`.
- **Google OAuth:** FE health-checks then navigates to `/auth/google/login?client_url=<window.location.origin>`. FE callback handling expects query `token`, `user`, optional `refreshToken`, which matches .NET callback redirect keys. .NET URL-encodes serialized user JSON. Its callback validates/limits the redirect origin to configured `Frontend:Url`; a deployed FE origin will be rejected/fallback to localhost unless `FRONTEND_URL` is set correctly.
- .NET builds OAuth `redirect_uri` from the incoming request scheme/host plus `/api/v1/auth/google/callback`. Google Console's exact registered URI was not inspected. Reverse-proxy forwarded-scheme/host behavior was not verified; `Program.cs` does not visibly configure forwarded headers.
- `POST /auth/login-google` is a separate ID-token API wrapper, not used by the FE UI scan. Do not confuse it with the browser-redirect OAuth flow.
- **Refresh/401:** refresh endpoint wrapper and token storage exist, but no refresh caller/interceptor exists. A protected-route 401 logs out rather than refreshing. This is a lifecycle gap, not an endpoint absence.
- **Logout:** FE attempts POST `/auth/logout`; failures are swallowed for client logout purposes. This matches the backend's public logout route and optional bearer handling.

## Pagination, search, dates, and response fields

### Pagination

FE uses `page=0`, sizes 20/50/100, and `sort=createdAt,desc` for notifications. These are valid .NET parameters; the API supports `page`, `size`, repeatable/comma-form `sort`, default page 0/size 20, and sizes through 2000. .NET returns `data.content`, `page`, `size`, `totalElements`, `totalPages`, `first`, `last`.

The FE list code reads `data.content` (or raw-array fallback), not page totals. FE's declared `pageNumber`/`pageSize` differ from BE's `page`/`size`, but no current list behavior depending on those names was found.

### RSQL fields seen in FE filters

| FE expression/property | .NET support |
|---|---|
| `projectId` (projects, members, sprints, tasks, trash) | Whitelisted for ProjectMember, Sprint, Task; Project has an `ownerId` alias, not a `projectId` filter. FE's `/projects` general `search` content was not observed to use `projectId`. |
| `sprintId`, `assigneeId`, `deletedById`, `isDeleted` (tasks) | All are in .NET Task RSQL whitelist. `;` conjunction is supported. |
| `userId` (notifications) | In .NET Notification RSQL whitelist. |
| General `search` on projects/users | URL-encoded by FE; .NET supports a per-module whitelist, not arbitrary fields. Unknown/malformed filters can return `INVALID_RSQL`; Java-compatible parser detail text is not guaranteed. |

FE interpolates numeric IDs into these RSQL values; general search text is URL-encoded. The identified concrete filter fields above are supported, but a future search term can still name an unsupported field/operator.

### Date/time and nullable response data

- FE date-bearing request types are strings for sprint `startDate`/`endDate`, task `dueDate`, and subtask `dueDate`; .NET `DateOnly` expects ISO `yyyy-MM-dd`. Type declarations alone do not prove every caller formats dates this way. Date values including a time/offset are an unverified risk for these fields.
- .NET `LocalDateTime` responses are local ISO date-time strings without `Z` or offset; FE uses notification/subtask timestamps in browser code. Browser timezone interpretation may differ from UTC expectations. No live time-zone test was run.
- .NET null values are omitted. Main FE fields used for User, Project, Task, Subtask, Sprint, Dashboard and Notification exist in their DTOs, with the explicit differences already called out (`userEmail`, activity `target`/`timeAgo`, and request extras). Null-related display fields such as avatar/name may be absent.
- FE expects/uses these response fields by module: Auth `accessToken`,`user`,`refreshToken`; Roles `id`,`name`; Users `id`,`name`,`email`,`roleName`,`avatar`; Projects `id`,`name`,`description`,`ownerId`,`currentUserRole`; members `userId`,`userName`,`userEmail`,`roleId`,`roleName`; Sprints `id`,`name`,`status`,`projectId`,`startDate`,`endDate`; Tasks `id`,`title`,`status`,`priority`,`dueDate`,`assigneeId`,`assigneeName`,`projectId`,`projectName`,`sprintId`,`isDeleted`,`deletedById`,`deletedByName` (detail may also use `description`,`estimate`,`taskCode`); Subtasks map `id`,`taskId`,`title`,`description`,`isCompleted`,`assigneeId`,`assigneeName`,`estimate`,`priority`,`dueDate`,`createdAt`,`updatedAt`; Activity UI uses `id`,`userName`,`action`,`target`,`timeAgo`; User Dashboard uses `totalProjects`,`totalAssignedTasks`,`tasksByStatus`; Notifications use `content`,`isRead`,`type`,`id`,`createdAt`.

## Statuses, mismatches, and recommendations (BE-only changes proposed)

The API method/path pairs used by FE are present in .NET; there are no confirmed `BE-MISSING` routes. The **endpoint layer** is mostly OK, while the mismatches below are payload/response semantics:

| Severity | Mismatch / risk | Evidence and impact | Suggested BE-side action only (not performed) |
|---|---|---|---|
| High | Local FE does not target local .NET by default | Example `.env` and Vite proxy point to hosted Render backend; `.env` absent. BE appsettings and launch profile disagree on port. | Align local API URL/proxy with verified listener; reconcile BE launch URL configuration. |
| High | Production OAuth origin/config may not match | BE default `Frontend:Url` is localhost:3000; callback uses validated configured origin. Google client credentials default empty; callback URI is derived from request host/scheme. | Set production `FRONTEND_URL`/Google credentials and verify the exact registered callback URI and proxy forwarding. |
| High | Refresh token is lost and no auto-refresh exists | FE AuthContext saves auth with null refresh token after login; protected 401 clears auth and logs out. | Preserve refresh token through auth-context save and implement a deliberate BE-compatible refresh lifecycle if desired. FE changes are outside this audit's allowed scope. |
| Medium | Task `parentTaskId` / subtask `assigneeName` are FE-only request fields | Not present on .NET request DTOs; extra JSON members are ignored. If UI expects these fields to persist, parent association/name is lost. | Add DTO/domain persistence only if the product contract requires it; otherwise remove/stop relying on these FE-only properties in a separately authorized FE change. |
| Resolved (BE) | Member `userEmail` response field absent | FE roster consumes `userEmail`; initial DTO omitted it. | Implemented as an additive nullable field mapped from the loaded User; no FE files changed. Regression test added. |
| Medium | Activity `target` / `timeAgo` response fields absent | FE activity renderer uses them; .NET ActivityLogResponse exposes action, related names, and timestamps instead. | Map the expected presentation fields in the BE response or align contract separately. |
| Medium | Page metadata names differ | FE type uses `pageNumber`/`pageSize`; BE serializes `page`/`size`. Current FE uses content only, so no current break was observed. | Consider a compatibility alias only if FE starts consuming metadata or its type is treated as authoritative. |
| Medium | Validation details are discarded | .NET may return `errors[]`; FE only extracts `code`, `message`, or `error`, so field-level errors are not displayed. | Provide a useful top-level `message` (already present) and consider a stable FE-compatible validation message; surfacing fields requires FE work. |
| Medium | Soft-deleted task trash may not populate for the current user | Existing documented .NET/Java behavior leaves `deletedById` null on normal soft delete, while FE filters trash by `deletedById==userId`. | Correct the trash ownership behavior only with explicit authorization to depart from parity; not changed here. |
| Low | Date/time format assumptions | FE types are string; .NET LocalDate requires `yyyy-MM-dd` and LocalDateTime has no zone suffix. No runtime payload test was done. | Verify actual serialized values in a local integration test; preserve offset-free response contract or document conversion. |
| Low | Permissive CORS origin pattern | BE allows `*` with credentials enabled. FE currently sends bearer headers without cookie credentials. | Restrict production allowed origins to deployed FE origins. |
| Low | FE fallback behavior can mask BE errors | Role fallback roles, empty-list fallbacks and subtask local cache may make a failed API look like valid empty/mock data. | Keep monitoring/logging explicit at integration boundaries; distinguish fallback data in UI in a separately authorized FE change. |

`MISMATCH` is not assigned to a route solely because it is unused. It indicates a confirmed request/response field or behavior gap. The `.NET` parity docs report that all 71 Java controller route/method pairs exist, while also documenting known parser, validation-message, PostgreSQL and OAuth configuration limitations.

## Backend routes not called by FE

No FE callsite was found for these .NET controller methods. They are `FE-UNUSED`, not `BE-MISSING`:

| Module | Unused .NET methods/routes |
|---|---|
| Auth | POST `/api/v1/auth/login-google` (wrapper-only); POST `/api/v1/auth/refresh-token` (wrapper-only); POST `/api/v1/auth/change-password` (no FE method/caller). OAuth callback is reached by provider redirect and is part of the observed flow, not a direct fetch. |
| Roles | GET `/api/v1/roles/{id}`, POST `/api/v1/roles`, PUT `/api/v1/roles/{id}`, DELETE `/api/v1/roles/{id}`. |
| Users | GET `/api/v1/users/{id}`, POST `/api/v1/users`, PUT `/api/v1/users/{id}` (wrapper-only role updater), DELETE `/api/v1/users/{id}`. |
| Projects | GET `/api/v1/projects/{id}`, PUT `/api/v1/projects/{id}` (wrapper-only), DELETE `/api/v1/projects/{id}` (wrapper-only). |
| Project members | GET `/api/v1/project-members/projects/{projectId}/users/{userId}`, PUT the same composite-key route (wrapper-only role updater). |
| Sprints | GET `/api/v1/sprints/{id}`. |
| Subtasks | GET `/api/v1/subtasks` (paged collection), GET `/api/v1/subtasks/{id}` (wrapper-only). The task-scoped list route is used. |
| Activity logs | GET `/api/v1/activity-logs/{id}`, POST `/api/v1/activity-logs`, PUT `/api/v1/activity-logs/{id}`, DELETE `/api/v1/activity-logs/{id}`. |
| Dashboard | GET `/api/v1/dashboard/project/{projectId}` (wrapper-only). |
| Notifications | GET `/api/v1/notifications/{id}`, POST `/api/v1/notifications`, DELETE `/api/v1/notifications/{id}` (wrapper-only). |
| Comments | All five routes: GET collection, GET by ID, POST, PUT by ID, DELETE by ID. |
| Attachments | All five routes: GET collection, GET by ID, POST, PUT by ID, DELETE by ID. |

**Count:** 37 .NET route/method pairs not called by scanned FE source, including wrapper-only methods. This count excludes non-HTTP support features and the OAuth callback reached through Google redirect.

## FE paths that are not ready to rely on with local .NET

1. With the checked-in FE example environment/proxy, calls go to the hosted Render URL, not local `PRN232`. If `VITE_API_URL` is unset, the Vite proxy still targets that hosted URL.
2. The local .NET listener must be confirmed because checked-in API URL settings disagree (8080 vs 5101). The previously observed run was 8080.
3. Google OAuth is not verifiably ready from defaults: credentials are empty, the BE frontend origin defaults to localhost:3000, and the callback URI/forwarded scheme host registration has not been validated. Google login is the only login method that depends on this setup; password login is a separate API contract.
4. Member email and activity target/time labels will be absent from .NET responses, and FE-only task/subtask request properties will not be applied.
5. Session/refresh continuity is not implemented: a 401 ends the session rather than renewing it, and the password-login context drops the refresh token.
6. FE screens for comments and attachments cannot obtain live data from these controllers because no FE calls exist; attachment fixtures are mock data and no upload/download request was found.
7. Page content, common entity fields and core auth payloads appear compatible by static contract review, but live serialization/date handling and database-backed behavior remain unverified.

## Evidence references

- FE API implementation and methods: [`src/services/api.ts`](./WorkFlowHub-FE/src/services/api.ts)
- FE request/response types: [`src/types/index.ts`](./WorkFlowHub-FE/src/types/index.ts)
- FE auth callback and lifecycle: [`src/context/AuthContext.tsx`](./WorkFlowHub-FE/src/context/AuthContext.tsx), [`src/utils/tokenStorage.ts`](./WorkFlowHub-FE/src/utils/tokenStorage.ts)
- FE environment/proxy: [`.env.example`](./WorkFlowHub-FE/.env.example), [`vite.config.ts`](./WorkFlowHub-FE/vite.config.ts)
- .NET controllers and wire DTOs: [`PRN232.Api/Controllers/`](./PRN232.Api/Controllers), [`PRN232.Application/`](./PRN232.Application)
- Existing migration comparison: [`ENDPOINT_PARITY.md`](./ENDPOINT_PARITY.md), [`MIGRATION_PROGRESS.md`](./MIGRATION_PROGRESS.md), [`KNOWN_ISSUES.md`](./KNOWN_ISSUES.md)
