using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
    // Same work as Verify against a real credential, for identities that have none.
    bool VerifyAbsent(string password);
}
