using ElectronicRegisterAPI.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.DTOs;

public record ClaimsContext(Guid UserId, UserRole Role, Guid? StudentId, Guid? TeacherId);
