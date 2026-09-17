using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Persistence.Repositories;

/// <summary>
/// Ports the legacy BeanHistPermits.retrieveHistory / BeanHistWells.retrieveHistory SQL to
/// parameterized Dapper with server-side paging and whitelisted sorting. The history tables are
/// static legacy data (permits issued 1987 - April 2005), so only read/search is supported.
/// </summary>
public sealed class HistoryRepository : IHistoryRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<HistoryRepository> _logger;
    private readonly string _schema;

    public HistoryRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions, ILogger<HistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    // Whitelisted sort keys → SQL column, so an arbitrary SortBy value can never reach the SQL text.
    private static readonly IReadOnlyDictionary<string, string> PermitSortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["permitNum"] = "hp.permit_num",
        ["workType"] = "hp.work_type",
        ["consultant"] = "hp.consultant",
        ["driller"] = "hp.driller_name",
        ["address"] = "hp.addr_street",
        ["city"] = "hp.city_name",
        ["startDt"] = "hp.start_date",
        ["permitDt"] = "hp.permit_date",
    };

    private static readonly IReadOnlyDictionary<string, string> WellSortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["permitNum"] = "hwl.permit_num",
        ["tractNum"] = "hwl.tract_num",
        ["sectNum"] = "hwl.sect_num",
        ["addrStreet"] = "hwl.addr_street",
        ["addrCity"] = "hwl.addr_city",
        ["ownerName"] = "hwl.owner_name",
        ["wellUse"] = "hwl.well_use",
    };

    public async Task<HistoryPermitSearchResult> SearchPermitsAsync(HistoryPermitSearchRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        var where = new StringBuilder(" WHERE 1 = 1");

        void Like(string? value, string column, string name)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                where.Append($" AND {column} LIKE @{name}");
                parameters.Add(name, $"%{value.Trim()}%");
            }
        }

        Like(request.PermitNum, "hp.permit_num", "PermitNum");
        Like(request.WorkType, "hp.work_type", "WorkType");
        Like(request.WellComplRptNum, "hp.well_compl_rpt_num", "WellComplRptNum");
        Like(request.CityName, "hp.city_name", "CityName");
        Like(request.Consultant, "hp.consultant", "Consultant");

        if (!string.IsNullOrWhiteSpace(request.AddrStreet))
        {
            where.Append(" AND (hp.addr_street LIKE @AddrStreet OR hp.bldg_num LIKE @AddrStreet)");
            parameters.Add("AddrStreet", $"%{request.AddrStreet.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(request.DrillerName))
        {
            where.Append(" AND (hp.driller_name LIKE @DrillerName OR hp.driller_license_num LIKE @DrillerName)");
            parameters.Add("DrillerName", $"%{request.DrillerName.Trim()}%");
        }

        if (request.PermitDate.HasValue)
        {
            where.Append(" AND hp.permit_date >= @PermitDate AND hp.permit_date < DATEADD(day, 1, @PermitDate)");
            parameters.Add("PermitDate", request.PermitDate.Value.Date);
        }

        if (request.StartDate.HasValue)
        {
            where.Append(" AND hp.start_date >= @StartDate AND hp.start_date < DATEADD(day, 1, @StartDate)");
            parameters.Add("StartDate", request.StartDate.Value.Date);
        }

        if (request.PermitYear.HasValue)
        {
            where.Append(" AND YEAR(hp.permit_date) = @PermitYear");
            parameters.Add("PermitYear", request.PermitYear.Value);
        }

        if (request.StartYear.HasValue)
        {
            where.Append(" AND YEAR(hp.start_date) = @StartYear");
            parameters.Add("StartYear", request.StartYear.Value);
        }

        var direction = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var sortColumn = !string.IsNullOrWhiteSpace(request.SortBy) && PermitSortColumns.TryGetValue(request.SortBy, out var mapped)
            ? mapped
            : "hp.permit_num";
        var orderBy = SqlOrderBy.WithTiebreaker(sortColumn, direction, "hp.permit_num");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM [{_schema}].[HIST_PERMITS] hp{where};",
            parameters,
            cancellationToken: cancellationToken));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;
        parameters.Add("OffsetRows", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $@"
SELECT hp.permit_num AS PermitNum, hp.work_type AS WorkType, hp.well_compl_rpt_num AS WellComplRptNum,
       hp.bldg_num AS BldgNum, hp.addr_street AS AddrStreet, hp.city_name AS CityName,
       hp.consultant AS Consultant, hp.driller_name AS DrillerName, hp.driller_license_num AS DrillerLicenseNum,
       hp.state_well_number AS StateWellNumber,
       CONVERT(CHAR(10), hp.start_date, 101) AS StartDate,
       CONVERT(CHAR(10), hp.permit_date, 101) AS PermitDate
FROM [{_schema}].[HIST_PERMITS] hp{where}
ORDER BY {orderBy}
OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var rows = (await connection.QueryAsync<HistoryPermitDto>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();
        return new HistoryPermitSearchResult(rows, total);
    }

    public async Task<HistoryWellSearchResult> SearchWellsAsync(HistoryWellSearchRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        var where = new StringBuilder(" WHERE 1 = 1");

        void Like(string? value, string column, string name)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                where.Append($" AND {column} LIKE @{name}");
                parameters.Add(name, $"%{value.Trim()}%");
            }
        }

        Like(request.PermitNum, "hwl.permit_num", "PermitNum");
        Like(request.TractNum, "hwl.tract_num", "TractNum");
        Like(request.SectNum, "hwl.sect_num", "SectNum");
        Like(request.AddrStreet, "hwl.addr_street", "AddrStreet");
        Like(request.AddrCity, "hwl.addr_city", "AddrCity");
        Like(request.OwnerName, "hwl.owner_name", "OwnerName");
        Like(request.WellUse, "hwl.well_use", "WellUse");

        var direction = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var sortColumn = !string.IsNullOrWhiteSpace(request.SortBy) && WellSortColumns.TryGetValue(request.SortBy, out var mapped)
            ? mapped
            : "hwl.permit_num";
        var orderBy = SqlOrderBy.WithTiebreaker(sortColumn, direction, "hwl.permit_num");

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM [{_schema}].[HIST_WELL_LOC] hwl{where};",
            parameters,
            cancellationToken: cancellationToken));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 25 : request.PageSize;
        parameters.Add("OffsetRows", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $@"
SELECT hwl.well_key AS WellKey, hwl.permit_num AS PermitNum, hwl.tract_num AS TractNum, hwl.sect_num AS SectNum,
       hwl.addr_street AS AddrStreet, hwl.addr_city AS AddrCity, hwl.owner_name AS OwnerName,
       hwl.well_use AS WellUse
FROM [{_schema}].[HIST_WELL_LOC] hwl{where}
ORDER BY {orderBy}
OFFSET @OffsetRows ROWS FETCH NEXT @PageSize ROWS ONLY;";

        var rows = (await connection.QueryAsync<HistoryWellDto>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();
        return new HistoryWellSearchResult(rows, total);
    }

    // ---- Detail + edit (ports BeanHistPermits / BeanHistWells retrieveByPermit / retrieveByWellKey
    // and the UpdateAppServlet updhist / delhist / updHistWell / delHistWell operations). ----

    public async Task<HistoryPermitDetailDto?> GetPermitByIdAsync(string permitNum, CancellationToken cancellationToken = default)
    {
        // LEFT JOIN surfaces the linked well_key (cross-link to the well detail). A permit may have
        // several wells; legacy read the first row, so we take TOP 1 by well_key for determinism.
        var sql = $@"
SELECT TOP 1
       hp.permit_num AS PermitNum, hwl.well_key AS WellLocationWellKey, hp.work_type AS WorkType,
       hp.city_name AS CityName, hp.addr_street AS AddrStreet, hp.bldg_num AS BldgNum,
       hp.consultant AS Consultant,
       CONVERT(CHAR(10), hp.permit_date, 23) AS PermitDate,
       CONVERT(CHAR(10), hp.start_date, 23) AS StartDate,
       CONVERT(CHAR(10), hp.end_date, 23) AS EndDate,
       CONVERT(CHAR(10), hp.start_notice_date, 23) AS StartNoticeDate,
       CONVERT(CHAR(10), hp.seal_date, 23) AS SealDate,
       hp.well_compl_rpt_exmpt AS WellComplRptExmpt,
       CONVERT(CHAR(10), hp.well_compl_rpt_recvdt, 23) AS WellComplRptRecvdt,
       hp.well_compl_rpt_num AS WellComplRptNum, hp.state_well_number AS StateWellNumber,
       hp.driller_name AS DrillerName, hp.driller_license_num AS DrillerLicenseNum,
       CONVERT(CHAR(10), hp.add_ts, 23) AS AddDate, hp.add_by AS AddBy,
       CONVERT(CHAR(10), hp.update_ts, 23) AS UpdateDate, hp.update_by AS UpdateBy,
       hp.document_image_filename AS DocumentImageFilename, hp.permit_image_filename AS PermitImageFilename,
       hp.well_compl_rpt_filename AS WellComplRptFilename
FROM [{_schema}].[HIST_PERMITS] hp
LEFT OUTER JOIN [{_schema}].[HIST_WELL_LOC] hwl ON hp.permit_num = hwl.permit_num
WHERE hp.permit_num = @PermitNum
ORDER BY hwl.well_key;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<HistoryPermitDetailDto>(
            new CommandDefinition(sql, new { PermitNum = permitNum }, cancellationToken: cancellationToken));
    }

    public async Task<int> UpdatePermitAsync(string permitNum, HistoryPermitUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        // Date columns are set to NULL when the field is left blank (matches legacy Formatter date
        // handling). File-name columns are intentionally omitted so their values are preserved.
        var parameters = new DynamicParameters();
        parameters.Add("PermitNum", permitNum);
        parameters.Add("WorkType", Text(request.WorkType));
        parameters.Add("CityName", Text(request.CityName));
        parameters.Add("AddrStreet", Text(request.AddrStreet));
        parameters.Add("BldgNum", Text(request.BldgNum));
        parameters.Add("Consultant", Text(request.Consultant));
        parameters.Add("PermitDate", Date(request.PermitDate));
        parameters.Add("StartDate", Date(request.StartDate));
        parameters.Add("EndDate", Date(request.EndDate));
        parameters.Add("StartNoticeDate", Date(request.StartNoticeDate));
        parameters.Add("SealDate", Date(request.SealDate));
        parameters.Add("WellComplRptExmpt", Text(request.WellComplRptExmpt));
        parameters.Add("WellComplRptRecvdt", Date(request.WellComplRptRecvdt));
        parameters.Add("WellComplRptNum", Text(request.WellComplRptNum));
        parameters.Add("StateWellNumber", Text(request.StateWellNumber));
        parameters.Add("DrillerName", Text(request.DrillerName));
        parameters.Add("DrillerLicenseNum", Text(request.DrillerLicenseNum));
        parameters.Add("UpdatedBy", Text(updatedBy));

        var sql = $@"
UPDATE [{_schema}].[HIST_PERMITS] SET
    work_type = @WorkType, city_name = @CityName, addr_street = @AddrStreet, bldg_num = @BldgNum,
    consultant = @Consultant, permit_date = @PermitDate, start_date = @StartDate, end_date = @EndDate,
    start_notice_date = @StartNoticeDate, seal_date = @SealDate, well_compl_rpt_exmpt = @WellComplRptExmpt,
    well_compl_rpt_recvdt = @WellComplRptRecvdt, well_compl_rpt_num = @WellComplRptNum,
    state_well_number = @StateWellNumber, driller_name = @DrillerName, driller_license_num = @DrillerLicenseNum,
    update_ts = GETDATE(), update_by = @UpdatedBy
WHERE permit_num = @PermitNum;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<int> DeletePermitAsync(string permitNum, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM [{_schema}].[HIST_PERMITS] WHERE permit_num = @PermitNum;",
            new { PermitNum = permitNum },
            cancellationToken: cancellationToken));
    }

    public async Task<int> InsertPermitAsync(HistoryPermitCreateRequest request, string createdBy, CancellationToken cancellationToken = default)
    {
        // Ports BeanHistPermits.add: date columns are NULL when blank; the four file-name columns
        // start empty (files are uploaded separately, after the record exists); add_by/update_by set
        // to the acting user with add_ts/update_ts = GETDATE().
        var parameters = new DynamicParameters();
        parameters.Add("PermitNum", Text(request.PermitNum));
        parameters.Add("WorkType", Text(request.WorkType));
        parameters.Add("CityName", Text(request.CityName));
        parameters.Add("AddrStreet", Text(request.AddrStreet));
        parameters.Add("BldgNum", Text(request.BldgNum));
        parameters.Add("Consultant", Text(request.Consultant));
        parameters.Add("PermitDate", Date(request.PermitDate));
        parameters.Add("StartDate", Date(request.StartDate));
        parameters.Add("EndDate", Date(request.EndDate));
        parameters.Add("StartNoticeDate", Date(request.StartNoticeDate));
        parameters.Add("SealDate", Date(request.SealDate));
        parameters.Add("WellComplRptRecvdt", Date(request.WellComplRptRecvdt));
        parameters.Add("WellComplRptExmpt", Text(request.WellComplRptExmpt));
        parameters.Add("WellComplRptNum", Text(request.WellComplRptNum));
        parameters.Add("StateWellNumber", Text(request.StateWellNumber));
        parameters.Add("DrillerName", Text(request.DrillerName));
        parameters.Add("DrillerLicenseNum", Text(request.DrillerLicenseNum));
        parameters.Add("CreatedBy", Text(createdBy));

        var sql = $@"
INSERT INTO [{_schema}].[HIST_PERMITS]
    (permit_num, work_type, city_name, addr_street, bldg_num, consultant, permit_date, start_date,
     end_date, start_notice_date, seal_date, well_compl_rpt_recvdt, well_compl_rpt_exmpt,
     well_compl_rpt_num, state_well_number, driller_name, driller_license_num, well_compl_rpt_filename,
     permit_image_filename, sitemap_filename, document_image_filename, add_by, add_ts, update_by, update_ts)
VALUES
    (@PermitNum, @WorkType, @CityName, @AddrStreet, @BldgNum, @Consultant, @PermitDate, @StartDate,
     @EndDate, @StartNoticeDate, @SealDate, @WellComplRptRecvdt, @WellComplRptExmpt,
     @WellComplRptNum, @StateWellNumber, @DrillerName, @DrillerLicenseNum, '', '', '', '',
     @CreatedBy, GETDATE(), @CreatedBy, GETDATE());";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException("A history permit with that permit number already exists.", ex);
        }
    }

    public async Task<HistoryWellDetailDto?> GetWellByIdAsync(int wellKey, CancellationToken cancellationToken = default)
    {
        // history_permit_num (non-empty) drives the cross-link back to the permit detail.
        var sql = $@"
SELECT TOP 1
       hw.well_key AS WellKey, hw.permit_num AS PermitNum, hr.permit_num AS HistoryPermitNum,
       hw.tract_num AS TractNum, hw.sect_num AS SectNum, hw.addr_street AS AddrStreet,
       hw.addr_city AS AddrCity, hw.addr_city_code AS AddrCityCode, hw.owner_name AS OwnerName,
       hw.coord_x AS CoordX, hw.coord_y AS CoordY, hw.match_level AS MatchLevel, hw.tsrqq AS TsrQq,
       hw.rec_code AS RecCode, hw.phone_num AS PhoneNum,
       CONVERT(CHAR(10), hw.drill_date, 23) AS DrillDate, hw.elevation AS Elevation,
       hw.total_depth AS TotalDepth, hw.water_depth AS WaterDepth, hw.diameter AS Diameter,
       hw.well_use AS WellUse, hw.log_code AS LogCode, hw.wq AS Wq, hw.wi AS Wi, hw.yield AS Yield,
       hw.dtwcalc AS DtwCalc, CONVERT(CHAR(10), hw.add_ts, 23) AS AddDate, hw.add_by AS AddBy,
       CONVERT(CHAR(10), hw.update_ts, 23) AS UpdateDate, hw.update_by AS UpdateBy
FROM [{_schema}].[HIST_WELL_LOC] hw
LEFT OUTER JOIN [{_schema}].[HIST_PERMITS] hr ON hw.permit_num = hr.permit_num
WHERE hw.well_key = @WellKey;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<HistoryWellDetailDto>(
            new CommandDefinition(sql, new { WellKey = wellKey }, cancellationToken: cancellationToken));
    }

    public async Task<int> UpdateWellAsync(int wellKey, HistoryWellUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        // Unlike the legacy servlet (which set optional columns only when non-empty), the React edit
        // form always submits every field, so blank numeric/date fields become NULL (an explicit
        // clear) and blank text fields become empty strings.
        var parameters = new DynamicParameters();
        parameters.Add("WellKey", wellKey);
        parameters.Add("PermitNum", Text(request.PermitNum));
        parameters.Add("TractNum", Text(request.TractNum));
        parameters.Add("SectNum", Text(request.SectNum));
        parameters.Add("AddrStreet", Text(request.AddrStreet));
        parameters.Add("AddrCity", Text(request.AddrCity));
        parameters.Add("AddrCityCode", Text(request.AddrCityCode));
        parameters.Add("OwnerName", Text(request.OwnerName));
        parameters.Add("CoordX", Text(request.CoordX));
        parameters.Add("CoordY", Text(request.CoordY));
        parameters.Add("MatchLevel", Text(request.MatchLevel));
        parameters.Add("TsrQq", Text(request.TsrQq));
        parameters.Add("RecCode", Text(request.RecCode));
        parameters.Add("PhoneNum", Text(request.PhoneNum));
        parameters.Add("DrillDate", Date(request.DrillDate));
        parameters.Add("Elevation", Text(request.Elevation));
        parameters.Add("TotalDepth", IntOrNull(request.TotalDepth));
        parameters.Add("WaterDepth", DecimalOrNull(request.WaterDepth));
        parameters.Add("Diameter", Text(request.Diameter));
        parameters.Add("WellUse", Text(request.WellUse));
        parameters.Add("LogCode", Text(request.LogCode));
        parameters.Add("Wq", Text(request.Wq));
        parameters.Add("Wi", Text(request.Wi));
        parameters.Add("Yield", IntOrNull(request.Yield));
        parameters.Add("DtwCalc", IntOrNull(request.DtwCalc));
        parameters.Add("UpdatedBy", Text(updatedBy));

        var sql = $@"
UPDATE [{_schema}].[HIST_WELL_LOC] SET
    permit_num = @PermitNum, tract_num = @TractNum, sect_num = @SectNum, addr_street = @AddrStreet,
    addr_city = @AddrCity, addr_city_code = @AddrCityCode, owner_name = @OwnerName, coord_x = @CoordX,
    coord_y = @CoordY, match_level = @MatchLevel, tsrqq = @TsrQq, rec_code = @RecCode,
    phone_num = @PhoneNum, drill_date = @DrillDate, elevation = @Elevation, total_depth = @TotalDepth,
    water_depth = @WaterDepth, diameter = @Diameter, well_use = @WellUse, log_code = @LogCode,
    wq = @Wq, wi = @Wi, yield = @Yield, dtwcalc = @DtwCalc, update_ts = GETDATE(), update_by = @UpdatedBy
WHERE well_key = @WellKey;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteWellAsync(int wellKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM [{_schema}].[HIST_WELL_LOC] WHERE well_key = @WellKey;",
            new { WellKey = wellKey },
            cancellationToken: cancellationToken));
    }

    public async Task<int> InsertWellAsync(HistoryWellUpdateRequest request, string createdBy, CancellationToken cancellationToken = default)
    {
        // Ports BeanHistWells.add: well_key is an IDENTITY, so we return SCOPE_IDENTITY() as the new
        // key. Optional numeric/date columns are NULL when blank; add_by/update_by set to the acting
        // user with add_ts/update_ts = GETDATE().
        var parameters = new DynamicParameters();
        parameters.Add("PermitNum", Text(request.PermitNum));
        parameters.Add("TractNum", Text(request.TractNum));
        parameters.Add("SectNum", Text(request.SectNum));
        parameters.Add("AddrStreet", Text(request.AddrStreet));
        parameters.Add("AddrCity", Text(request.AddrCity));
        parameters.Add("AddrCityCode", Text(request.AddrCityCode));
        parameters.Add("OwnerName", Text(request.OwnerName));
        parameters.Add("CoordX", Text(request.CoordX));
        parameters.Add("CoordY", Text(request.CoordY));
        parameters.Add("MatchLevel", Text(request.MatchLevel));
        parameters.Add("TsrQq", Text(request.TsrQq));
        parameters.Add("RecCode", Text(request.RecCode));
        parameters.Add("PhoneNum", Text(request.PhoneNum));
        parameters.Add("DrillDate", Date(request.DrillDate));
        parameters.Add("Elevation", Text(request.Elevation));
        parameters.Add("TotalDepth", IntOrNull(request.TotalDepth));
        parameters.Add("WaterDepth", DecimalOrNull(request.WaterDepth));
        parameters.Add("Diameter", Text(request.Diameter));
        parameters.Add("WellUse", Text(request.WellUse));
        parameters.Add("LogCode", Text(request.LogCode));
        parameters.Add("Wq", Text(request.Wq));
        parameters.Add("Wi", Text(request.Wi));
        parameters.Add("Yield", IntOrNull(request.Yield));
        parameters.Add("DtwCalc", IntOrNull(request.DtwCalc));
        parameters.Add("CreatedBy", Text(createdBy));

        var sql = $@"
INSERT INTO [{_schema}].[HIST_WELL_LOC]
    (permit_num, tract_num, sect_num, addr_street, addr_city, addr_city_code, owner_name, coord_x,
     coord_y, match_level, tsrqq, rec_code, phone_num, drill_date, elevation, total_depth, water_depth,
     diameter, well_use, log_code, wq, wi, yield, dtwcalc, update_ts, update_by, add_ts, add_by)
VALUES
    (@PermitNum, @TractNum, @SectNum, @AddrStreet, @AddrCity, @AddrCityCode, @OwnerName, @CoordX,
     @CoordY, @MatchLevel, @TsrQq, @RecCode, @PhoneNum, @DrillDate, @Elevation, @TotalDepth, @WaterDepth,
     @Diameter, @WellUse, @LogCode, @Wq, @Wi, @Yield, @DtwCalc, GETDATE(), @CreatedBy, GETDATE(), @CreatedBy);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<HistoryCityDto>> GetHistoryCitiesAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT DISTINCT addr_city_code AS Code, addr_city AS Name
FROM [{_schema}].[HIST_WELL_LOC]
WHERE addr_city <> '' AND addr_city_code <> '' AND addr_city <> '?' AND addr_city_code <> '?'
ORDER BY addr_city;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<HistoryCityDto>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
        return rows;
    }

    // Trims a text value; null/whitespace becomes an empty string so the column is cleared, matching
    // the legacy Formatter.formatDbString behaviour.
    private static string Text(string? value) => value?.Trim() ?? string.Empty;

    // Parses an ISO (yyyy-MM-dd) date string; blank/invalid becomes NULL.
    private static object? Date(string? value)
        => !string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d
            : null;

    private static object? IntOrNull(string? value)
        => !string.IsNullOrWhiteSpace(value) && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    private static object? DecimalOrNull(string? value)
        => !string.IsNullOrWhiteSpace(value) && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
}
