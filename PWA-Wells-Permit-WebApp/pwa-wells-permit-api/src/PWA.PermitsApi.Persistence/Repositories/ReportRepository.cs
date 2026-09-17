using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Persistence.Repositories;

/// <summary>
/// Ports the legacy intra report SQL (BeanReportRecon / BeanReportStatus) to parameterized Dapper
/// queries. Date ranges use the legacy "BETWEEN start AND CAST(end AS datetime)+1" semantics via
/// DATEADD so an inclusive end-of-day is preserved.
/// </summary>
public sealed class ReportRepository : IReportRepository
{
    // Legacy Constants.PPHRASE used to encrypt acct_name_encr (payer name) in APP_PAYMENT_INFO.
    private const string Passphrase = "PWA Wells is map based";

    private readonly DapperContext _context;
    private readonly ILogger<ReportRepository> _logger;
    private readonly string _schema;

    public ReportRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions, ILogger<ReportRepository> logger)
    {
        _context = context;
        _logger = logger;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<IReadOnlyList<ReconciliationLineDto>> GetReconciliationAsync(DateTime start, DateTime end, string payType, CancellationToken cancellationToken = default)
    {
        var payFilter = PaymentTypeFilter(payType, "pay");

        var sql = $@"
SELECT pay.application_id AS AppId,
       app.app_business_name AS BusinessName,
       LTRIM(RTRIM(ISNULL(app.app_first_name,'') + ' ' + ISNULL(app.app_last_name,''))) AS ApplicantName,
       CONVERT(varchar(50), DecryptByPassPhrase(@Passphrase, pay.acct_name_encr, 1, CONVERT(varbinary(13), pay.application_id))) AS Payer,
       RTRIM(pay.payment_type) AS PaymentType,
       RTRIM(ISNULL(pt.payment_desc, '')) AS PaymentDesc,
       RTRIM(ISNULL(pay.check_num, '')) AS CheckNum,
       RTRIM(ISNULL(pay.receipt_num, '')) AS ReceiptNum,
       pay.paid_amount AS Amount,
       CONVERT(char(10), pay.paid_date, 101) AS PaidDate,
       CONVERT(char(8), pay.paid_date, 108) AS PaidTime,
       (SELECT MIN(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = pay.application_id) AS MinPermit,
       (SELECT MAX(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = pay.application_id) AS MaxPermit
FROM [{_schema}].[APP_PAYMENT_INFO] pay
JOIN [{_schema}].[APPLICATION_INFO] app ON app.application_id = pay.application_id
LEFT JOIN [{_schema}].[payment_types] pt ON pt.payment_type = pay.payment_type
WHERE pay.status_code = 'PAID'
  AND pay.update_ts >= @Start AND pay.update_ts < DATEADD(day, 1, @End)
  {payFilter}
ORDER BY pay.payment_type, PaidDate, PaidTime, pay.application_id;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ReconRow>(new CommandDefinition(sql, new { Passphrase, Start = start.Date, End = end.Date }, cancellationToken: cancellationToken));

        return rows.Select(r => new ReconciliationLineDto(
            r.AppId,
            r.BusinessName,
            r.ApplicantName,
            r.Payer,
            r.PaymentType ?? string.Empty,
            r.PaymentDesc,
            r.CheckNum,
            r.ReceiptNum,
            r.PaidDate,
            r.PaidTime,
            r.Amount,
            PermitRange(r.MinPermit, r.MaxPermit))).ToList();
    }

    public async Task<IReadOnlyList<CompletedWorkLineDto>> GetCompletedWorksAsync(DateTime start, DateTime end, string? inspectorId, string? cityCode, CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder($@"
SELECT CONVERT(varchar(20), IA.INSPECTOR_ID) AS InspectorId, I.INSPECTOR_NAME AS InspectorName,
       W.WORK_CATEGORY AS WorkCategory, C.WORK_CAT_DESC AS WorkCatDesc, W.WORK_TYPE AS WorkType,
       (SELECT TOP 1 WT.WORK_DESC FROM [{_schema}].[WORK_TYPES] WT WHERE WT.WORK_CATEGORY = W.WORK_CATEGORY AND WT.WORK_TYPE = W.WORK_TYPE) AS WorkDesc,
       W.WELL_USE_TYPE AS WellUseType,
       (SELECT TOP 1 WU.WELL_USE_DESC FROM [{_schema}].[WORK_USE_TYPES] WU WHERE WU.WORK_CATEGORY = W.WORK_CATEGORY AND WU.WORK_TYPE = W.WORK_TYPE AND WU.WELL_USE_TYPE = W.WELL_USE_TYPE) AS WellUseDesc,
       A.APPLICATION_ID AS AppId, A.PROJECT_SITE_CITY_CODE AS CityCode,
       (SELECT TOP 1 CN.CITY_NAME FROM [{_schema}].[CITY_CODES] CN WHERE CN.CITY_CODE = A.PROJECT_SITE_CITY_CODE) AS CityName,
       P.PERMIT_NUMBER AS PermitNumber, P.STATUS_CODE AS PermitStatus,
       ISNULL(SP.DRILL_COUNT, 0) AS DrillCount,
       CONVERT(CHAR(10), P.INSPECTION_COMPLETION_DATE, 101) AS InspectionCompleteDate
FROM [{_schema}].[APPLICATION_INFO] A,
     [{_schema}].[APP_WORK_PERMITS] P,
     [{_schema}].[APP_WORK_SPECS] SP,
     [{_schema}].[INSPECTORS] I,
     [{_schema}].[APP_WORKS] W,
     [{_schema}].[WORK_CATEGORIES] C,
     [{_schema}].[INSPECTION_ASSIGNMENTS] IA
WHERE P.STATUS_CODE IN ('PCLSD')
  AND P.APPLICATION_ID = W.APPLICATION_ID
  AND P.WORK_ID = W.WORK_ID
  AND P.APPLICATION_ID = IA.APPLICATION_ID
  AND SP.APPLICATION_ID = P.APPLICATION_ID
  AND SP.WORK_ID = P.WORK_ID
  AND SP.WORK_SPECS_ID = P.WORK_SPECS_ID
  AND I.INSPECTOR_ID = IA.INSPECTOR_ID
  AND P.APPLICATION_ID = A.APPLICATION_ID
  AND P.INSPECTION_COMPLETION_DATE >= @Start AND P.INSPECTION_COMPLETION_DATE < DATEADD(day, 1, @End)
  AND W.WORK_CATEGORY = C.WORK_CATEGORY
  AND IA.INSPECTION_DATE = (SELECT MAX(IA2.INSPECTION_DATE) FROM [{_schema}].[INSPECTION_ASSIGNMENTS] IA2
                            WHERE IA2.APPLICATION_ID = IA.APPLICATION_ID AND IA2.STATUS_CODE IN ('ICOMP','IWAIV'))");

        var parameters = new DynamicParameters();
        parameters.Add("Start", start.Date);
        parameters.Add("End", end.Date);

        if (!string.IsNullOrWhiteSpace(inspectorId))
        {
            sql.Append(" AND I.INSPECTOR_ID = @InspectorId");
            parameters.Add("InspectorId", inspectorId);
        }

        if (!string.IsNullOrWhiteSpace(cityCode))
        {
            sql.Append(" AND A.PROJECT_SITE_CITY_CODE = @CityCode");
            parameters.Add("CityCode", cityCode);
        }

        sql.Append(" ORDER BY IA.INSPECTOR_ID, W.WORK_CATEGORY, W.WORK_TYPE, P.APPLICATION_ID, P.WORK_ID, P.WORK_SPECS_ID, P.PERMIT_NUMBER;");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CompletedWorkRow>(new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken));
        return rows.Select(r => new CompletedWorkLineDto(
            r.InspectorId ?? string.Empty,
            r.InspectorName,
            r.WorkCategory ?? string.Empty,
            r.WorkCatDesc,
            r.WorkType ?? string.Empty,
            r.WorkDesc,
            r.WellUseType,
            r.WellUseDesc,
            r.AppId ?? string.Empty,
            r.CityCode,
            r.CityName,
            r.PermitNumber,
            r.PermitStatus,
            r.DrillCount,
            r.InspectionCompleteDate)).ToList();
    }

    public async Task<IReadOnlyList<InspectionReportLineDto>> GetCompletedInspectionsByInspectorAsync(DateTime? fromDate, DateTime? toDate, string? inspectorId, CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder($@"
SELECT DISTINCT I.INSPECTOR_ID AS InspectorId, I.INSPECTOR_NAME AS InspectorName,
       APP.APPLICATION_ID AS AppId, APP.APP_BUSINESS_NAME AS BusinessName,
       LTRIM(RTRIM(ISNULL(APP.APP_FIRST_NAME,'') + ' ' + ISNULL(APP.APP_LAST_NAME,''))) AS ApplicantName,
       APP.PROJECT_SITE_LOCATION AS ProjectSiteLocation, CTY.CITY_NAME AS CityName,
       CONVERT(CHAR(10), APP.PROJECT_START_DATE, 101) AS ProjectStartDate,
       CONVERT(CHAR(10), APP.PROJECT_END_DATE, 101) AS ProjectEndDate,
       (SELECT MIN(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = APP.APPLICATION_ID) AS MinPermit,
       (SELECT MAX(wp.permit_number) FROM [{_schema}].[APP_WORK_PERMITS] wp WHERE wp.application_id = APP.APPLICATION_ID) AS MaxPermit
FROM (SELECT DISTINCT APPLICATION_ID, INSPECTOR_ID FROM [{_schema}].[INSPECTION_ASSIGNMENTS] WHERE STATUS_CODE = 'ICOMP') AS IA
LEFT OUTER JOIN [{_schema}].[INSPECTORS] I ON IA.INSPECTOR_ID = I.INSPECTOR_ID
LEFT OUTER JOIN [{_schema}].[APPLICATION_INFO] APP ON IA.APPLICATION_ID = APP.APPLICATION_ID
LEFT JOIN [{_schema}].[CITY_CODES] CTY ON APP.PROJECT_SITE_CITY_CODE = CTY.CITY_CODE
WHERE 1 = 1");

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(inspectorId))
        {
            sql.Append(" AND I.INSPECTOR_ID = @InspectorId");
            parameters.Add("InspectorId", inspectorId);
        }

        if (fromDate.HasValue && toDate.HasValue)
        {
            sql.Append(" AND APP.APPROVED_DATE BETWEEN @FromDate AND @ToDate");
            parameters.Add("FromDate", fromDate.Value.Date);
            parameters.Add("ToDate", toDate.Value.Date);
        }

        sql.Append(" ORDER BY I.INSPECTOR_NAME;");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<InspectionRow>(new CommandDefinition(sql.ToString(), parameters, cancellationToken: cancellationToken));

        return rows.Select(r => new InspectionReportLineDto(
            r.InspectorId ?? string.Empty,
            r.InspectorName,
            r.AppId ?? string.Empty,
            PermitRange(r.MinPermit, r.MaxPermit),
            r.BusinessName,
            r.ApplicantName,
            r.ProjectSiteLocation,
            r.CityName,
            r.ProjectStartDate,
            r.ProjectEndDate)).ToList();
    }

    public async Task<ExtractReportResult> GetExtractAsync(ExtractReportRequest request, CancellationToken cancellationToken = default)
    {
        var selectClause = $@"
SELECT T1.application_id AS AppId, T1.project_site_location AS ProjLocation, T1.project_site_city_code AS CityCode,
       T1.city_name AS CityName, T1.app_business_name AS BusinessName, T1.work_id AS WorkId,
       T1.work_category AS WorkCategory,
       (SELECT TOP 1 wc.work_cat_desc FROM [{_schema}].[WORK_CATEGORIES] wc WHERE wc.work_category = T1.work_category) AS WorkCatDesc,
       T1.work_type AS WorkType,
       (SELECT TOP 1 wt.work_desc FROM [{_schema}].[WORK_TYPES] wt WHERE wt.work_category = T1.work_category AND wt.work_type = T1.work_type) AS WorkDesc,
       T1.hist_work_type AS HistWorkType,
       T1.hist_well_use AS HistWellUse, T1.work_specs_id AS WorkSpecsId, T1.state_well_id AS StateWellNum,
       T1.latitude AS Latitude, T1.longitude AS Longitude, T1.tract_num AS TractNum, T1.sect_num AS SectNum,
       T1.permit_number AS PermitNumber, T1.permit_issued_date AS PermitIssuedDate, T1.permit_status AS PermitStatus,
       (SELECT TOP 1 sc.status_desc FROM [{_schema}].[STATUS_CODES] sc WHERE sc.status_code = T1.permit_status) AS PermitStatusDesc";

        var fromClause = $@"
FROM (
    SELECT app.application_id AS application_id, app.project_site_location AS project_site_location,
           app.project_site_city_code AS project_site_city_code, cd.city_name AS city_name,
           app.app_business_name AS app_business_name, wrk.work_id AS work_id, wrk.work_category AS work_category,
           wrk.work_type AS work_type, '' AS hist_work_type, '' AS hist_well_use, wsp.work_specs_id AS work_specs_id,
           wsp.state_well_id AS state_well_id, IsNull(wsp.latitude,'') AS latitude, IsNull(wsp.longitude,'') AS longitude,
           '' AS tract_num, '' AS sect_num, IsNull(awp.permit_number,'') AS permit_number, awp.status_code AS permit_status,
           awp.permit_issued_date AS sort_permit_iss_date,
           CONVERT(CHAR(10), awp.permit_issued_date, 101) AS permit_issued_date, YEAR(IsNull(awp.permit_issued_date, Null)) AS permit_issued_year
    FROM [{_schema}].[APPLICATION_INFO] app
    LEFT OUTER JOIN [{_schema}].[CITY_CODES] cd ON cd.city_code = app.project_site_city_code
    LEFT OUTER JOIN [{_schema}].[APP_WORKS] wrk ON wrk.application_id = app.application_id
    LEFT OUTER JOIN [{_schema}].[APP_WORK_SPECS] wsp ON wsp.application_id = wrk.application_id AND wsp.work_id = wrk.work_id
    LEFT OUTER JOIN [{_schema}].[APP_WORK_PERMITS] awp ON awp.application_id = wsp.application_id AND awp.work_id = wsp.work_id
         AND (awp.work_specs_id = wsp.work_specs_id OR awp.work_specs_id = 0)
    UNION
    SELECT 'Hist Permit' AS application_id, hp.addr_street + '' + IsNull(hp.bldg_num,'') AS project_site_location,
           '' AS project_site_city_code, hp.city_name AS city_name, hp.consultant AS app_business_name,
           '' AS work_id, '' AS work_category, '' AS work_type, hp.work_type AS hist_work_type, '' AS hist_well_use,
           '' AS work_specs_id, hp.state_well_number AS state_well_id, '' AS latitude, '' AS longitude,
           '' AS tract_num, '' AS sect_num, IsNull(hp.permit_num,'') AS permit_number, '' AS permit_status,
           hp.permit_date AS sort_permit_iss_date,
           CONVERT(CHAR(10), IsNull(hp.permit_date, Null), 101) AS permit_issued_date, YEAR(IsNull(hp.permit_date, Null)) AS permit_issued_year
    FROM [{_schema}].[HIST_PERMITS] AS hp
    UNION
    SELECT 'Hist Well Loc' AS application_id, hw.addr_street AS project_site_location,
           '' AS project_site_city_code, hw.addr_city AS city_name, hw.owner_name AS app_business_name,
           '' AS work_id, '' AS work_category, '' AS work_type, '' AS hist_work_type, hw.well_use AS hist_well_use,
           '' AS work_specs_id, '' AS state_well_id, hw.coord_x AS latitude, hw.coord_y AS longitude,
           IsNull(hw.tract_num,'') AS tract_num, IsNull(hw.sect_num,'') AS sect_num,
           IsNull(hw.permit_num,'') AS permit_number, '' AS permit_status,
           null AS sort_permit_iss_date, null AS permit_issued_date, '' AS permit_issued_year
    FROM [{_schema}].[HIST_WELL_LOC] AS hw
) AS T1";

        var parameters = new DynamicParameters();
        var where = new StringBuilder();

        void And(string clause)
        {
            where.Append(where.Length == 0 ? " WHERE " : " AND ");
            where.Append(clause);
        }

        if (!string.IsNullOrWhiteSpace(request.IssueYear))
        {
            And("T1.permit_issued_year = @IssueYear");
            parameters.Add("IssueYear", request.IssueYear.Trim());
        }

        if (request.PermitFromDate.HasValue && request.PermitToDate.HasValue)
        {
            And("T1.sort_permit_iss_date >= @PermitFrom AND T1.sort_permit_iss_date <= DATEADD(day, 1, @PermitTo)");
            parameters.Add("PermitFrom", request.PermitFromDate.Value.Date);
            parameters.Add("PermitTo", request.PermitToDate.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(request.CityName))
        {
            And("T1.city_name = @CityName");
            parameters.Add("CityName", request.CityName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.PermitNum))
        {
            And("T1.permit_number LIKE @PermitNum");
            parameters.Add("PermitNum", "%" + request.PermitNum.Trim() + "%");
        }

        // The three "category" sub-groups below are OR-ed together and then AND-ed onto the base
        // filter (mirrors BeanReportStatus.retrieveExtractData's wheresql2/3/4 combination).
        var orGroups = new List<string>();

        var current = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.PermitStatus)) { current.Add("T1.permit_status = @PermitStatus"); parameters.Add("PermitStatus", request.PermitStatus.Trim()); }
        if (!string.IsNullOrWhiteSpace(request.WorkCategory)) { current.Add("T1.work_category = @WorkCategory"); parameters.Add("WorkCategory", request.WorkCategory.Trim()); }
        if (!string.IsNullOrWhiteSpace(request.WorkType)) { current.Add("T1.work_type = @WorkType"); parameters.Add("WorkType", request.WorkType.Trim()); }
        if (current.Count > 0) orGroups.Add("(" + string.Join(" AND ", current) + ")");

        if (!string.IsNullOrWhiteSpace(request.HistWorkType))
        {
            orGroups.Add("(T1.hist_work_type LIKE @HistWorkType)");
            parameters.Add("HistWorkType", "%" + request.HistWorkType.Trim() + "%");
        }

        var histLoc = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.TractNum)) { histLoc.Add("T1.tract_num LIKE @TractNum"); parameters.Add("TractNum", "%" + request.TractNum.Trim() + "%"); }
        if (!string.IsNullOrWhiteSpace(request.SectNum)) { histLoc.Add("T1.sect_num LIKE @SectNum"); parameters.Add("SectNum", "%" + request.SectNum.Trim() + "%"); }
        if (!string.IsNullOrWhiteSpace(request.HistWellUse)) { histLoc.Add("T1.hist_well_use LIKE @HistWellUse"); parameters.Add("HistWellUse", "%" + request.HistWellUse.Trim() + "%"); }
        if (histLoc.Count > 0) orGroups.Add("(" + string.Join(" AND ", histLoc) + ")");

        if (orGroups.Count > 0)
        {
            And("(" + string.Join(" OR ", orGroups) + ")");
        }

        // Whitelisted sortable columns (T1.* aliases from the UNION). Anything else falls back to
        // the legacy default order. The composite (permit, work, work-specs) is always appended as
        // a deterministic tiebreaker, skipping the chosen column to avoid SQL Server error 169
        // (a column specified more than once in ORDER BY).
        var sortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["permitNumber"] = "T1.permit_number",
            ["appId"] = "T1.application_id",
            ["projLocation"] = "T1.project_site_location",
            ["cityName"] = "T1.city_name",
            ["businessName"] = "T1.app_business_name",
            ["workId"] = "T1.work_id",
            ["workCategory"] = "T1.work_category",
            ["workType"] = "T1.work_type",
            ["histWorkType"] = "T1.hist_work_type",
            ["histWellUse"] = "T1.hist_well_use",
            ["stateWellNum"] = "T1.state_well_id",
            ["tractNum"] = "T1.tract_num",
            ["sectNum"] = "T1.sect_num",
            ["permitIssuedDate"] = "T1.sort_permit_iss_date",
            ["permitStatus"] = "T1.permit_status",
        };

        var direction = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        string orderBy;
        if (!string.IsNullOrWhiteSpace(request.SortBy) && sortColumns.TryGetValue(request.SortBy.Trim(), out var sortCol))
        {
            var parts = new List<string> { $"{sortCol} {direction}" };
            foreach (var tb in new[] { "T1.permit_number", "T1.work_id", "T1.work_specs_id" })
            {
                if (!string.Equals(tb, sortCol, StringComparison.OrdinalIgnoreCase)) parts.Add(tb);
            }
            orderBy = string.Join(", ", parts);
        }
        else
        {
            orderBy = "T1.permit_number, T1.work_id, T1.work_specs_id";
        }

        var whereSql = where.ToString();

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*)" + fromClause + whereSql, parameters, cancellationToken: cancellationToken));

        var dataSql = new StringBuilder();
        dataSql.Append(selectClause).Append(fromClause).Append(whereSql);
        dataSql.Append(" ORDER BY ").Append(orderBy);

        // PageSize <= 0 returns every matching row (used by the "Export to Excel" CSV action, which
        // must include the full filtered set); a positive PageSize pages the on-screen grid.
        if (request.PageSize > 0)
        {
            var page = request.Page < 1 ? 1 : request.Page;
            parameters.Add("OffsetRows", (page - 1) * request.PageSize);
            parameters.Add("PageSize", request.PageSize);
            dataSql.Append(" OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY");
        }
        dataSql.Append(';');

        var rows = await connection.QueryAsync<ExtractRow>(new CommandDefinition(dataSql.ToString(), parameters, cancellationToken: cancellationToken));
        var items = rows.Select(r => new ExtractLineDto(
            r.AppId ?? string.Empty,
            r.ProjLocation,
            r.CityCode,
            r.CityName,
            r.BusinessName,
            r.WorkId,
            r.WorkCategory,
            r.WorkCatDesc,
            r.WorkType,
            r.WorkDesc,
            r.HistWorkType,
            r.HistWellUse,
            r.WorkSpecsId,
            r.StateWellNum,
            r.Latitude,
            r.Longitude,
            r.TractNum,
            r.SectNum,
            r.PermitNumber,
            r.PermitIssuedDate,
            r.PermitStatus,
            r.PermitStatusDesc)).ToList();
        return new ExtractReportResult(items, totalCount);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT LTRIM(RTRIM(status_code)) AS StatusCode, COUNT(*) AS Cnt
FROM [{_schema}].[APPLICATION_INFO]
GROUP BY status_code;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string StatusCode, int Cnt)>(new CommandDefinition(sql, cancellationToken: cancellationToken));

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.StatusCode))
            {
                result[row.StatusCode] = row.Cnt;
            }
        }
        return result;
    }

    private static string PaymentTypeFilter(string payType, string alias) => payType?.ToLowerInvariant() switch
    {
        "cc" => $"AND ({alias}.payment_type = 'MC' OR {alias}.payment_type = 'VISA')",
        "ch" => $"AND ({alias}.payment_type = 'CHECK' OR {alias}.payment_type = 'CASH')",
        _ => string.Empty
    };

    private static string? PermitRange(string? min, string? max)
    {
        min = min?.Trim();
        max = max?.Trim();
        if (string.IsNullOrEmpty(min) && string.IsNullOrEmpty(max)) return null;
        if (string.IsNullOrEmpty(max) || string.Equals(min, max, StringComparison.OrdinalIgnoreCase)) return min;
        if (string.IsNullOrEmpty(min)) return max;
        return $"{min} to {max}";
    }

    private sealed class ReconRow
    {
        public string AppId { get; init; } = string.Empty;
        public string? BusinessName { get; init; }
        public string? ApplicantName { get; init; }
        public string? Payer { get; init; }
        public string? PaymentType { get; init; }
        public string? PaymentDesc { get; init; }
        public string? CheckNum { get; init; }
        public string? ReceiptNum { get; init; }
        public string? PaidDate { get; init; }
        public string? PaidTime { get; init; }
        public decimal Amount { get; init; }
        public string? MinPermit { get; init; }
        public string? MaxPermit { get; init; }
    }

    private sealed class InspectionRow
    {
        public string? InspectorId { get; init; }
        public string? InspectorName { get; init; }
        public string? AppId { get; init; }
        public string? BusinessName { get; init; }
        public string? ApplicantName { get; init; }
        public string? ProjectSiteLocation { get; init; }
        public string? CityName { get; init; }
        public string? ProjectStartDate { get; init; }
        public string? ProjectEndDate { get; init; }
        public string? MinPermit { get; init; }
        public string? MaxPermit { get; init; }
    }

    private sealed class CompletedWorkRow
    {
        public string? InspectorId { get; set; }
        public string? InspectorName { get; set; }
        public string? WorkCategory { get; set; }
        public string? WorkCatDesc { get; set; }
        public string? WorkType { get; set; }
        public string? WorkDesc { get; set; }
        public string? WellUseType { get; set; }
        public string? WellUseDesc { get; set; }
        public string? AppId { get; set; }
        public string? CityCode { get; set; }
        public string? CityName { get; set; }
        public string? PermitNumber { get; set; }
        public string? PermitStatus { get; set; }
        public int DrillCount { get; set; }
        public string? InspectionCompleteDate { get; set; }
    }

    private sealed class ExtractRow
    {
        public string? AppId { get; set; }
        public string? ProjLocation { get; set; }
        public string? CityCode { get; set; }
        public string? CityName { get; set; }
        public string? BusinessName { get; set; }
        public string? WorkId { get; set; }
        public string? WorkCategory { get; set; }
        public string? WorkCatDesc { get; set; }
        public string? WorkType { get; set; }
        public string? WorkDesc { get; set; }
        public string? HistWorkType { get; set; }
        public string? HistWellUse { get; set; }
        public string? WorkSpecsId { get; set; }
        public string? StateWellNum { get; set; }
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? TractNum { get; set; }
        public string? SectNum { get; set; }
        public string? PermitNumber { get; set; }
        public string? PermitIssuedDate { get; set; }
        public string? PermitStatus { get; set; }
        public string? PermitStatusDesc { get; set; }
    }
}
