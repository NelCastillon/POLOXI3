namespace Ams.Domain.Enums;

public enum RetentionStartTrigger
{
    Creation = 1,
    PolicyExpiry = 2,
    ClaimClosure = 3,
    LastModified = 4,
    DocumentUpload = 5
}
