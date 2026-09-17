using System.Text;
using Dapper;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Persistence.Repositories;

public sealed class InspectionRepository : IInspectionRepository
{
    private readonly DapperContext _context;
    private readonly string _schema;

    public InspectionRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions)
    {
        _context = context;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<IReadOnlyList<Inspection>> GetByApplicationAsync(string appId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT a.INSPECTION_DATE AS InspectionDate, a.SLOT_ID AS SlotId, a.APPLICATION_ID AS AppId,
       a.INSPECTOR_ID AS InspectorId, a.INSPECTION_DATE_TIME AS InspectionDateTime, a.STATUS_CODE AS StatusCode,
       (SELECT TOP 1 n.NOTES_TEXT FROM [{_schema}].[INSPECTION_NOTES] n
        WHERE n.APPLICATION_ID = a.APPLICATION_ID ORDER BY n.SEQ_NUM DESC) AS Notes
FROM [{_schema}].[INSPECTION_ASSIGNMENTS] a
WHERE a.APPLICATION_ID = @AppId
ORDER BY a.INSPECTION_DATE DESC, a.SLOT_ID;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Inspection>(new CommandDefinition(sql, new { AppId = appId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<Inspection> AddAsync(Inspection inspection, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sql = $@"
INSERT INTO [{_schema}].[INSPECTION_ASSIGNMENTS]
    (INSPECTION_DATE, SLOT_ID, INSPECTOR_ID, APPLICATION_ID, INSPECTION_DATE_TIME, STATUS_CODE, ADD_TS, ADD_BY)
VALUES
    (@InspectionDate, @SlotId, @InspectorId, @AppId, @InspectionDateTime, @StatusCode, @Ts, @AddBy);";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            inspection.InspectionDate,
            inspection.SlotId,
            inspection.InspectorId,
            inspection.AppId,
            inspection.InspectionDateTime,
            inspection.StatusCode,
            Ts = DateTime.UtcNow,
            AddBy = "intra"
        }, transaction, cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(inspection.Notes))
        {
            var notesSql = $@"
INSERT INTO [{_schema}].[INSPECTION_NOTES] (APPLICATION_ID, SEQ_NUM, NOTES_TEXT, ADD_BY, ADD_TS)
SELECT @AppId, ISNULL(MAX(SEQ_NUM), 0) + 1, @Notes, @AddBy, @Ts
FROM [{_schema}].[INSPECTION_NOTES] WHERE APPLICATION_ID = @AppId;";
            await connection.ExecuteAsync(new CommandDefinition(notesSql, new
            {
                inspection.AppId,
                inspection.Notes,
                Ts = DateTime.UtcNow,
                AddBy = "intra"
            }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        return inspection;
    }

    public async Task<Inspection?> UpdateAsync(Inspection inspection, CancellationToken cancellationToken = default)
    {
        var sql = $@"
UPDATE [{_schema}].[INSPECTION_ASSIGNMENTS]
SET INSPECTOR_ID = COALESCE(@InspectorId, INSPECTOR_ID),
    INSPECTION_DATE_TIME = @InspectionDateTime,
    STATUS_CODE = @StatusCode,
    UPDATE_TS = @Ts,
    UPDATE_BY = @UpdateBy
WHERE INSPECTION_DATE = @InspectionDate AND SLOT_ID = @SlotId;

SELECT a.INSPECTION_DATE AS InspectionDate, a.SLOT_ID AS SlotId, a.APPLICATION_ID AS AppId,
       a.INSPECTOR_ID AS InspectorId, a.INSPECTION_DATE_TIME AS InspectionDateTime, a.STATUS_CODE AS StatusCode,
       CAST(NULL AS varchar(4000)) AS Notes
FROM [{_schema}].[INSPECTION_ASSIGNMENTS] a
WHERE a.INSPECTION_DATE = @InspectionDate AND a.SLOT_ID = @SlotId;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Inspection>(new CommandDefinition(sql, new
        {
            inspection.InspectionDate,
            inspection.SlotId,
            inspection.InspectorId,
            inspection.InspectionDateTime,
            inspection.StatusCode,
            Ts = DateTime.UtcNow,
            UpdateBy = "intra"
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(DateTime inspectionDate, int slotId, CancellationToken cancellationToken = default)
    {
        var sql = $@"DELETE FROM [{_schema}].[INSPECTION_ASSIGNMENTS]
WHERE INSPECTION_DATE = @InspectionDate AND SLOT_ID = @SlotId;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new { InspectionDate = inspectionDate.Date, SlotId = slotId }, cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<int?> GetMaxSlotsPerDayAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT TOP 1 MAX_SLOTS_PER_DAY FROM [{_schema}].[INSPECTION_CONTROLS] ORDER BY ID;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<InspectionUnavailableDay>> GetUnavailableDaysAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT INSPECTION_DATE AS InspectionDate, COMMENTS AS Comments
FROM [{_schema}].[INSPECTION_UNAVAILABLE_DAYS]
WHERE INSPECTION_DATE >= @From AND INSPECTION_DATE < @To
ORDER BY INSPECTION_DATE;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<InspectionUnavailableDay>(new CommandDefinition(sql, new { From = from.Date, To = to.Date.AddDays(1) }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Inspection>> GetAssignmentsInRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT INSPECTION_DATE AS InspectionDate, SLOT_ID AS SlotId, APPLICATION_ID AS AppId,
       INSPECTOR_ID AS InspectorId, INSPECTION_DATE_TIME AS InspectionDateTime, STATUS_CODE AS StatusCode,
       CAST(NULL AS varchar(4000)) AS Notes
FROM [{_schema}].[INSPECTION_ASSIGNMENTS]
WHERE INSPECTION_DATE >= @From AND INSPECTION_DATE < @To
ORDER BY INSPECTION_DATE, SLOT_ID;";
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Inspection>(new CommandDefinition(sql, new { From = from.Date, To = to.Date.AddDays(1) }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    // ================================================================================
    // Intra Inspections menu list screens — ported from BeanSearch list queries.
    // ================================================================================

    private static readonly IReadOnlyDictionary<string, string> PendingSortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["appId"] = "app.application_id",
        ["contractor"] = "app.app_business_name",
        ["city"] = "site_city_name",
        ["projectStart"] = "app.project_start_date",
    };

    private static readonly IReadOnlyDictionary<string, string> DueSortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["appId"] = "app.application_id",
        ["contractor"] = "app.app_business_name",
        ["city"] = "SiteCityName",
        ["inspector"] = "InspectorName",
        ["dueDate"] = "due.due_date",
    };

    private static readonly IReadOnlyDictionary<string, string> HoldSortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["appId"] = "app.application_id",
        ["contractor"] = "app.app_business_name",
        ["city"] = "SiteCityName",
        ["inspector"] = "InspectorName",
        ["projectStart"] = "app.project_start_date",
    };

    // MM/DD/YYYY display time (e.g. "9:00 AM"), matching the legacy INSPECTION_TIME_DISP expression.
    private const string TimeDispExpr =
        "LTRIM(SUBSTRING(CONVERT(VARCHAR(20), a.INSPECTION_DATE_TIME, 22), 10, 5) + RIGHT(CONVERT(VARCHAR(20), a.INSPECTION_DATE_TIME, 22), 3))";

    private static string Direction(string? sortDir) =>
        string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

    private static string? PermitRange(string? min, string? max)
    {
        min = min?.Trim();
        max = max?.Trim();
        if (string.IsNullOrEmpty(min))
        {
            return null;
        }

        return string.Equals(min, max, StringComparison.OrdinalIgnoreCase) ? min : $"{min} to {max}";
    }

    public async Task<InspectionPendingSearchResult> SearchPendingInspectionsAsync(InspectionPendingSearchRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        var existsFilter = new StringBuilder();

        if (request.InspectorId.HasValue)
        {
            existsFilter.Append(" AND ia.inspector_id = @InspectorId");
            parameters.Add("InspectorId", request.InspectorId.Value);
        }

        if (request.InspectionDate.HasValue)
        {
            existsFilter.Append(" AND ia.inspection_date >= @InspectionDate AND ia.inspection_date < DATEADD(day, 1, @InspectionDate)");
            parameters.Add("InspectionDate", request.InspectionDate.Value.Date);
        }

        var whereApp = $@"app.status_code = 'APPRV'
      AND EXISTS (SELECT 1 FROM [{_schema}].[INSPECTION_ASSIGNMENTS] ia
                  WHERE ia.application_id = app.application_id AND ia.status_code = 'IPEND'{existsFilter})";

        var direction = Direction(request.SortDir);
        var sortColumn = !string.IsNullOrWhiteSpace(request.SortBy) && PendingSortColumns.TryGetValue(request.SortBy, out var mapped)
            ? mapped
            : "app.project_start_date";
        var orderBy = SqlOrderBy.WithTiebreaker(sortColumn, direction, "app.application_id");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM [{_schema}].[APPLICATION_INFO] app WHERE {whereApp};",
            parameters,
            cancellationToken: cancellationToken));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;
        parameters.Add("OffsetRows", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $@"
SELECT app.application_id AS AppId,
       app.app_business_name AS AppBusinessName,
       LTRIM(RTRIM(ISNULL(app.app_first_name, '') + ' ' + ISNULL(app.app_last_name, ''))) AS ApplicantName,
       app.project_site_location AS ProjectSiteLocation,
       cty.city_name AS SiteCityName,
       CONVERT(CHAR(10), app.project_start_date, 101) AS ProjectStartDate,
       CONVERT(CHAR(10), app.project_end_date, 101) AS ProjectEndDate,
       (SELECT MIN(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = app.application_id) AS MinPermit,
       (SELECT MAX(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = app.application_id) AS MaxPermit
FROM [{_schema}].[APPLICATION_INFO] app
LEFT JOIN [{_schema}].[CITY_CODES] cty ON app.project_site_city_code = cty.city_code
WHERE {whereApp}
ORDER BY {orderBy}
OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var appRows = (await connection.QueryAsync<PendingAppRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        var scheduleByApp = new Dictionary<string, List<InspectionScheduleLineDto>>(StringComparer.OrdinalIgnoreCase);
        var appIds = appRows.Select(r => r.AppId).ToList();
        if (appIds.Count > 0)
        {
            var scheduleSql = $@"
SELECT a.APPLICATION_ID AS AppId,
       CONVERT(CHAR(10), a.INSPECTION_DATE, 101) AS InspectionDate,
       {TimeDispExpr} AS InspectionTimeDisp,
       a.INSPECTOR_ID AS InspectorId, b.INSPECTOR_NAME AS InspectorName,
       a.STATUS_CODE AS StatusCode, s.STATUS_DESC AS StatusDesc
FROM [{_schema}].[INSPECTION_ASSIGNMENTS] a
LEFT JOIN [{_schema}].[INSPECTORS] b ON a.INSPECTOR_ID = b.INSPECTOR_ID
LEFT JOIN [{_schema}].[STATUS_CODES] s ON a.STATUS_CODE = s.STATUS_CODE
WHERE a.APPLICATION_ID IN @AppIds
ORDER BY a.APPLICATION_ID, a.INSPECTION_DATE, a.INSPECTION_DATE_TIME;";

            var lines = await connection.QueryAsync<InspectionScheduleLineDto>(new CommandDefinition(scheduleSql, new { AppIds = appIds }, cancellationToken: cancellationToken));
            foreach (var line in lines)
            {
                if (!scheduleByApp.TryGetValue(line.AppId, out var list))
                {
                    list = new List<InspectionScheduleLineDto>();
                    scheduleByApp[line.AppId] = list;
                }

                list.Add(line);
            }
        }

        var items = appRows.Select(r => new InspectionPendingItemDto(
            r.AppId,
            PermitRange(r.MinPermit, r.MaxPermit),
            r.AppBusinessName,
            r.ApplicantName,
            r.ProjectSiteLocation,
            r.SiteCityName,
            r.ProjectStartDate,
            r.ProjectEndDate,
            scheduleByApp.TryGetValue(r.AppId, out var schedule) ? schedule : new List<InspectionScheduleLineDto>())).ToList();

        return new InspectionPendingSearchResult(items, total);
    }

    public Task<InspectionDueSearchResult> SearchPendingWcrAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default)
        => SearchDueListAsync(request, "PDWR", "awp.dwr_due_date", "ia.inspection_date_time", cancellationToken);

    public Task<InspectionDueSearchResult> SearchPendingGeoLogAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default)
        => SearchDueListAsync(request, "PGEO", "awp.geolog_due_date", "ia.inspection_date", cancellationToken);

    // Shared implementation of pending_dwr_list.jsp / pending_geo_list.jsp: applications whose permits
    // are pending a WCR (PDWR) or GeoLog (PGEO), with the earliest due date and the latest-assigned
    // inspector (CROSS APPLY + inner INSPECTORS join reproduces the legacy inner join that hides
    // applications without an assigned inspector).
    private async Task<InspectionDueSearchResult> SearchDueListAsync(InspectionDueSearchRequest request, string statusCode, string dueColumn, string latestOrderColumn, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("StatusCode", statusCode);
        var where = new StringBuilder(" WHERE 1 = 1");

        if (!string.IsNullOrWhiteSpace(request.AppId))
        {
            where.Append(" AND app.application_id = @AppId");
            parameters.Add("AppId", request.AppId.Trim());
        }

        if (request.InspectorId.HasValue)
        {
            where.Append(" AND insp.inspector_id = @InspectorId");
            parameters.Add("InspectorId", request.InspectorId.Value);
        }

        var fromClause = $@"
FROM (
    SELECT aw.application_id, MIN({dueColumn}) AS due_date
    FROM [{_schema}].[WORK_TYPES] wt
    INNER JOIN [{_schema}].[APP_WORKS] aw ON wt.work_category = aw.work_category AND wt.work_type = aw.work_type
    INNER JOIN [{_schema}].[APP_WORK_PERMITS] awp ON aw.application_id = awp.application_id AND aw.work_id = awp.work_id
    WHERE awp.status_code = @StatusCode
    GROUP BY aw.application_id
) due
JOIN [{_schema}].[APPLICATION_INFO] app ON app.application_id = due.application_id
LEFT JOIN [{_schema}].[CITY_CODES] cty ON app.project_site_city_code = cty.city_code
CROSS APPLY (
    SELECT TOP 1 ia.inspector_id, i.inspector_name
    FROM [{_schema}].[INSPECTION_ASSIGNMENTS] ia
    INNER JOIN [{_schema}].[INSPECTORS] i ON ia.inspector_id = i.inspector_id
    WHERE ia.application_id = app.application_id
    ORDER BY {latestOrderColumn} DESC
) insp{where}";

        var direction = Direction(request.SortDir);
        var sortColumn = !string.IsNullOrWhiteSpace(request.SortBy) && DueSortColumns.TryGetValue(request.SortBy, out var mapped)
            ? mapped
            : "app.application_id";
        var orderBy = SqlOrderBy.WithTiebreaker(sortColumn, direction, "app.application_id");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*){fromClause};",
            parameters,
            cancellationToken: cancellationToken));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;
        parameters.Add("OffsetRows", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $@"
SELECT app.application_id AS AppId,
       app.app_business_name AS AppBusinessName,
       LTRIM(RTRIM(ISNULL(app.app_first_name, '') + ' ' + ISNULL(app.app_last_name, ''))) AS ApplicantName,
       app.project_site_location AS ProjectSiteLocation,
       cty.city_name AS SiteCityName,
       CONVERT(CHAR(10), due.due_date, 101) AS DueDate,
       insp.inspector_id AS InspectorId,
       LTRIM(RTRIM(insp.inspector_name)) AS InspectorName,
       (SELECT MIN(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = app.application_id) AS MinPermit,
       (SELECT MAX(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = app.application_id) AS MaxPermit
{fromClause}
ORDER BY {orderBy}
OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var rows = await connection.QueryAsync<DueRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var items = rows.Select(r => new InspectionDueItemDto(
            r.AppId,
            PermitRange(r.MinPermit, r.MaxPermit),
            r.AppBusinessName,
            r.ApplicantName,
            r.ProjectSiteLocation,
            r.SiteCityName,
            r.InspectorId,
            r.InspectorName,
            r.DueDate)).ToList();

        return new InspectionDueSearchResult(items, total);
    }

    public async Task<InspectionHoldSearchResult> SearchHoldListAsync(InspectionHoldSearchRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        var where = new StringBuilder();
        where.Append($@" WHERE EXISTS (SELECT 1 FROM [{_schema}].[APP_WORK_PERMITS] awp WHERE awp.application_id = app.application_id AND awp.status_code = 'HOLD')");

        if (request.InspectorId.HasValue)
        {
            where.Append(" AND insp.inspector_id = @InspectorId");
            parameters.Add("InspectorId", request.InspectorId.Value);
        }

        var fromClause = $@"
FROM [{_schema}].[APPLICATION_INFO] app
LEFT JOIN [{_schema}].[CITY_CODES] cty ON app.project_site_city_code = cty.city_code
CROSS APPLY (
    SELECT TOP 1 ia.inspector_id, i.inspector_name
    FROM [{_schema}].[INSPECTION_ASSIGNMENTS] ia
    INNER JOIN [{_schema}].[INSPECTORS] i ON ia.inspector_id = i.inspector_id
    WHERE ia.application_id = app.application_id
    ORDER BY ia.inspection_date DESC
) insp{where}";

        var direction = Direction(request.SortDir);
        var sortColumn = !string.IsNullOrWhiteSpace(request.SortBy) && HoldSortColumns.TryGetValue(request.SortBy, out var mapped)
            ? mapped
            : "app.application_id";
        var orderBy = SqlOrderBy.WithTiebreaker(sortColumn, direction, "app.application_id");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*){fromClause};",
            parameters,
            cancellationToken: cancellationToken));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;
        parameters.Add("OffsetRows", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $@"
SELECT app.application_id AS AppId,
       app.app_business_name AS AppBusinessName,
       LTRIM(RTRIM(ISNULL(app.app_first_name, '') + ' ' + ISNULL(app.app_last_name, ''))) AS ApplicantName,
       app.project_site_location AS ProjectSiteLocation,
       cty.city_name AS SiteCityName,
       CONVERT(CHAR(10), app.project_start_date, 101) AS ProjectStartDate,
       CONVERT(CHAR(10), app.project_end_date, 101) AS ProjectEndDate,
       insp.inspector_id AS InspectorId,
       LTRIM(RTRIM(insp.inspector_name)) AS InspectorName,
       'HOLD' AS StatusCode,
       (SELECT MIN(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = app.application_id) AS MinPermit,
       (SELECT MAX(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = app.application_id) AS MaxPermit
{fromClause}
ORDER BY {orderBy}
OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var rows = await connection.QueryAsync<HoldRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var items = rows.Select(r => new InspectionHoldItemDto(
            r.AppId,
            PermitRange(r.MinPermit, r.MaxPermit),
            r.AppBusinessName,
            r.ApplicantName,
            r.ProjectSiteLocation,
            r.SiteCityName,
            r.ProjectStartDate,
            r.ProjectEndDate,
            r.InspectorId,
            r.InspectorName,
            r.StatusCode)).ToList();

        return new InspectionHoldSearchResult(items, total);
    }

    public async Task<IReadOnlyList<InspectionScheduleLineDto>> GetScheduledInspectionsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT a.APPLICATION_ID AS AppId,
       CONVERT(CHAR(10), a.INSPECTION_DATE, 101) AS InspectionDate,
       {TimeDispExpr} AS InspectionTimeDisp,
       a.INSPECTOR_ID AS InspectorId, b.INSPECTOR_NAME AS InspectorName,
       a.STATUS_CODE AS StatusCode, s.STATUS_DESC AS StatusDesc
FROM [{_schema}].[INSPECTION_ASSIGNMENTS] a
LEFT JOIN [{_schema}].[INSPECTORS] b ON a.INSPECTOR_ID = b.INSPECTOR_ID
LEFT JOIN [{_schema}].[STATUS_CODES] s ON a.STATUS_CODE = s.STATUS_CODE
WHERE a.INSPECTION_DATE >= @From AND a.INSPECTION_DATE < @To
ORDER BY a.INSPECTION_DATE, a.INSPECTION_DATE_TIME;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<InspectionScheduleLineDto>(new CommandDefinition(sql, new { From = from.Date, To = to.Date.AddDays(1) }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private sealed class PendingAppRow
    {
        public string AppId { get; init; } = string.Empty;
        public string? AppBusinessName { get; init; }
        public string? ApplicantName { get; init; }
        public string? ProjectSiteLocation { get; init; }
        public string? SiteCityName { get; init; }
        public string? ProjectStartDate { get; init; }
        public string? ProjectEndDate { get; init; }
        public string? MinPermit { get; init; }
        public string? MaxPermit { get; init; }
    }

    private sealed class DueRow
    {
        public string AppId { get; init; } = string.Empty;
        public string? AppBusinessName { get; init; }
        public string? ApplicantName { get; init; }
        public string? ProjectSiteLocation { get; init; }
        public string? SiteCityName { get; init; }
        public string? DueDate { get; init; }
        public int? InspectorId { get; init; }
        public string? InspectorName { get; init; }
        public string? MinPermit { get; init; }
        public string? MaxPermit { get; init; }
    }

    private sealed class HoldRow
    {
        public string AppId { get; init; } = string.Empty;
        public string? AppBusinessName { get; init; }
        public string? ApplicantName { get; init; }
        public string? ProjectSiteLocation { get; init; }
        public string? SiteCityName { get; init; }
        public string? ProjectStartDate { get; init; }
        public string? ProjectEndDate { get; init; }
        public int? InspectorId { get; init; }
        public string? InspectorName { get; init; }
        public string? StatusCode { get; init; }
        public string? MinPermit { get; init; }
        public string? MaxPermit { get; init; }
    }
}
