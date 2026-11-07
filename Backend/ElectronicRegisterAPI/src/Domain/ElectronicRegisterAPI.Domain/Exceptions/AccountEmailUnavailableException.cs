namespace ElectronicRegisterAPI.Domain.Exceptions;

// One answer for every reason an address cannot become a login: already an account
// here or in another institution, or reserved in the global email namespace.
public sealed class AccountEmailUnavailableException : Exception
{
    public AccountEmailUnavailableException() : base("Indirizzo email non disponibile.") { }
}
