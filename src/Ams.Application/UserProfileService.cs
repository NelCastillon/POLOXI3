using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _repository;
    public UserProfileService(IUserProfileRepository repository) => _repository = repository;

    public Task<UserProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _repository.GetByUserIdAsync(userId, cancellationToken);

    public Task UpsertAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertAsync(request, cancellationToken);
}
