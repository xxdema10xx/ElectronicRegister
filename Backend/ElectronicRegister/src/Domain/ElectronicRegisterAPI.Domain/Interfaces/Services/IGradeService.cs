using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IGradeService
    {
        void EnsureValidGradeValue(decimal value);
        Task EnsureTeacherTeachesSubjectAsync(Guid teacherId, Guid subjectId);
        Task EnsureGradeExists(Guid id);
        Task EnsureTeacherOwnsGradeAsync(Guid teacherId, Grade grade);
    }
}
