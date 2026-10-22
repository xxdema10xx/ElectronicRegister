# Institution Class API

All five routes require a current `InstitutionAccess` bearer token with the
`InstitutionAdmin` role. The membership, security generation and institution are
revalidated. `Multitenancy:SchoolApi:Enabled` defaults to false. Teacher, Student,
legacy admin and PlatformOperator tokens do not grant access.

| Method | Route | Successful response |
| --- | --- | --- |
| GET | `/api/institution/classes` | 200, paged DTO |
| GET | `/api/institution/classes/{id}` | 200, class DTO and ETag |
| POST | `/api/institution/classes` | 201, server UUID and Location |
| PUT | `/api/institution/classes/{id}` | 204 |
| DELETE | `/api/institution/classes/{id}` | 204 |

List parameters: `pageNumber=1`, `pageSize=20` (1-100), optional
`bienniumStudyPathId` and `courseEditionId`. Ordering is MySQL Name, then Id.
Filters, ordering and pagination run in SQL; unassigned classes are included.
Unknown query parameters are rejected, including `institutionId`.

List response:

```json
{
  "items": [
    {
      "id": "11111111-1111-4111-8111-111111111111",
      "name": "Classe 1",
      "bienniumStudyPathId": "22222222-2222-4222-8222-222222222222",
      "courseEditionId": null,
      "revision": 1
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 20
}
```

Detail returns one item in this same shape. No EF entity or tenant selector is
exposed. Missing IDs and IDs owned by another institution both return 404.

POST accepts **only** `name` and `bienniumStudyPathId`. Name is trimmed and must
contain 1-20 characters after trimming. The study path must belong to the current
institution. IDs and owners are generated/resolved on the server. CourseEdition
assignment uses the existing dedicated workflow:
`POST /api/institution/course-editions/{editionId}/classes/{classId}`.

PUT accepts **only** `name` and requires `If-Match: "<revision>"`, where revision
comes from the last detail/list response. Example for React Native:

```ts
const response = await fetch(`${baseUrl}/api/institution/classes/${item.id}`, {
  method: "PUT",
  headers: {
    Authorization: `Bearer ${institutionAccess}`,
    "Content-Type": "application/json",
    "If-Match": `"${item.revision}"`,
  },
  body: JSON.stringify({ name: newName }),
});
if (response.status === 409) {
  // Refetch the detail, display the current name, and let the user retry.
}
```

Revisions are durable class-CRUD audit sequence numbers, not CourseEditionId and
not in-memory state. Existing classes start at revision 0. Every accepted rename,
including a rename to the current name, advances the revision. Two writes based on
the same revision cannot both succeed. CourseEdition attribution remains a separate
workflow: renaming changes only Name and preserves its current attribution.

DELETE does not detach students or assignments and does not cascade educational
data. Only an unassigned class without students, assignments or administrative FK
references can be deleted. The dedicated append-only snapshots remain after deletion;
their class ID is a historical identifier, deliberately not a live Class FK.

Errors: 400 invalid input/unknown JSON members, 401 invalid token, 403 insufficient
role/revoked authorization/disabled rollout, 404 unavailable target, 409 duplicate
name, stale revision, dependencies or SQL integrity race, 428 missing If-Match.
Duplicate comparison uses the actual MySQL utf8mb4_unicode_ci unique constraint.
SQL internals are never returned. Client-selected IDs, tenant/audit/security fields
and structural PUT fields are rejected rather than silently ignored.

This version does not move classes between paths, transfer students, change course
editions, archive classes or expose a class-audit browsing API. Retirement of a class
with data requires a separately designed archival workflow.
