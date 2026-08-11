using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface ISubjectService
    {
        Task EnsureSubjectExistsAsync(Guid subjectId);
        Task EnsureNameIsAvailableAsync(string subjectName);
        Task EnsureSubjectCanBeDeletedAsync(Guid subjectId);
    }
}
