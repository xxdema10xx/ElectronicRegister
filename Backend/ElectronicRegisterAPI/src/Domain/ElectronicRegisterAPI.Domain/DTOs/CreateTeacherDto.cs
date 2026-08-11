using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.DTOs
{
    public class CreateTeacherDto
    {
        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;
    }
}
