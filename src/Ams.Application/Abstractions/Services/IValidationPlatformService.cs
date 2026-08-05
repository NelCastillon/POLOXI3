using Ams.Application.Features.Platform;

namespace Ams.Application.Abstractions.Services;

public interface IValidationPlatformService
{
    Task<ValidationExecutionResponse> ValidateAsync(ExecuteValidationsRequest request, CancellationToken cancellationToken = default);
}
