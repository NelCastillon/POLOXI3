using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.DocumentIntake;

public static class DocumentIntakeValidator
{
    public static void Validate(object request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        ValidateRules(request, results);
        if (results.Count > 0)
            throw new ValidationException(string.Join(" ", results.Select(result => result.ErrorMessage).Where(message => !string.IsNullOrWhiteSpace(message)).Distinct(StringComparer.Ordinal)));
    }

    private static void ValidateRules(object request, ICollection<ValidationResult> results)
    {
        var properties = request.GetType().GetProperties();
        foreach (var property in properties.Where(property => property.PropertyType == typeof(Guid)))
        {
            if ((Guid)(property.GetValue(request) ?? Guid.Empty) == Guid.Empty)
                results.Add(new ValidationResult($"{property.Name} is required.", [property.Name]));
        }

        if (request is CreateDocumentIntakeSessionCommand create && !DocumentIntakeModules.All.Contains(create.ModuleCode))
            results.Add(new ValidationResult($"ModuleCode '{create.ModuleCode}' is not supported.", [nameof(create.ModuleCode)]));

        if (request is ReviewDocumentIntakeFieldCommand review &&
            review.DecisionCode is not (DocumentIntakeReviewStatuses.Approved or DocumentIntakeReviewStatuses.Corrected or DocumentIntakeReviewStatuses.Rejected))
            results.Add(new ValidationResult("DecisionCode must be APPROVED, CORRECTED, or REJECTED.", [nameof(review.DecisionCode)]));

        if (request is ReviewDocumentIntakeFieldCommand corrected &&
            corrected.DecisionCode == DocumentIntakeReviewStatuses.Corrected && string.IsNullOrWhiteSpace(corrected.ReviewedValue))
            results.Add(new ValidationResult("ReviewedValue is required for a corrected field.", [nameof(corrected.ReviewedValue)]));

        if (request is AttachDocumentToIntakeCommand attach && attach.ContentHashSha256 is not null &&
            !attach.ContentHashSha256.All(Uri.IsHexDigit))
            results.Add(new ValidationResult("ContentHashSha256 must contain 64 hexadecimal characters.", [nameof(attach.ContentHashSha256)]));
    }
}
