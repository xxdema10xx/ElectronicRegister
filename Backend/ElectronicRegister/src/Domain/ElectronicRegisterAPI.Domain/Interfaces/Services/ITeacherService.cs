using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface ITeacherService
    {
        Task EnsureTeacherExistsAsync(Guid teacherId);
        Task EnsureTeacherCanBeDeletedAsync(Guid teacherId);
    }
}
