namespace Ams.Knowledge.Application.Common.Validation;

public sealed class ApplicationValidationException : InvalidOperationException
{
    public ApplicationValidationException(IReadOnlyCollection<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyCollection<string> Errors { get; }
}
