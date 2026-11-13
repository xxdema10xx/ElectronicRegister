namespace ElectronicRegisterAPI.Domain.Exceptions;

// E2: the names of a request linking an existing profile must match that profile (422).
public sealed class AccountProfileMismatchException : Exception
{
    public AccountProfileMismatchException() : base("Nome e cognome non coincidono con la scheda collegata.") { }
}
