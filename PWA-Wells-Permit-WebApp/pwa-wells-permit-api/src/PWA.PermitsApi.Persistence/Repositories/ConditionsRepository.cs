using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Persistence.Repositories;

public sealed class ConditionsRepository : IConditionsRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<ConditionsRepository> _logger;
    private readonly string _schema;

    public ConditionsRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions, ILogger<ConditionsRepository> logger)
    {
        _context = context;
        _logger = logger;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<IReadOnlyList<WorkForConditionsDto>> GetWorksAsync(string appId, CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT wrk.work_id AS WorkId,
       LTRIM(RTRIM(wrk.work_category)) AS WorkCategory,
       LTRIM(RTRIM(wrk.work_type)) AS WorkType,
       LTRIM(RTRIM(wrk.status_code)) AS StatusCode,
       wca.work_cat_desc AS WorkCategoryDesc,
       wtp.work_desc AS WorkTypeDesc,
       wus.well_use_desc AS WellUseDesc
FROM [{_schema}].[APP_WORKS] wrk
LEFT JOIN [{_schema}].[WORK_CATEGORIES] wca ON wrk.work_category = wca.work_category
LEFT JOIN [{_schema}].[WORK_TYPES] wtp ON wrk.work_category = wtp.work_category AND wrk.work_type = wtp.work_type
LEFT JOIN [{_schema}].[WORK_USE_TYPES] wus ON wrk.work_category = wus.work_category AND wrk.work_type = wus.work_type AND wrk.well_use_type = wus.well_use_type
WHERE wrk.application_id = @AppId
ORDER BY wrk.work_id;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WorkRow>(new CommandDefinition(sql, new { AppId = appId }, cancellationToken: cancellationToken));
        return rows.Select(r => new WorkForConditionsDto(
            r.WorkId,
            r.WorkCategory ?? string.Empty,
            r.WorkType ?? string.Empty,
            BuildLabel(r),
            r.StatusCode ?? string.Empty)).ToList();
    }

    public async Task<IReadOnlyList<WorkConditionRowDto>> GetAppliedConditionsAsync(string appId, CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT work_id AS WorkId, LTRIM(RTRIM(condition_type)) AS ConditionType, condition_other_desc AS OtherDesc
FROM [{_schema}].[APP_WORK_CONDITIONS]
WHERE application_id = @AppId
ORDER BY work_id, cond_id;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WorkConditionRowDto>(new CommandDefinition(sql, new { AppId = appId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetWorkConditionTypesAsync(string workCategory, string workType, CancellationToken cancellationToken = default)
    {
        // Work-type-scoped condition master list — the legacy retrieveWorkCondByWtype set.
        var sql = $@"SELECT LTRIM(RTRIM(wct.condition_type)) AS Code, cot.condition_desc AS Label
FROM [{_schema}].[WORK_CONDITION_TYPES] wct
LEFT JOIN [{_schema}].[CONDITION_TYPES] cot ON wct.condition_type = cot.condition_type
WHERE wct.work_category = @WorkCategory
  AND wct.work_type = @WorkType
ORDER BY cot.condition_desc;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ReferenceItemDto>(new CommandDefinition(sql, new { WorkCategory = workCategory, WorkType = workType }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task ReplaceWorkConditionsAsync(string appId, int workId, IReadOnlyList<ConditionSelectionDto> conditions, bool noSpecials, string updatedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var currentStatus = (await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT LTRIM(RTRIM(status_code)) FROM [{_schema}].[APP_WORKS] WHERE application_id = @AppId AND work_id = @WorkId;",
            new { AppId = appId, WorkId = workId }, transaction, cancellationToken: cancellationToken)))?.Trim();

        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM [{_schema}].[APP_WORK_CONDITIONS] WHERE application_id = @AppId AND work_id = @WorkId;",
            new { AppId = appId, WorkId = workId }, transaction, cancellationToken: cancellationToken));

        var insertSql = $@"INSERT INTO [{_schema}].[APP_WORK_CONDITIONS]
    (application_id, work_id, cond_id, condition_type, condition_other_desc)
VALUES (@AppId, @WorkId, @CondId, @ConditionType, @OtherDesc);";

        var condId = 0;
        foreach (var condition in conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.ConditionType))
            {
                continue;
            }

            condId++;
            await connection.ExecuteAsync(new CommandDefinition(insertSql, new
            {
                AppId = appId,
                WorkId = workId,
                CondId = condId,
                ConditionType = condition.ConditionType.Trim(),
                OtherDesc = string.IsNullOrWhiteSpace(condition.OtherDesc) ? null : condition.OtherDesc.Trim()
            }, transaction, cancellationToken: cancellationToken));
        }

        // Per-work status transition mirroring the legacy intra: a work still "Pending Conditions"
        // (PENDC) advances to "Pending Approval" (PEND) once it carries at least one condition
        // (BeanAppWrk.adjustStatus) OR the user chose "No Specials" (processNoWorkConditions). This
        // never advances a work that already has conditions cleared with no "No Specials", and never
        // regresses a work that is already past PENDC.
        var advanced = 0;
        if (string.Equals(currentStatus, "PENDC", StringComparison.OrdinalIgnoreCase) && (condId > 0 || noSpecials))
        {
            advanced = await connection.ExecuteAsync(new CommandDefinition($@"
UPDATE [{_schema}].[APP_WORKS]
SET status_code = 'PEND',
    update_by = @UpdatedBy,
    update_ts = @UpdatedTs
WHERE application_id = @AppId
  AND work_id = @WorkId
  AND status_code = 'PENDC';",
                new { AppId = appId, WorkId = workId, UpdatedBy = updatedBy, UpdatedTs = DateTime.UtcNow }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Replaced conditions for application {AppId} work {WorkId} ({Count} conditions, noSpecials={NoSpecials}, advanced {Advanced} PENDC->PEND) by {UpdatedBy}",
            appId, workId, condId, noSpecials, advanced, updatedBy);
    }

    private static string BuildLabel(WorkRow r)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(r.WorkCategoryDesc) ? r.WorkCategory : r.WorkCategoryDesc,
            string.IsNullOrWhiteSpace(r.WorkTypeDesc) ? r.WorkType : r.WorkTypeDesc,
            r.WellUseDesc,
        };
        return string.Join(" - ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
    }

    private sealed class WorkRow
    {
        public int WorkId { get; init; }
        public string? WorkCategory { get; init; }
        public string? WorkType { get; init; }
        public string? StatusCode { get; init; }
        public string? WorkCategoryDesc { get; init; }
        public string? WorkTypeDesc { get; init; }
        public string? WellUseDesc { get; init; }
    }
}
