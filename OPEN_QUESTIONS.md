# Open questions and migration decisions

## Decisions from the current request

- Kafka is excluded: do not add `Confluent.Kafka`, producers, consumers, or topics.
- Redis is excluded: do not add Redis packages, `IDistributedCache`, dashboard caching, or cache eviction.
- Assignment event effects will be persisted through a synchronous `IAssignmentNotifier` after the main Task transaction, with failures logged but not allowed to fail the main business operation.
- Suspected Java bugs and missing authorization checks must be preserved and recorded in `KNOWN_ISSUES.md`, not corrected during migration.
- No EF migrations or schema changes.

## Unresolved

- `docs/java-samples.md` is not present at the supplied Java source root or target workspace. Current implementation will use the Java source and `MIGRATION_INVENTORY.md` as the contract; confirm if the samples are stored elsewhere.
- The target .NET directory has no `.git` metadata. The user chose to proceed with this batch without per-module commits; no repository was initialized. Future requests that require commits need a target Git worktree.
- Users is implemented and its focused tests pass. PostgreSQL-backed verification of case-sensitive email equality, unique constraint behavior, and EF transaction behavior remains outstanding.
- Project is implemented with the owner membership writer using the same scoped `AppDbContext`; a PostgreSQL-backed failure/rollback test is still needed to verify the two `SaveChangesAsync` calls roll back together against the deployed provider/schema.
- ProjectMember is implemented with the Java composite key and ordered delete checks. Confirm behavior against live JWT role claims and PostgreSQL foreign keys; unit tests use the Java-compatible `roles=ADMIN` claim and do not exercise a running Spring/.NET pair.
- ActivityLog CRUD and whitelisted RSQL are implemented from source; verify PostgreSQL FK behavior, nested related-name projection and database paging/sorting against the deployed provider.
- Notification CRUD and the direct assignment notifier are implemented; PostgreSQL-backed FK/persistence verification is outstanding. Task and Subtask create/update call sites invoke the notifier under their Java conditions.
- Sprint CRUD and the owner/PM-only update guard are implemented; verify PostgreSQL relation replacement, DateOnly filtering and sort translations against the deployed provider. The Java update authorizes against the old project before applying a new `projectId`, which is intentionally preserved.
- Task CRUD/trash and Attachment metadata CRUD are implemented; PostgreSQL-backed soft-delete, hard-delete, filtered RSQL, FK/cascade, assignment-notification timing and transaction behavior remain unverified. No Redis/Kafka integration or EF migration was added.
- Comment CRUD and whitelisted RSQL are implemented; verify PostgreSQL FK behavior, paging/sorting and ID-only create response mapping against the deployed provider. No service-level authorization is present in Java and none was added.
- Subtask CRUD, authorization and direct assignment notification are implemented; verify PostgreSQL FK behavior, eager-loading of a soft-deleted parent task, RSQL paging/sorting and notification persistence/timing against the deployed provider. No EF migration or Kafka integration was added.
- Dashboard routes are implemented with direct DB queries and no cache/membership check; verify aggregate filters, first-ACTIVE sprint selection, response serialization and query plans against PostgreSQL.
- The shared .NET RSQL parser now matches Java's unknown-field error text and SQL null semantics for `=in=`, `=out=`, and `!=`. Malformed syntax/conversion errors still do not reproduce the RSQL library's detailed Java parser text; status and error envelope match, but exact malformed-query text remains a parity gap.
- No Java `RoleServiceTest.java` was found under `src/test`; create .NET tests from observed service behavior and endpoint contract unless a test source is provided.
- Confirm actual PostgreSQL column widths and applied Flyway migrations against the target database before any production rollout. Existing EF entity declarations were based on the checked-in DDL, not a live schema inspection.

## Exact Java messages/edge behavior to check during parity

- Invalid RSQL parser messages can include library-specific parser details; compare the source `INVALID_RSQL` body for representative malformed queries.
- JWT security filter error suffix text is Spring Security version-dependent; existing auth migration currently uses a fixed suffix. Confirm whether exact dynamic exception text is a frontend contract.
- BCrypt `$2a$` interop is covered by format/verification tests generated with BCrypt.Net; no historical production hash sample is available in this workspace.
- Google OAuth redirects now reject non-configured origins, but access/refresh tokens remain in the configured frontend URL query to preserve the current FE callback contract. Moving tokens out of URLs requires a coordinated FE change (for example a one-time authorization code).
- Role-management endpoints remain authenticated-only, matching Java, per the user's explicit parity choice. This preserves the role-escalation risk described in `KNOWN_ISSUES.md`; decide separately whether to harden these routes to admin-only.
- All 71 Java controller routes are represented in `ENDPOINT_PARITY.md`. Final review removed numeric route constraints, aligned invalid numeric binding errors and Spring `Pageable` normalization, and aligned sort failure status. Remaining known differences are support surfaces (request-ID filter and OpenAPI security metadata), malformed-RSQL/validation text, and provider-level behavior.
- Decide whether to add the non-endpoint support features `X-Request-ID` generation/log correlation and the Java OpenAPI BearerAuth metadata; both are inventoried as not fully migrated in `ENDPOINT_PARITY.md`.
- Verify page sorting, especially entity-property names and unsupported sort behavior, against the deployed PostgreSQL/Spring version; the .NET target currently uses explicit entity-selector whitelists.
- The existing FE contract expects Google OAuth tokens in the configured frontend URL query. Confirm when FE can switch to a one-time callback code so tokens are no longer carried in URLs.
