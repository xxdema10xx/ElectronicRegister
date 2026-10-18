# Matrice endpoint - audit finale backend

Inventario ricavato dai controller finali e verificato staticamente. Non equivale a un test HTTP di ogni combinazione. A=admin, T=teacher, S=student; anonimo solo login. Errori comuni: 400 binding/argomenti, 401 token assente/non valido, 403 ruolo/ownership, 404 risorsa; 409 regola di business. Errori legacy non classificati possono ancora essere 500: vedere il report.

| Metodo | Route | Ruoli | Parametri/input | Output dichiarato o effettivo | Successo / assenza |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/Auth/login` | anonimo | `LoginDto dto` | `{ token: string }` | 200 |
| POST | `/api/Auth/microsoft-login` | anonimo | `[FromBody] MicrosoftLoginDto dto` | `{ token: string }` | 200 |
| POST | `/api/Auth/register` | admin | `RegisterDto dto` | `string` | 200 |
| POST | `/api/Auth/RegisterForAdmin` | admin | `RegisterForAdminDto dto` | `string` | 200 |
| GET | `/api/Auth/me` | teacher,admin,student | `-` | `UserDto` | 200 (404 su risorsa assente) |
| GET | `/api/Biennium/count` | admin | `-` | `int` | 200, anche zero/collection vuota |
| GET | `/api/Biennium` | admin | `-` | `List<BienniumDto>` | 200 / 404 |
| GET | `/api/Biennium/active` | admin | `-` | `List<BienniumDto>` | 200 / 404 |
| GET | `/api/Biennium/inactive` | admin | `-` | `List<BienniumDto>` | 200 / 404 |
| GET | `/api/Biennium/{id}` | admin | `Guid id` | `BienniumDto` | 200 / 404 |
| GET | `/api/Biennium/byname/{name}` | admin | `string name` | `SubjectDto` | 200 / 404 |
| GET | `/api/Biennium/byteacher/{id}` | admin | `Guid id` | `List<SubjectDto>` | 200 / 404 |
| PUT | `/api/Biennium/update/{id}` | admin | `Guid id, UpdateBienniumDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Biennium` | admin | `CreateBienniumDto dto` | `Guid JSON` | 201 |
| DELETE | `/api/Biennium/{id}` | admin | `Guid id` | `nessun body (204)` | 204 / 404 su risorsa assente |
| GET | `/api/Exam` | teacher,admin | `-` | `List<ExamSummaryDto>` | 200, anche zero/collection vuota |
| GET | `/api/Exam/{id}` | teacher,admin | `Guid id` | `ExamDetailDto` | 200 (404 su risorsa assente) |
| GET | `/api/Exam/assigned` | student | `-` | `List<AssignedExamDto>` | 200, anche zero/collection vuota |
| POST | `/api/Exam` | teacher,admin | `CreateExamDto dto` | `Guid JSON` | 201 |
| PUT | `/api/Exam/{id}` | teacher,admin | `Guid id, UpdateExamDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| PUT | `/api/Exam/{id}/students` | teacher,admin | `Guid id, UpdateExamStudentsDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Exam/{id}/publish` | teacher,admin | `Guid id` | `nessun body (204)` | 204 |
| POST | `/api/Exam/{id}/close` | teacher,admin | `Guid id` | `nessun body (204)` | 204 |
| POST | `/api/Exam/{id}/cancel` | teacher,admin | `Guid id` | `nessun body (204)` | 204 |
| DELETE | `/api/Exam/{id}` | teacher,admin | `Guid id` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Exam/{id}/start` | student | `Guid id` | `StartExamDto` | 200 (404 su risorsa assente) |
| POST | `/api/Exam/{id}/answers/save` | student | `Guid id, SaveExamAnswersDto dto` | `SaveAnswersResponseDto` | 200 (404 su risorsa assente) |
| POST | `/api/Exam/{id}/submit` | student | `Guid id, SubmitExamDto dto` | `SubmissionResultDto` | 200 (404 su risorsa assente) |
| GET | `/api/Exam/{examId}/submissions` | teacher,admin | `Guid examId, [FromQuery] SubmissionStatus? status` | `List<SubmissionSummaryDto>` | 200, anche zero/collection vuota |
| GET | `/api/Exam/submission/{id}/review` | teacher,admin | `Guid id` | `SubmissionReviewDto` | 200 (404 su risorsa assente) |
| PUT | `/api/Exam/submission/{id}/review` | teacher,admin | `Guid id, ReviewOpenAnswersDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Exam/submission/{id}/finalize` | teacher,admin | `Guid id, FinalizeSubmissionDto dto` | `nessun body (204)` | 204 |
| GET | `/api/Exam/submission/{id}/result` | teacher,admin,student | `Guid id` | `SubmissionResultDto` | 200 (404 su risorsa assente) |
| GET | `/api/Grade/count` | teacher,admin,student | `-` | `int` | 200, anche zero/collection vuota |
| GET | `/api/Grade/statistics` | teacher,admin,student | `-` | `GradeStatisticsDto` | 200 (404 su risorsa assente) |
| GET | `/api/Grade/filters` | teacher,admin,student | `-` | `GradeFiltersDto` | 200, anche zero/collection vuota |
| GET | `/api/Grade/paged` | teacher,admin,student | `[FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? subjectId = null, [FromQuery] Guid? studentId = null, [FromQuery] DateOnly? date = null` | `GradePageDto` | 200, anche zero/collection vuota |
| GET | `/api/Grade` | teacher,admin,student | `-` | `List<GradeDto>` | 200, anche zero/collection vuota |
| GET | `/api/Grade/{id}` | teacher,admin,student | `Guid id` | `GradeDto` | 200 (404 su risorsa assente) |
| GET | `/api/Grade/bystudentid/{id}` | admin | `Guid id` | `List<GradeDto>` | 200 (404 su risorsa assente) |
| GET | `/api/Grade/bysubject/{subject}` | teacher,admin,student | `string subject` | `List<GradeDto>` | 200 (404 su risorsa assente) |
| GET | `/api/Grade/bydate/{date}` | teacher,admin,student | `DateOnly date` | `List<GradeDto>` | 200 (404 su risorsa assente) |
| POST | `/api/Grade` | teacher,admin | `CreateGradeDto dto` | `Guid JSON` | 201 |
| PUT | `/api/Grade/update/{id}` | teacher,admin | `Guid id, UpdateGradeDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| DELETE | `/api/Grade/{id}` | admin | `Guid id` | `nessun body (204)` | 204 / 404 su risorsa assente |
| GET | `/api/Student/count` | teacher,admin | `-` | `int` | 200, anche zero/collection vuota |
| GET | `/api/Student` | teacher,admin | `-` | `List<StudentDto>` | 200 / 404 |
| GET | `/api/Student/{id}` | teacher,admin | `Guid id` | `StudentDto` | 200 / 404 |
| GET | `/api/Student/bylastname/{lastName}` | teacher,admin | `string lastName` | `List<StudentDto>` | 200 / 404 |
| PUT | `/api/Student/update/{id}` | admin | `Guid id, UpdateStudentDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Student` | admin | `CreateStudentDto dto` | `Guid JSON` | 201 |
| DELETE | `/api/Student/{id}` | admin | `Guid id` | `nessun body (204)` | 204 / 404 su risorsa assente |
| GET | `/api/Subject/count` | teacher,admin,student | `-` | `int` | 200, anche zero/collection vuota |
| GET | `/api/Subject` | teacher,admin,student | `-` | `List<SubjectDto>` | 200 / 404 |
| GET | `/api/Subject/{id}` | teacher,admin,student | `Guid id` | `SubjectDto` | 200 / 404 |
| GET | `/api/Subject/byname/{name}` | teacher,admin,student | `string name` | `SubjectDto` | 200 / 404 |
| GET | `/api/Subject/byteacher/{id}` | teacher,admin,student | `Guid id` | `List<SubjectDto>` | 200 / 404 |
| PUT | `/api/Subject/update/{id}` | admin | `Guid id, UpdateSubjectDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Subject` | admin | `CreateSubjectDto dto` | `Guid JSON` | 201 |
| DELETE | `/api/Subject/{id}` | admin | `Guid id` | `nessun body (204)` | 204 / 404 su risorsa assente |
| GET | `/api/Teacher/count` | teacher,admin | `-` | `int` | 200, anche zero/collection vuota |
| GET | `/api/Teacher` | teacher,admin,student | `-` | `List<TeacherDto>` | 200 / 404 |
| GET | `/api/Teacher/{id}` | teacher,admin,student | `Guid id` | `TeacherDto` | 200 / 404 |
| PUT | `/api/Teacher/update/{id}` | admin | `Guid id, UpdateTeacherDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Teacher` | admin | `CreateTeacherDto dto` | `Guid JSON` | 201 |
| DELETE | `/api/Teacher/{id}` | admin | `Guid id` | `nessun body (204)` | 204 / 404 su risorsa assente |
| GET | `/api/Users/count` | admin | `-` | `int` | 200, anche zero/collection vuota |
| GET | `/api/Users` | admin | `-` | `List<UserDto>` | 200 / 404 |
| GET | `/api/Users/{id}` | admin | `Guid id` | `UserDto` | 200 / 404 |
| PUT | `/api/Users/update/{id}` | admin | `Guid id, UpdateUserDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| PUT | `/api/Users/updatepassword/{id}` | student,teacher,admin | `Guid id, UpdatePasswordDto dto` | `nessun body (204)` | 204 / 404 su risorsa assente |
| POST | `/api/Users` | admin | `CreateUserDto dto` | `Guid JSON` | 201 |
| DELETE | `/api/Users/{id}` | admin | `Guid id` | `nessun body (204)` | 204 / 404 su risorsa assente |

Totale: 73 operazioni HTTP negli otto controller concreti. I manager StudyArea/StudyPath non hanno un controller pubblico.
