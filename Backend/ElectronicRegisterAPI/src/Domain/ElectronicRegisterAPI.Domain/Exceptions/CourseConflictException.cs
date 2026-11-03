namespace ElectronicRegisterAPI.Domain.Exceptions;

public sealed class CourseConflictException(Exception? innerException = null) : Exception("Conflitto sulla struttura formativa.", innerException);
