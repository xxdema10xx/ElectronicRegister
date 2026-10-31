using System;
using System.Collections.Generic;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface ISubjectService
{
    Task<ClassSubject> EnsureValidClassSubjectId(Guid classSubjectId);
}
