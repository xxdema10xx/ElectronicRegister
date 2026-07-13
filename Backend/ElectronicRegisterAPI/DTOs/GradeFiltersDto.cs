namespace ElectronicRegisterAPI.DTOs
{
    public class GradeFiltersDto
    {
        public List<SubjectDto> Subjects { get; set; } = new();
        public List<StudentDto> Students { get; set; } = new();
    }
}
