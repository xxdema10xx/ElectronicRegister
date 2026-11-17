namespace ElectronicRegisterAPI.Domain.Configuration;

// Multitenancy:ExamApi:ViolationAutoSubmitThreshold. 0 (default) keeps violations as
// signals only; 1-128 submits the attempt with the persisted answers once a newly
// recorded event brings ViolationCount to the threshold. Any other value fails startup.
public sealed class ExamViolationOptions
{
    public const string Section = "Multitenancy:ExamApi";
    public const int MaximumThreshold = 128;
    public int ViolationAutoSubmitThreshold { get; set; }

    public static bool Valid(ExamViolationOptions o) => o.ViolationAutoSubmitThreshold is >= 0 and <= MaximumThreshold;
    public bool ReachesThreshold(int violationCount) => ViolationAutoSubmitThreshold > 0 && violationCount >= ViolationAutoSubmitThreshold;
}
