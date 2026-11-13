# Institutional academic administration

Use an InstitutionAccess token for the current active InstitutionAdmin membership.
Owner IDs, resource IDs and audit fields are never accepted in bodies. Unknown JSON
properties and unknown list query parameters are rejected. Foreign and missing IDs
both return 404. No legacy or PlatformOperator token grants administrative access.

Catalog routes under `/api/institution`: `study-areas`, `study-paths`, `biennia`,
`biennium-study-areas`, `biennium-study-paths`. Each supports GET list, GET `/{id}`,
POST and DELETE `/{id}`. The first three also support PUT `/{id}`. Structural
associations have no PUT. New IDs are server generated. POST returns 201 with a
GUID body and recoverable Location. PUT/DELETE return 204.

List query: `pageNumber=1`, `pageSize=20` (1-100). Response:
`{items: [...], totalCount, pageNumber, pageSize}`. Ordering is deterministic by
name/id, dates/id or association id. Filtering and pagination execute in SQL.

StudyArea input: `{name, description}` (description required, may be empty).
StudyPath: `{name, description?}`. Name is trimmed, required, max 100; MySQL
collation determines uniqueness within the tenant. Descriptions are TEXT (UTF-8
byte limit 65535). Biennium: `{startYear:"2030-01-01",endYear:"2032-01-01"}`;
DateOnly, years 1000-9999, end >= start, unique pair per tenant.
BienniumStudyArea: `{bienniumId,studyAreaId}`.
BienniumStudyPath: `{bienniumStudyAreaId,studyPathId}`.
All referenced resources must belong to the current tenant.

Details contain id, input fields and revision. GET detail returns `ETag: "1"`.
PUT requires `If-Match: "1"`: missing 428, malformed 400, stale 409. Reload the
detail after a conflict. Existing resources have revision zero until their first
audited mutation. Deletion requires no live dependencies and retains the ledger.

Errors use ProblemDetails: invalid input 400, unauthenticated 401, forbidden 403,
missing/foreign 404, duplicate/stale/dependent resource 409. SQL details are hidden.

Catalog availability requires `Multitenancy:SchoolApi:Enabled`, false by default.
Contracts alone do not enable rollout. Course edition assignment, student transfers,
historical reassignment and archival remain separate workflows.

## ClassSubject

`/api/institution/class-subjects` now supports list, detail, POST, PUT and DELETE.
POST body: `{classId,subjectId,teacherId}`. PUT body: `{teacherId}` only, with
If-Match. ClassId and SubjectId cannot be changed. All three references must be
owned by the current tenant. MySQL enforces unique class/subject pairs.

GET list preserves the previous array shape and the `classSubjectId` field used
by exam selectors. Each item additionally has `id` and `revision`. Existing names
and teacher fields remain available. Pagination defaults to 1/20, max 100;
`X-Total-Count` contains the filtered total. Optional `classId`, `subjectId` and
`teacherId` filters execute in SQL. Teachers retain access only to their own
assignments; other operations require InstitutionAdmin. Detail returns ETag.

Any Exam, Grade or other incoming dependency blocks teacher reassignment and
DELETE with 409, even before a student starts the exam. No attempt, result or
grade is deleted or moved. A historical teacher change needs a future dedicated
workflow. This surface retains the existing SchoolApi + ExamApi rollout gate.

## Campus

`/api/institution/campuses`: GET list/detail, POST, PUT and DELETE; all require
InstitutionAdmin and the existing SchoolApi + CourseApi rollout gate.
Create body: `{name,address?,city?,province?,isActive?}` (active defaults true).
PUT is a full descriptive replacement with the same fields and **explicit**
`isActive`, plus mandatory If-Match. Limits: name 200, address 300, city/province
100. Name is required and trimmed; blank optional fields become null.

GET list preserves the previous array and `campusId` field, adds `id`/`revision`,
and returns `X-Total-Count`. Paging defaults to 1/20, max 100; optional `isActive`
filter executes in SQL. Detail returns ETag. Active/inactive campuses are listed
by default. No uniqueness rule for campus names is invented.

Set `isActive:false` to deactivate and true to reactivate. CourseEdition links
and historical events remain intact. DELETE rejects CourseEditionCampus,
course_administration_event or any other incoming FK dependency with 409. The
shared resource mutation ledger survives deletion of a genuinely unused campus.
Existing `/course-editions/{id}/campuses` linking remains the only linking workflow.
