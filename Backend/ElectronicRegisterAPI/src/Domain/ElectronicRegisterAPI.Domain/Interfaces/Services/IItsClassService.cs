using ElectronicRegisterAPI.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services
{
    public interface IItsClassService
    {
        Task EnsureBienniumStudyPathExistsAsync (Guid bienniumStudyPathId);
        Task EnsureValidItsClassNameAsync(string name);
        Task<ItsClass> EnsureItsClassCanBeDeletedAsync(Guid itsClassId);
    }
}
