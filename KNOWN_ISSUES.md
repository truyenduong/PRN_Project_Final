# Known Java behavior to preserve during migration

These are observed implementation details and suspected bugs, not fixes approved as part of migration. Port the behavior and add parity tests where applicable.

- `TaskService.deleteTask` computes `currentUserId` with `ObjectUtils.isEmpty(SecurityUtils.getCurrentUserId()) ? ... : null`, so an authenticated non-null ID follows the `null` branch and is not assigned to `deletedBy`. Preserve this expression's effective behavior.
- Because `deletedById` stays null on normal soft deletes, `emptyTrash(all=false)` filters for `deletedById == currentUserId` and therefore does not remove tasks deleted through this flow. Preserve this interaction; `all=true` remains the project-wide purge path for exact `ADMIN` or PM member.
- Task list search appends `isDeleted==false` unless the case-sensitive search text contains `isDeleted`; the EF repository bypasses the entity query filter so explicit deleted-task searches and ID lookups retain Java behavior.
- Task update authorization is only evaluated when a non-null status changes case-insensitively or a non-null assignee changes. Those changes allow exact `ADMIN`/`ROLE_ADMIN`, project owner, PM member or current assignee. Create, other field updates, delete, restore and permanent delete have no additional service-level permission check in Java.
- Task assignment notifications are emitted on create only when assigned, and on update only when the non-null assignee differs from the previous assignee. The .NET notifier writes after the main task transaction and logs/suppresses persistence errors; Kafka delivery and Redis invalidation are excluded.
- Attachment CRUD has no service-level project/task authorization in Java; preserve authenticated-only route behavior without adding ownership checks.
- `TaskService.evictTaskCaches` appears to test cache emptiness before eviction in one branch. Redis/cache behavior is intentionally excluded from the .NET target; no cache eviction is ported.
- Project/trash checks are not uniform: `TaskService.emptyTrash(all=true)` checks admin or project-member PM and has no project-owner bypass; other task/subtask mutation checks allow owner/admin/PM combinations as implemented.
- `SprintService.update` allows project owner or project-member PM only; no admin bypass. Sprint create/delete have no service-level permission check.
- `ProjectMemberService.create` and `update`, and several other CRUD methods, do not check project membership/ownership. Do not silently add such checks.
- ActivityLog CRUD has no service-level project/member authorization in Java; preserve this on all five routes (the shared authenticated-route policy still applies).
- Comment CRUD has no service-level task/member authorization in Java; preserve this on all five routes (the shared authenticated-route policy still applies). The create mapper builds ID-only Task/User references, so the create response's `userName` is null until a later read.
- Dashboard's user `totalProjects` counts project-member rows only (not ownership), while project-dashboard access has no membership check. The first ACTIVE sprint is selected without an explicit sort, completion is rounded with Java `Math.round` to two decimals, and dashboard data is uncached in this migration.
- Notification CRUD does not restrict reads/updates/deletes to the authenticated recipient in Java; preserve this behavior rather than adding an ownership check.
- ActivityLog and Notification create mappers build associated User/Project references from IDs only, so their create responses omit related display names even though subsequent reads can include them.
- `SprintService.update` authorizes against the sprint's existing project before applying an optional new `projectId`; a project owner/PM can therefore move the sprint to a project they do not manage. Preserve this order and do not add a second check.
- Sprint create and updates that include `projectId` use an ID-only Project reference, so Java mapper responses may omit `projectName` for those writes; ordinary reads include it.
- `ProjectMemberService.delete` checks in this order: target exists, current user, self-removal, project lookup, owner-removal, caller's admin/owner/PM permission, delete.
- The Java ProjectMember delete admin check compares the authority to the exact string `"ADMIN"` (not `"ROLE_ADMIN"`); `.NET` preserves the literal. Its PM check is case-insensitive through `RoleEnums.PROJECT_MANAGER.matches`.
- ProjectMember `create` delegates to Spring Data `save` with a non-null composite ID, so an existing `(projectId,userId)` is merged/updated rather than always inserted. The .NET repository preserves this upsert-like behavior.
- User CRUD has no service-level admin/owner check. `UsersService.updateUser` only checks for duplicate email when the requested email differs case-insensitively from the current email, then applies the supplied casing; preserve that behavior.
- `UsersService.createUser` assigns the exact `"User"` role and hashes the supplied password with the same BCrypt cost (10) as the migrated Auth service. A missing default role is reported with the Java `User not found with id: null` message.
- Project creation only rejects when the owner's existing project count is greater than five, so an owner at exactly five projects can create one more. Owner membership creation is part of the same transaction.
- Project CRUD has no general owner/member authorization check in Java; `.NET` preserves the authenticated-only route behavior. `getMyProject` includes projects owned by the caller or represented by a `project_members` row and derives `currentUserRole` from that row, falling back to `PROJECT_MANAGER` for an owner with no non-empty membership role.
- `SubtaskService.handleUpdateAssign` compares a non-null requested assignee ID with `entity.getAssignee().getId()` without first guarding a null existing assignee; the source can throw. Do not “fix” this as part of parity work.
- Subtask assignment notifications are skipped when the new subtask assignee matches the parent task assignee. The .NET path writes directly to the notification table after the subtask transaction and suppresses/logs notifier persistence failures; Kafka delivery is intentionally excluded.
- `RoleService.updateRole` checks a case-insensitive name difference before checking duplicates, but then applies the requested casing to the existing role. The .NET update preserves this behavior.
- Java Role CRUD requires authentication but does not require an admin role. This allows an authenticated user to rename the default `User` role to `ADMIN`, which can grant admin claims to later registrations. This was reported in the security review; the user chose to preserve Java parity for this task, so it remains unresolved until separately authorized.
- Assignment event send failure may trigger notification persistence fallback in `TaskEventProducer`; source catch/log behavior and transaction timing need to be retained as closely as possible in the explicitly requested synchronous notifier implementation.
- Google OAuth callback catches service exceptions and redirects an error query parameter rather than returning the regular exception body. Google flows are part of existing auth but not part of this business-module batch.
- Redis and Kafka are intentionally excluded by the current request, despite the Java project using both.

## Remaining source-to-target parity gaps

- Java `RequestIdFilter.doFilterInternal` is not ported: .NET does not generate/echo `X-Request-ID` or add it to request-scoped logging context.
- Java `OpenApiConfig.customOpenAPI` is only partially represented: Swagger UI is enabled, but the OpenAPI title/description/version and BearerAuth security scheme are not configured.
- RSQL malformed syntax/conversion details and some DTO validation text can differ because .NET uses a separate parser and validation runtime. Status and broad error-envelope shape are retained.
- Java support types `BasePageRequest`/`BaseSortRequest` and `RsqlUtils` are not ported as APIs; none of the 71 controllers uses those request DTOs or utility methods. Endpoints use Spring `Pageable`, whose effective defaults and max size are represented in the .NET page parser.
- The Java generic `BaseServiceImpl` methods are not exposed as a .NET base class; CRUD behavior is implemented explicitly in domain services/repositories. No endpoint dependency on the generic base implementation remains.
- Exact PostgreSQL-backed sort, collation, FK, timestamp and transaction behavior has not been verified against a live database. Unsupported sort fields and malformed sort directions now use the generic 500 response path, matching Java's uncaught property/direction failures.
