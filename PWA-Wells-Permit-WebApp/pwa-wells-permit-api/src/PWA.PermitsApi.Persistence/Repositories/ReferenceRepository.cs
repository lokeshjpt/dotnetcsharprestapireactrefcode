using Dapper;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Persistence.Repositories;

public sealed class ReferenceRepository : IReferenceRepository
{
    private readonly DapperContext _context;
    private readonly string _schema;

    public ReferenceRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions)
    {
        _context = context;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT LTRIM(RTRIM(state_code)) AS Code, state_name AS Label
FROM [{_schema}].[STATE_CODES] ORDER BY state_name;";
        return await QueryAsync(sql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetCitiesAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT LTRIM(RTRIM(city_code)) AS Code, city_name AS Label
FROM [{_schema}].[CITY_CODES] ORDER BY city_name;";
        return await QueryAsync(sql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetPaymentTypesAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT LTRIM(RTRIM(payment_type)) AS Code, payment_desc AS Label
FROM [{_schema}].[PAYMENT_TYPES]
WHERE payment_type NOT IN ('CASH', 'MC', 'VISA')
ORDER BY display_seq;";
        return await QueryAsync(sql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetWorkCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT LTRIM(RTRIM(work_category)) AS Code, work_cat_desc AS Label
FROM [{_schema}].[WORK_CATEGORIES]
WHERE active_flag = 'Y'
ORDER BY work_cat_desc;";
        return await QueryAsync(sql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkTypeDto>> GetWorkTypesAsync(string? category, CancellationToken cancellationToken = default)
    {
        // SiteExtraRate: for tiered "site" work types (site_max > 0) the flat fee covers up to
        // site_max wells/holes and each additional well is charged the single global extra rate held
        // in the special WORK_TYPES row (work_category='system', work_type='siteExtra'). Legacy parity:
        // BeanCodesWorkType.getWorkTypeSiteExtra + DisplayAppServlet setWorkSiteExtraRate. Non-site or
        // untiered types return 0 so the frontends never add an extra charge for them.
        var sql = $@"SELECT LTRIM(RTRIM(work_type)) AS Code,
       work_desc AS Label,
       CAST(ISNULL(fee_rate_amt, 0) AS decimal(19,2)) AS FeeRate,
       LTRIM(RTRIM(ISNULL(fee_unit, ''))) AS FeeUnit,
       ISNULL(site_max, 0) AS SiteMax,
       CASE WHEN LTRIM(RTRIM(ISNULL(fee_unit, ''))) = 'site' AND ISNULL(site_max, 0) > 0
            THEN (SELECT CAST(ISNULL(fee_rate_amt, 0) AS decimal(19,2))
                  FROM [{_schema}].[WORK_TYPES]
                  WHERE work_category = 'system' AND work_type = 'siteExtra')
            ELSE 0 END AS SiteExtraRate
FROM [{_schema}].[WORK_TYPES]
WHERE active_flag = 'Y'
  AND (@Category IS NULL OR work_category = @Category)
ORDER BY work_desc;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<WorkTypeDto>(new CommandDefinition(sql, new { Category = category }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetWellUseTypesAsync(string? category, string? workType, CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT LTRIM(RTRIM(well_use_type)) AS Code, well_use_desc AS Label
FROM [{_schema}].[WORK_USE_TYPES]
WHERE active_flag = 'Y'
  AND (@Category IS NULL OR work_category = @Category)
  AND (@WorkType IS NULL OR work_type = @WorkType)
ORDER BY well_use_desc;";
        return await QueryAsync(sql, new { Category = category, WorkType = workType }, cancellationToken);
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetDrillMethodsAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT LTRIM(RTRIM(drill_method_type)) AS Code, drill_method_name AS Label
FROM [{_schema}].[DRILL_METHOD_TYPES]
WHERE active_flag = 'Y'
ORDER BY drill_method_name;";
        return await QueryAsync(sql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetInspectorsAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT CONVERT(varchar(20), INSPECTOR_ID) AS Code, LTRIM(RTRIM(INSPECTOR_NAME)) AS Label
FROM [{_schema}].[INSPECTORS]
WHERE ACTIVE_FLAG = 'Y'
ORDER BY INSPECTOR_NAME;";
        return await QueryAsync(sql, null, cancellationToken);
    }

    private async Task<IReadOnlyList<ReferenceItemDto>> QueryAsync(string sql, object? parameters, CancellationToken cancellationToken)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ReferenceItemDto>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
