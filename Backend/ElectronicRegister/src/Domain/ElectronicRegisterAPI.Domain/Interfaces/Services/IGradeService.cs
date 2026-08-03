using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IGradeService
    {
        void EnsureValidGradeValue(decimal value);
        Task EnsureTeacherTeachesSubjectAsync(Guid teacherId, Guid subjectId);
        Task EnsureGradeEsists(Guid id);
    }
}
