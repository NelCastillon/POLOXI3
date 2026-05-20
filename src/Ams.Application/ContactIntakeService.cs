using System.ComponentModel.DataAnnotations;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.ContactIntake;

namespace Ams.Application;

public sealed class ContactIntakeService : IContactIntakeService
{
    private static readonly HashSet<string> AllowedAgencySizes = new(StringComparer.OrdinalIgnoreCase)
    {
        "1-10 users", "11-50 users", "51-200 users", "200+ users"
    };

    private static readonly HashSet<string> AllowedBranches = new(StringComparer.OrdinalIgnoreCase)
    {
        "Single location", "2-5 branches", "6-20 branches", "20+ branches"
    };

    private static readonly HashSet<string> AllowedBusinessLines = new(StringComparer.OrdinalIgnoreCase)
    {
        "Commercial lines", "Personal lines", "Benefits", "Mixed book", "MGA / wholesale"
    };

    private static readonly HashSet<string> AllowedTimelines = new(StringComparer.OrdinalIgnoreCase)
    {
        "Exploring options", "0-3 months", "3-6 months", "6-12 months", "12+ months"
    };

    private static readonly HashSet<string> AllowedBudgets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Not sure yet", "Under $1,000", "$1,000 - $5,000", "$5,000 - $15,000", "$15,000+"
    };

    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "CRM", "Submissions", "Policies", "Renewals", "Claims", "Integrations", "AI", "Security"
    };

    private readonly IContactIntakeRepository _repository;

    public ContactIntakeService(IContactIntakeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ContactDemoSubmissionResult> SubmitDemoRequestAsync(CreateContactDemoRequest request, ContactDemoRequestContext context, CancellationToken cancellationToken = default)
    {
        Normalize(request);
        Validate(request);
        return await _repository.CreateDemoRequestAsync(request, context, cancellationToken);
    }

    public Task<IReadOnlyList<ContactIntakeOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => _repository.GetOptionsAsync(cancellationToken);

    private static void Normalize(CreateContactDemoRequest request)
    {
        request.FirstName = request.FirstName.Trim();
        request.LastName = request.LastName.Trim();
        request.WorkEmail = request.WorkEmail.Trim().ToLowerInvariant();
        request.Phone = NullIfWhiteSpace(request.Phone);
        request.Title = NullIfWhiteSpace(request.Title);
        request.AgencyName = request.AgencyName.Trim();
        request.AgencySize = request.AgencySize.Trim();
        request.Branches = request.Branches.Trim();
        request.BusinessLines = request.BusinessLines.Trim();
        request.CurrentSystem = NullIfWhiteSpace(request.CurrentSystem);
        request.Timeline = request.Timeline.Trim();
        request.Budget = request.Budget.Trim();
        request.Message = NullIfWhiteSpace(request.Message);
        request.Priorities = request.Priorities
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static void Validate(CreateContactDemoRequest request)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request);
        if (!Validator.TryValidateObject(request, context, results, validateAllProperties: true))
            throw new ValidationException(string.Join(" ", results.Select(x => x.ErrorMessage)));

        if (!request.ConsentToContact)
            throw new ValidationException("Consent to contact is required.");

        if (!AllowedAgencySizes.Contains(request.AgencySize))
            throw new ValidationException("Agency size is invalid.");

        if (!AllowedBranches.Contains(request.Branches))
            throw new ValidationException("Branch count is invalid.");

        if (!AllowedBusinessLines.Contains(request.BusinessLines))
            throw new ValidationException("Business line is invalid.");

        if (!AllowedTimelines.Contains(request.Timeline))
            throw new ValidationException("Timeline is invalid.");

        if (!AllowedBudgets.Contains(request.Budget))
            throw new ValidationException("Budget is invalid.");

        if (request.Priorities.Any(priority => !AllowedPriorities.Contains(priority)))
            throw new ValidationException("One or more selected priorities are invalid.");
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
