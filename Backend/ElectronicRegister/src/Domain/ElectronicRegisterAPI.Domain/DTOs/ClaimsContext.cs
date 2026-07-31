using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.DTOs;

public record ClaimsContext(Enums.UserRole Role, Guid? StudentId, Guid? TeacherId);
