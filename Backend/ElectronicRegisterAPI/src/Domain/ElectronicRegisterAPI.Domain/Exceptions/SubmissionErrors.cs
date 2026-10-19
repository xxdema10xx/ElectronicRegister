namespace ElectronicRegisterAPI.Domain.Exceptions;

/// <summary>Classifies expected submission errors while preserving the existing InvalidOperationException contract.</summary>
public static class SubmissionErrors
{
    private const string Marker = "ElectronicRegister.SubmissionError";

    public static InvalidOperationException Conflict(string message) => Create(message, "conflict");

    public static InvalidOperationException InvalidRequest(string message) => Create(message, "invalid_request");

    private static InvalidOperationException Create(string message, string kind)
    {
        var exception = new InvalidOperationException(message);
        exception.Data[Marker] = kind;
        return exception;
    }

    public static bool IsConflict(Exception exception) =>
        exception is InvalidOperationException && exception.Data[Marker] as string == "conflict";

    public static bool IsInvalidRequest(Exception exception) =>
        exception is InvalidOperationException && exception.Data[Marker] as string == "invalid_request";
}
