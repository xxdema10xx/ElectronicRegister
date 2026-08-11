using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface ITeacherService
    {
        Task EnsureTeacherCanBeDeletedAsync(Guid teacherId);
    }
}
