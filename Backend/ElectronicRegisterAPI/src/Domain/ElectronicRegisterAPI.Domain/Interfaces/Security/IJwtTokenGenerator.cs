using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IJwtTokenGenerator
{
    string Generate(Guid userId, string email, Enums.UserRole role, Guid? studentId, Guid? teacherId);
}