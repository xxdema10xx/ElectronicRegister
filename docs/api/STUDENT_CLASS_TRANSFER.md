# Student class transfer

Administrative workflow, frontend-ready, disabled operationally by the existing
rollout flags. This is an application policy; the three-month default is not
asserted to be a national legal obligation. No React Native screen is added.

## Access and endpoints

Only the active InstitutionAdmin of the verified current membership may use:

- `POST /api/institution/students/{id}/transfer-class`: requires SchoolApi **and**
  CourseApi. Uses the existing School endpoint policy and an additional fail-closed
  Course rollout filter; it does not combine incompatible endpoint markers.
- `GET /api/institution/students/{id}/class-history`: requires SchoolApi and the
  same current administrator authorization.

Legacy tokens, Student, Teacher and PlatformOperator do not gain this capability.
An absent/foreign-tenant student or destination ID returns indistinguishable 404.
No InstitutionId is accepted as an authorization source. Unknown body fields and
unsupported query parameters are rejected. PUT student remains name-only and
rejects ClassId; creating a student with an initial class is unchanged.

## POST contract

```http
POST /api/institution/students/11111111-1111-4111-8111-111111111111/transfer-class
Authorization: Bearer <InstitutionAccess>
Content-Type: application/json

{
  "eventId": "22222222-2222-4222-8222-222222222222",
  "toClassId": "33333333-3333-4333-8333-333333333333",
  "effectiveDate": "2026-10-23",
  "reason": "Cambio gruppo scolastico autorizzato",
  "expectedRevision": 0
}
```

All five fields must be supplied. GUIDs must be nonempty. Reason is trimmed,
required, non-whitespace and at most 1000 characters. The payload limit is 16 KiB.
The effective date must equal today's operational date computed by the server;
backdated and future operations are rejected. Read currentRevision from history
before submitting. Revision zero means no recorded transfers, not reconstructed
legacy history. This contract uses the explicit expectedRevision body precondition,
not If-Match/ETag; missing fields return 400 rather than 428.

Successful first application and replay both return **200 OK**:

```json
{
  "eventId": "22222222-2222-4222-8222-222222222222",
  "studentId": "11111111-1111-4111-8111-111111111111",
  "fromClassId": "44444444-4444-4444-8444-444444444444",
  "toClassId": "33333333-3333-4333-8333-333333333333",
  "fromCourseEditionId": "55555555-5555-4555-8555-555555555555",
  "toCourseEditionId": "66666666-6666-4666-8666-666666666666",
  "effectiveDate": "2026-10-23",
  "appliedLimitMonths": 3,
  "revision": 1,
  "occurredAtUtc": "2026-10-23T08:00:00Z"
}
```

Create one EventId for each administrative intention. On a lost response, resend
identical field values with the same EventId and actor. The stored receipt is
returned even after later transfers, policy changes or the original date passing.
Authorization is always checked again. A different actor, payload, student or
revision cannot claim an existing event ID (409). On a stale revision, refresh
history and review the intention; do not blindly retry a different transfer.

## Administrative policy

Options, validated at startup:

```json
{
  "AcademicTransfers": {
    "MaxMonthsFromCourseStart": 3,
    "TimeZoneId": "Europe/Rome"
  }
}
```

The supported configured limit is 1–120 calendar months. The deadline is
CourseEdition.StartDate.AddMonths(limit), inclusive, calculated with DateOnly.
The server uses TimeProvider and the configured timezone, including DST. One
captured server instant supplies both the operational date and UTC event instant.
There is no HTTP policy editor, discretionary override or per-institution override
in this version; a future tenant policy can replace the global configuration.

Both classes must have a real, tenant-owned CourseEdition with a non-null official
StartDate and a compatible study path. Planned/active editions are eligible;
completed/cancelled editions are rejected. For different editions **both** course
start deadlines must pass, including the source deadline. Missing legacy links or
dates cause 409; the workflow never invents editions, dates or past events.

Within the same edition: source enrollment must be enrolled, effective and without
a completion date. Only current ClassId, transfer revision and audit change; no
enrollment or enrollment event is created.

Between editions of the same institution: the source must be enrolled. It becomes
withdrawn with CompletionDate equal to the verified effective date. Its original
EnrollmentDate remains unchanged. A new enrolled destination relation is created
with that effective EnrollmentDate. Any existing destination relation in **any**
state causes 409; no reactivation or second episode is inferred. Both enrollment
events include a TransferEventId snapshot link and are atomic with the transfer.

## Exams and grades

An unsubmitted in_progress attempt whose deadline is still in the future blocks
transfer (409). An expired attempt cannot accept new answers and is not treated
as an active attempt; the existing expiry worker is responsible for its lifecycle.
The workflow does not force submission or alter deadlines, answers or statuses.

Unstarted old-class assignments remain stored but disappear from current assigned
exams and cannot be started. No destination exams are assigned automatically.
Already submitted attempts remain visible to their student; their original teacher
can still review Open answers and finalize. Randomization, persistent IDs, grades,
question positions and historical provenance are preserved. START and transfer
serialize on the same tenant-owned Student row before any exam row lock.

The complete grade list remains a historical dossier. After a recorded cross-course
transfer, statistics include only grades belonging to the current CourseEdition;
old-course grades are not recognized as credits. Same-edition class changes retain
course grades. Untransferred legacy statistics retain their existing semantics.

## GET history contract

`?pageNumber=1&pageSize=20`; pageSize maximum 100. SQL pagination; order is
OccurredAtUtc, RevisionAfter, EventId. Response:

```json
{
  "items": [],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "currentRevision": 0
}
```

Each item contains `receipt` (the POST receipt), snapshot names for both classes
and courses, source/destination enrollment IDs, reason, ActorUserId,
ActorMembershipId, RevisionBefore and both course start dates used for the decision.
Snapshot names and applied limits remain historical even if configuration or names
change. Empty history is returned for a legacy student with no recorded transfer.
No other student, teacher or tenant may read the administrative ledger.

## Errors and data protection

400 invalid input/date/unknown fields; 401 absent or invalid authentication; 403
role, revoked membership or disabled rollout; 404 unavailable tenant resource;
409 stale revision, EventId conflict, active attempt, missing links/dates, expired
window or incompatible enrollment/course; 413 oversized body. Errors use
ProblemDetails and do not expose SQL identifiers or foreign-tenant information.

The ledger is append-only and retains tenant-aware RESTRICT references to student,
classes, courses, enrollments and actor membership. Deletions that would break
history are blocked; no cascade is used to remove educational data. One READ
COMMITTED transaction, persistent revision and current row locks prevent stale
snapshots, partial commits and simultaneous incompatible transfers. IAM locks are
tenant-first and privileges are revalidated within the transaction.

Retention, lawful rectification and administrative archiving require separate
approved policies. No legal retention period, cross-institution transfer, automatic
credit recognition, retroactive correction, future scheduling, structural class
move or repeated enrollment episode is implemented.
