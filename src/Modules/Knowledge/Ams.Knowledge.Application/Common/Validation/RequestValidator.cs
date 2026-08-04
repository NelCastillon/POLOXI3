using System.ComponentModel.DataAnnotations;

namespace Ams.Knowledge.Application.Common.Validation;

public static class RequestValidator
{
    public static void Validate(object request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        ValidateConstructorParameters(request, results);
        ValidateGlobalRules(request, results);
        if (results.Count == 0)
            return;

        throw new ApplicationValidationException(results
            .Select(result => result.ErrorMessage ?? "The request is invalid.")
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    private static void ValidateConstructorParameters(object request, ICollection<ValidationResult> results)
    {
        var type = request.GetType();
        var constructor = type.GetConstructors()
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();
        if (constructor is null)
            return;

        var properties = type.GetProperties().ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in constructor.GetParameters())
        {
            if (!properties.TryGetValue(parameter.Name ?? string.Empty, out var property))
                continue;

            var attributes = parameter.GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                .Cast<ValidationAttribute>()
                .ToArray();
            if (attributes.Length == 0)
                continue;

            var context = new ValidationContext(request) { MemberName = property.Name };
            Validator.TryValidateValue(property.GetValue(request), context, results, attributes);
        }
    }

    private static void ValidateGlobalRules(object request, ICollection<ValidationResult> results)
    {
        var properties = request.GetType().GetProperties();
        foreach (var property in properties.Where(property => property.PropertyType == typeof(Guid)))
        {
            if ((Guid)(property.GetValue(request) ?? Guid.Empty) == Guid.Empty)
                results.Add(new ValidationResult($"{property.Name} is required.", [property.Name]));
        }

        var effectiveFrom = properties.SingleOrDefault(property => property.Name == "EffectiveFromUtc")?.GetValue(request) as DateTime?;
        var effectiveTo = properties.SingleOrDefault(property => property.Name == "EffectiveToUtc")?.GetValue(request) as DateTime?;
        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo <= effectiveFrom)
            results.Add(new ValidationResult("Effective-to date must be later than effective-from date.", ["EffectiveToUtc"]));

        var minimum = properties.SingleOrDefault(property => property.Name == "MinimumCount")?.GetValue(request) as int?;
        var maximum = properties.SingleOrDefault(property => property.Name == "MaximumCount")?.GetValue(request) as int?;
        if (minimum.HasValue && maximum.HasValue && minimum > maximum)
            results.Add(new ValidationResult("Minimum count cannot exceed maximum count.", ["MaximumCount"]));

        var subject = properties.SingleOrDefault(property => property.Name == "SubjectConceptId")?.GetValue(request) as Guid?;
        var @object = properties.SingleOrDefault(property => property.Name == "ObjectConceptId")?.GetValue(request) as Guid?;
        if (subject.HasValue && @object.HasValue && subject == @object)
            results.Add(new ValidationResult("A concept relationship cannot reference itself.", ["ObjectConceptId"]));
    }
}
