namespace ElectronicRegisterAPI.DTOs
{
    public class GradePageDto
    {
        public List<GradeDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
