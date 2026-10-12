using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IExamService
    {
        void EnsureValidSchedule(DateTime from, DateTime until, int durationMinutes);
        void EnsureValidPassingPercent(decimal passingPercent);
        void EnsureValidStructure(Exam exam);
        void EnsureEditable(Exam exam);
        void EnsurePublishable(Exam exam);
        void EnsureCanCancel(Exam exam, bool hasSubmissions);
        void EnsureCanDelete(Exam exam);
        decimal CalculateTotalPoints(IEnumerable<Question> questions);
    }
}
