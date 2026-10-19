using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class GradeMapper
{
    public static List<GradeDto> ToDtos(List<Grade> grades,
        IReadOnlyDictionary<Guid, ClassSubject> classSubjects,
        IReadOnlyDictionary<Guid, Subject> subjects,
        IReadOnlyDictionary<Guid, Student> students)
    {
        var result = new List<GradeDto>();

        foreach (var grade in grades)
        {
            if (!classSubjects.TryGetValue(grade.ClassSubjectId, out var classSubject))
            {
                throw new InvalidOperationException($"ClassSubject {grade.ClassSubjectId} non trovato per il voto {grade.Id}.");
            }

            subjects.TryGetValue(classSubject.SubjectId, out var subject);
            students.TryGetValue(grade.StudentId, out var student);

            result.Add(new GradeDto
            {
                Id = grade.Id,
                StudentId = grade.StudentId,
                SubjectId = classSubject.SubjectId,
                SubjectName = subject?.Name,
                TeacherId = classSubject.TeacherId,
                Value = grade.Value,
                Date = grade.Date,
                Student = student is null ? null : new StudentDto
                {
                    Id = student.Id,
                    FirstName = student.FirstName,
                    LastName = student.LastName
                }
            });
        }

        return result;
    }
}
