using System.ComponentModel.DataAnnotations;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.ContactIntake;

namespace Ams.Application;

public sealed class ContactIntakeService : IContactIntakeService
{
    private readonly IContactIntakeRepository _repository;

    public ContactIntakeService(IContactIntakeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ContactDemoSubmissionResult> SubmitDemoRequestAsync(CreateContactDemoRequest request, ContactDemoRequestContext context, CancellationToken cancellationToken = default)
    {
        Normalize(request);
        var allowedOptions = await GetAllowedOptionsAsync(cancellationToken);
        Validate(request, allowedOptions);
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

    private async Task<IReadOnlyDictionary<string, HashSet<string>>> GetAllowedOptionsAsync(CancellationToken cancellationToken)
    {
        var options = await _repository.GetOptionsAsync(cancellationToken);
        return options
            .GroupBy(option => option.OptionType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(option => option.Code).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void Validate(CreateContactDemoRequest request, IReadOnlyDictionary<string, HashSet<string>> allowedOptions)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request);
        if (!Validator.TryValidateObject(request, context, results, validateAllProperties: true))
            throw new ValidationException(string.Join(" ", results.Select(x => x.ErrorMessage)));

        if (!request.ConsentToContact)
            throw new ValidationException("Consent to contact is required.");

        if (!IsAllowed(allowedOptions, "AgencySize", request.AgencySize))
            throw new ValidationException("Agency size is invalid.");

        if (!IsAllowed(allowedOptions, "Branches", request.Branches))
            throw new ValidationException("Branch count is invalid.");

        if (!IsAllowed(allowedOptions, "BusinessLines", request.BusinessLines))
            throw new ValidationException("Business line is invalid.");

        if (!IsAllowed(allowedOptions, "Timeline", request.Timeline))
            throw new ValidationException("Timeline is invalid.");

        if (!IsAllowed(allowedOptions, "Budget", request.Budget))
            throw new ValidationException("Budget is invalid.");

        if (request.Priorities.Any(priority => !IsAllowed(allowedOptions, "Priority", priority)))
            throw new ValidationException("One or more selected priorities are invalid.");
    }

    private static bool IsAllowed(IReadOnlyDictionary<string, HashSet<string>> allowedOptions, string optionType, string value)
        => allowedOptions.TryGetValue(optionType, out var values) && values.Contains(value);

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
