namespace ElectronicRegisterAPI.Domain.DTOs;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public class UpdateStudentDto
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}
