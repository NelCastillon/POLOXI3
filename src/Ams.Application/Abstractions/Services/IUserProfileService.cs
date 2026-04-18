using Ams.Application.Common.Dtos;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface IUserProfileService
{
    Task<UserProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default);
}
