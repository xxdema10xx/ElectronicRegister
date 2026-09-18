// Domain — modello di dominio (Domain/Models/Grade.cs)
namespace ElectronicRegisterAPI.Domain.Models;

public class Grade
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ClassSubjectId { get; set; }
    public decimal Value { get; set; }
    public DateOnly Date { get; set; }
}