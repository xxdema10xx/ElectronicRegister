using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface ISubjectService
{
    void EnsureClassSubjectExists(ClassSubject? classSubject);
    Task<ClassSubject> EnsureValidClassSubjectId(Guid classSubjectId);
    Task EnsureSubjectExistsAsync(Guid subjectId);
    Task EnsureNameIsAvailableAsync(string subjectName);
    Task EnsureSubjectCanBeDeletedAsync(Guid subjectId);
}
