using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Exceptions;

/// <summary>One or more batch rows are invalid; nothing has been saved.</summary>
public sealed class GradeBatchValidationException(IReadOnlyList<GradeBatchRowErrorDto> rows) : Exception("Uno o più voti del blocco non sono validi.")
{
    public IReadOnlyList<GradeBatchRowErrorDto> Rows { get; } = rows;
}
