using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Common.Validation;
using Ams.Knowledge.Contracts.Mappings;

namespace Ams.Knowledge.Application.Services;

public sealed class ExternalMappingService : IExternalMappingService
{
    private readonly IExternalMappingRepository _repository;

    public ExternalMappingService(IExternalMappingRepository repository) => _repository = repository;

    public Task<ExternalMappingResult?> ResolveMappingAsync(ExternalMappingRequest request, CancellationToken cancellationToken = default)
    {
        RequestValidator.Validate(request);
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceSystemTypeCode) || (string.IsNullOrWhiteSpace(request.ExternalCode) && string.IsNullOrWhiteSpace(request.ExternalValue)))
            throw new ApplicationValidationException(["Tenant, source system, and an external code or value are required."]);
        return _repository.ResolveApprovedAsync(request, cancellationToken);
    }
}
