using Microsoft.Extensions.Logging;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Application.Services;

public sealed class ConditionsService : IConditionsService
{
    private readonly IConditionsRepository _repository;
    private readonly ILogger<ConditionsService> _logger;

    public ConditionsService(IConditionsRepository repository, ILogger<ConditionsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ApplicationConditionsDto> GetConditionsAsync(string appId, CancellationToken cancellationToken = default)
    {
        var works = await _repository.GetWorksAsync(appId, cancellationToken);
        var applied = await _repository.GetAppliedConditionsAsync(appId, cancellationToken);
        var appliedByWork = applied
            .GroupBy(a => a.WorkId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<WorkConditionsDto>(works.Count);
        foreach (var work in works)
        {
            // The work-type-scoped condition master list (rendered as checkboxes). The reviewer checks
            // the ones to apply — nothing is pre-checked, matching the legacy proc_work_conditions.jsp
            // where seeded work-type defaults are shown with UNCHECKED boxes until saved.
            var available = await _repository.GetWorkConditionTypesAsync(work.WorkCategory, work.WorkType, cancellationToken);

            var selected = appliedByWork.TryGetValue(work.WorkId, out var rows)
                ? rows.Select(r => new ConditionSelectionDto(r.ConditionType, r.OtherDesc)).ToList()
                : new List<ConditionSelectionDto>();

            result.Add(new WorkConditionsDto(work.WorkId, work.WorkLabel, work.StatusCode, available, selected));
        }

        return new ApplicationConditionsDto(result);
    }

    public async Task<ApplicationConditionsDto> UpdateWorkConditionsAsync(string appId, int workId, UpdateWorkConditionsRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var conditions = (request.Conditions ?? new List<ConditionSelectionDto>())
            .Where(c => !string.IsNullOrWhiteSpace(c.ConditionType))
            .Select(c => new ConditionSelectionDto(
                c.ConditionType.Trim(),
                string.IsNullOrWhiteSpace(c.OtherDesc) ? null : c.OtherDesc.Trim()))
            .ToList();

        await _repository.ReplaceWorkConditionsAsync(appId, workId, conditions, request.NoSpecials, updatedBy, cancellationToken);
        _logger.LogInformation("Applied {Count} conditions to work {WorkId} of application {AppId} (noSpecials={NoSpecials})",
            conditions.Count, workId, appId, request.NoSpecials);
        return await GetConditionsAsync(appId, cancellationToken);
    }
}
