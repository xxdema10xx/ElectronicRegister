using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IStudentService
    {
        Task EnsureStudentCanBeDeletedAsync(Guid studentId);
        Task EnsureStudentExistsAsync(Guid studentId);
    }
}
