namespace Ams.Application.Features.Sod;

public sealed class AssignSodConflictReviewerRequest
{
    public Guid  ReviewerUserId  { get; set; }
    public Guid? AssignedByUserId { get; set; }
}
