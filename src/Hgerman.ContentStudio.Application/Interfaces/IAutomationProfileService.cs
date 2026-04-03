using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Shared.DTOs;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IAutomationProfileService
{
    Task<List<AutomationProfileListItemDto>> GetListAsync(CancellationToken cancellationToken = default);
    Task<AutomationProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(UpsertAutomationProfileRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, UpsertAutomationProfileRequest request, CancellationToken cancellationToken = default);
    Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default);
    Task<AutomationProfileDashboardDto?> GetDashboardAsync(int id, CancellationToken cancellationToken = default);
}