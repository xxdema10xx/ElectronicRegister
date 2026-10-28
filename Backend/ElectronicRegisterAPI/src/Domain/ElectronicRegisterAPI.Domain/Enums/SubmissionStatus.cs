using System;
using System.Collections.Generic;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Enums;

public enum SubmissionStatus
{
    InProgress,
    Graded,
    PendingReview,
    FailedAuto,
    Finalized
}
