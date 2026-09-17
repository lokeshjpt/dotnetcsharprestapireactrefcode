using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Exceptions;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Persistence.Repositories;

// Reference-data ("Code Maintenance") repository — parameterised ports of the legacy BeanCodes* /
// Bean* SQL against the EEAOWN lookup tables. All writes flow through Execute* which translates
// SQL-Server key/foreign-key violations into the same "already exists" / "in use" outcomes the
// legacy servlet surfaced.
public sealed class MaintenanceRepository : IMaintenanceRepository
{
    private const int DuplicateKeyError = 2627;   // PRIMARY KEY / UNIQUE constraint violation
    private const int DuplicateKeyError2 = 2601;  // UNIQUE index violation
    private const int ForeignKeyError = 547;      // REFERENCE constraint (delete of in-use code)

    private readonly DapperContext _context;
    private readonly string _schema;

    public MaintenanceRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions)
    {
        _context = context;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    // ---------------------------------------------------------------- Cities
    public Task<IReadOnlyList<CityCodeDto>> GetCitiesAsync(CancellationToken ct = default)
        => QueryAsync<CityCodeDto>($@"SELECT LTRIM(RTRIM(city_code)) AS CityCode, city_name AS CityName, county_juris AS CountyJuris
FROM [{_schema}].[city_codes] ORDER BY city_code;", null, ct);

    public Task AddCityAsync(CityCodeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[city_codes] (city_code, city_name, county_juris)
VALUES (@CityCode, @CityName, @CountyJuris);",
            new { r.CityCode, r.CityName, r.CountyJuris }, "City Code", ct);

    public Task UpdateCityAsync(CityCodeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[city_codes] SET city_name = @CityName, county_juris = @CountyJuris
WHERE city_code = @CityCode;",
            new { r.CityCode, r.CityName, r.CountyJuris }, "Update Failed: City Code does not exists.", ct);

    public Task DeleteCityAsync(string cityCode, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[city_codes] WHERE city_code = @CityCode;",
            new { CityCode = cityCode }, "Delete Failed: City Code does not exists.", ct);

    // ---------------------------------------------------------------- States
    public Task<IReadOnlyList<StateCodeDto>> GetStatesAsync(CancellationToken ct = default)
        => QueryAsync<StateCodeDto>($@"SELECT LTRIM(RTRIM(state_code)) AS StateCode, state_name AS StateName
FROM [{_schema}].[state_codes] ORDER BY state_code;", null, ct);

    public Task AddStateAsync(StateCodeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[state_codes] (state_code, state_name) VALUES (@StateCode, @StateName);",
            new { r.StateCode, r.StateName }, "State Code", ct);

    public Task UpdateStateAsync(StateCodeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[state_codes] SET state_name = @StateName WHERE state_code = @StateCode;",
            new { r.StateCode, r.StateName }, "Update Failed: State Code does not exists.", ct);

    public Task DeleteStateAsync(string stateCode, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[state_codes] WHERE state_code = @StateCode;",
            new { StateCode = stateCode }, "Delete Failed: State Code does not exists.", ct);

    // ---------------------------------------------------------------- Work categories
    public Task<IReadOnlyList<WorkCategoryRowDto>> GetWorkCategoriesAsync(CancellationToken ct = default)
        => QueryAsync<WorkCategoryRowDto>($@"SELECT LTRIM(RTRIM(work_category)) AS WorkCategory, work_cat_desc AS WorkCatDesc, active_flag AS ActiveFlag
FROM [{_schema}].[work_categories] ORDER BY work_cat_desc;", null, ct);

    public Task AddWorkCategoryAsync(WorkCategoryRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[work_categories] (work_category, work_cat_desc, active_flag)
VALUES (@WorkCategory, @WorkCatDesc, @ActiveFlag);",
            new { r.WorkCategory, r.WorkCatDesc, r.ActiveFlag }, "Work Category", ct);

    public Task UpdateWorkCategoryAsync(WorkCategoryRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[work_categories] SET work_cat_desc = @WorkCatDesc, active_flag = @ActiveFlag
WHERE work_category = @WorkCategory;",
            new { r.WorkCategory, r.WorkCatDesc, r.ActiveFlag }, "Update Failed: Work Category does not exists.", ct);

    public Task DeleteWorkCategoryAsync(string workCategory, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[work_categories] WHERE work_category = @WorkCategory;",
            new { WorkCategory = workCategory }, "Delete Failed: Work Category does not exists.", ct);

    // ---------------------------------------------------------------- Work types
    public Task<IReadOnlyList<WorkTypeRowDto>> GetWorkTypesAsync(CancellationToken ct = default)
        => QueryAsync<WorkTypeRowDto>($@"SELECT LTRIM(RTRIM(wt.work_category)) AS WorkCategory, wc.work_cat_desc AS WorkCatDesc,
       LTRIM(RTRIM(wt.work_type)) AS WorkType, wt.work_desc AS WorkDesc,
       wt.fee_rate_amt AS FeeRateAmt, LTRIM(RTRIM(wt.fee_unit)) AS FeeUnit, wt.site_max AS SiteMax,
       wt.dwr_required AS DwrRequired, wt.geolog_required AS GeologRequired, wt.active_flag AS ActiveFlag
FROM [{_schema}].[work_types] wt
LEFT JOIN [{_schema}].[work_categories] wc ON wc.work_category = wt.work_category
ORDER BY wt.fee_unit, wt.work_category, wt.work_desc;", null, ct);

    public Task AddWorkTypeAsync(WorkTypeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[work_types]
       (work_category, work_type, work_desc, fee_rate_amt, fee_unit, site_max, dwr_required, active_flag, geolog_required)
VALUES (@WorkCategory, @WorkType, @WorkDesc, @FeeRateAmt, @FeeUnit, @SiteMax, @DwrRequired, @ActiveFlag, @GeologRequired);",
            new { r.WorkCategory, r.WorkType, r.WorkDesc, r.FeeRateAmt, r.FeeUnit, r.SiteMax, r.DwrRequired, r.ActiveFlag, r.GeologRequired },
            "Work Type", ct);

    public Task UpdateWorkTypeAsync(WorkTypeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[work_types] SET work_desc = @WorkDesc, dwr_required = @DwrRequired,
       geolog_required = @GeologRequired, active_flag = @ActiveFlag, fee_rate_amt = @FeeRateAmt,
       fee_unit = @FeeUnit, site_max = @SiteMax
WHERE work_type = @WorkType AND work_category = @WorkCategory;",
            new { r.WorkCategory, r.WorkType, r.WorkDesc, r.FeeRateAmt, r.FeeUnit, r.SiteMax, r.DwrRequired, r.GeologRequired, r.ActiveFlag },
            "Update Failed: Work Type does not exists.", ct);

    public Task DeleteWorkTypeAsync(string workCategory, string workType, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[work_types] WHERE work_category = @WorkCategory AND work_type = @WorkType;",
            new { WorkCategory = workCategory, WorkType = workType }, "Delete Failed: Work Type does not exists.", ct);

    // ---------------------------------------------------------------- Well use types
    public Task<IReadOnlyList<WellUseTypeRowDto>> GetWellUseTypesAsync(CancellationToken ct = default)
        => QueryAsync<WellUseTypeRowDto>($@"SELECT LTRIM(RTRIM(wu.work_category)) AS WorkCategory, wc.work_cat_desc AS WorkCatDesc,
       LTRIM(RTRIM(wu.work_type)) AS WorkType, wt.work_desc AS WorkDesc,
       LTRIM(RTRIM(wu.well_use_type)) AS WellUseType, wu.well_use_desc AS WellUseDesc, wu.active_flag AS ActiveFlag
FROM [{_schema}].[work_use_types] wu
LEFT JOIN [{_schema}].[work_categories] wc ON wc.work_category = wu.work_category
LEFT JOIN [{_schema}].[work_types] wt ON wt.work_category = wu.work_category AND wt.work_type = wu.work_type
ORDER BY wu.well_use_desc;", null, ct);

    public Task AddWellUseTypeAsync(WellUseTypeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[work_use_types] (work_category, work_type, well_use_type, well_use_desc, active_flag)
VALUES (@WorkCategory, @WorkType, @WellUseType, @WellUseDesc, @ActiveFlag);",
            new { r.WorkCategory, r.WorkType, r.WellUseType, r.WellUseDesc, r.ActiveFlag }, "Well Use Type", ct);

    public Task UpdateWellUseTypeAsync(WellUseTypeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[work_use_types] SET well_use_desc = @WellUseDesc, active_flag = @ActiveFlag
WHERE work_category = @WorkCategory AND work_type = @WorkType AND well_use_type = @WellUseType;",
            new { r.WorkCategory, r.WorkType, r.WellUseType, r.WellUseDesc, r.ActiveFlag },
            "Update Failed: Well use type under this work type and work category does not exists.", ct);

    public Task DeleteWellUseTypeAsync(string workCategory, string workType, string wellUseType, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[work_use_types]
WHERE work_category = @WorkCategory AND work_type = @WorkType AND well_use_type = @WellUseType;",
            new { WorkCategory = workCategory, WorkType = workType, WellUseType = wellUseType },
            "Delete Failed: Well Use Type does not exists.", ct);

    // ---------------------------------------------------------------- Drill methods
    public Task<IReadOnlyList<DrillMethodRowDto>> GetDrillMethodsAsync(CancellationToken ct = default)
        => QueryAsync<DrillMethodRowDto>($@"SELECT LTRIM(RTRIM(drill_method_type)) AS DrillMethodType, drill_method_name AS DrillMethodName, active_flag AS ActiveFlag
FROM [{_schema}].[drill_method_types] ORDER BY drill_method_name;", null, ct);

    public Task AddDrillMethodAsync(DrillMethodRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[drill_method_types] (drill_method_type, drill_method_name, active_flag)
VALUES (@DrillMethodType, @DrillMethodName, @ActiveFlag);",
            new { r.DrillMethodType, r.DrillMethodName, r.ActiveFlag }, "Drill Type", ct);

    public Task UpdateDrillMethodAsync(DrillMethodRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[drill_method_types] SET drill_method_name = @DrillMethodName, active_flag = @ActiveFlag
WHERE drill_method_type = @DrillMethodType;",
            new { r.DrillMethodType, r.DrillMethodName, r.ActiveFlag }, "Update Failed: Drill Code does not exists.", ct);

    public Task DeleteDrillMethodAsync(string drillMethodType, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[drill_method_types] WHERE drill_method_type = @DrillMethodType;",
            new { DrillMethodType = drillMethodType }, "Delete Failed: Drill Type does not exists.", ct);

    // ---------------------------------------------------------------- Condition types
    public Task<IReadOnlyList<ConditionTypeRowDto>> GetConditionTypesAsync(CancellationToken ct = default)
        => QueryAsync<ConditionTypeRowDto>($@"SELECT LTRIM(RTRIM(condition_type)) AS ConditionType, condition_desc AS ConditionDesc, active_flag AS ActiveFlag
FROM [{_schema}].[condition_types] ORDER BY condition_type;", null, ct);

    public Task AddConditionTypeAsync(ConditionTypeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[condition_types] (condition_type, condition_desc, active_flag)
VALUES (@ConditionType, @ConditionDesc, @ActiveFlag);",
            new { r.ConditionType, r.ConditionDesc, r.ActiveFlag }, "Condition Type", ct);

    public Task UpdateConditionTypeAsync(ConditionTypeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[condition_types] SET condition_desc = @ConditionDesc, active_flag = @ActiveFlag
WHERE condition_type = @ConditionType;",
            new { r.ConditionType, r.ConditionDesc, r.ActiveFlag }, "Update Failed: Condition Type does not exists.", ct);

    public Task DeleteConditionTypeAsync(string conditionType, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[condition_types] WHERE condition_type = @ConditionType;",
            new { ConditionType = conditionType }, "Delete Failed: Condition Type does not exists.", ct);

    // ---------------------------------------------------------------- Work condition types
    public Task<IReadOnlyList<WorkConditionTypeRowDto>> GetWorkConditionTypesAsync(CancellationToken ct = default)
        => QueryAsync<WorkConditionTypeRowDto>($@"SELECT LTRIM(RTRIM(wct.work_category)) AS WorkCategory, wc.work_cat_desc AS WorkCatDesc,
       LTRIM(RTRIM(wct.work_type)) AS WorkType, wt.work_desc AS WorkDesc,
       LTRIM(RTRIM(wct.condition_type)) AS ConditionType, cot.condition_desc AS ConditionDesc
FROM [{_schema}].[work_condition_types] wct
LEFT JOIN [{_schema}].[condition_types] cot ON cot.condition_type = wct.condition_type
LEFT JOIN [{_schema}].[work_categories] wc ON wc.work_category = wct.work_category
LEFT JOIN [{_schema}].[work_types] wt ON wt.work_category = wct.work_category AND wt.work_type = wct.work_type
ORDER BY wct.work_category, wct.work_type, wct.condition_type;", null, ct);

    public Task AddWorkConditionTypeAsync(WorkConditionTypeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[work_condition_types] (work_category, work_type, condition_type)
VALUES (@WorkCategory, @WorkType, @ConditionType);",
            new { r.WorkCategory, r.WorkType, r.ConditionType }, "Work Condition Type", ct);

    public Task DeleteWorkConditionTypeAsync(string workCategory, string workType, string conditionType, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[work_condition_types]
WHERE work_category = @WorkCategory AND work_type = @WorkType AND condition_type = @ConditionType;",
            new { WorkCategory = workCategory, WorkType = workType, ConditionType = conditionType },
            "Delete Failed: Work Condition Type under this Work Category and Work Type does not exists.", ct);

    // ---------------------------------------------------------------- Payment types
    public Task<IReadOnlyList<PaymentTypeRowDto>> GetPaymentTypesAsync(CancellationToken ct = default)
        => QueryAsync<PaymentTypeRowDto>($@"SELECT LTRIM(RTRIM(payment_type)) AS PaymentType, payment_desc AS PaymentDesc,
       service_charge AS ServiceCharge, display_seq AS DisplaySeq
FROM [{_schema}].[payment_types] ORDER BY display_seq;", null, ct);

    public Task AddPaymentTypeAsync(PaymentTypeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[payment_types] (payment_type, payment_desc, service_charge, display_seq)
VALUES (@PaymentType, @PaymentDesc, @ServiceCharge, @DisplaySeq);",
            new { r.PaymentType, r.PaymentDesc, r.ServiceCharge, r.DisplaySeq }, "Payment Type", ct);

    public Task UpdatePaymentTypeAsync(PaymentTypeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[payment_types] SET payment_desc = @PaymentDesc, service_charge = @ServiceCharge, display_seq = @DisplaySeq
WHERE payment_type = @PaymentType;",
            new { r.PaymentType, r.PaymentDesc, r.ServiceCharge, r.DisplaySeq }, "Update Failed: Payment Type does not exists.", ct);

    public Task DeletePaymentTypeAsync(string paymentType, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[payment_types] WHERE payment_type = @PaymentType;",
            new { PaymentType = paymentType }, "Delete Failed: Payment Type does not exists.", ct);

    // ---------------------------------------------------------------- Status codes
    public Task<IReadOnlyList<StatusCodeRowDto>> GetStatusCodesAsync(CancellationToken ct = default)
        => QueryAsync<StatusCodeRowDto>($@"SELECT LTRIM(RTRIM(status_code)) AS StatusCode, status_desc AS StatusDesc, sort_seq AS SortSeq
FROM [{_schema}].[status_codes] ORDER BY status_code;", null, ct);

    public Task AddStatusCodeAsync(StatusCodeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[status_codes] (status_code, status_desc, sort_seq) VALUES (@StatusCode, @StatusDesc, @SortSeq);",
            new { r.StatusCode, r.StatusDesc, r.SortSeq }, "Status Code", ct);

    public Task UpdateStatusCodeAsync(StatusCodeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[status_codes] SET status_desc = @StatusDesc, sort_seq = @SortSeq WHERE status_code = @StatusCode;",
            new { r.StatusCode, r.StatusDesc, r.SortSeq }, "Update Failed: Status Code does not exists.", ct);

    public Task DeleteStatusCodeAsync(string statusCode, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[status_codes] WHERE status_code = @StatusCode;",
            new { StatusCode = statusCode }, "Delete Failed: Status Code does not exists.", ct);

    // ---------------------------------------------------------------- Document types
    public Task<IReadOnlyList<DocumentTypeRowDto>> GetDocumentTypesAsync(CancellationToken ct = default)
        => QueryAsync<DocumentTypeRowDto>($@"SELECT UPPER(LTRIM(RTRIM(DOCUMENT_TYPE))) AS DocumentType, DOCUMENT_DESC AS DocumentDesc
FROM [{_schema}].[DOCUMENT_TYPES] ORDER BY DOCUMENT_TYPE;", null, ct);

    public Task AddDocumentTypeAsync(DocumentTypeRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[DOCUMENT_TYPES] (DOCUMENT_TYPE, DOCUMENT_DESC) VALUES (@DocumentType, @DocumentDesc);",
            new { r.DocumentType, r.DocumentDesc }, "Document Type", ct);

    public Task UpdateDocumentTypeAsync(DocumentTypeRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[DOCUMENT_TYPES] SET DOCUMENT_DESC = @DocumentDesc WHERE DOCUMENT_TYPE = @DocumentType;",
            new { r.DocumentType, r.DocumentDesc }, "Update Failed: Document Type does not exists.", ct);

    public Task DeleteDocumentTypeAsync(string documentType, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[DOCUMENT_TYPES] WHERE DOCUMENT_TYPE = @DocumentType;",
            new { DocumentType = documentType }, "Cannot delete, type is in use", ct);

    // ---------------------------------------------------------------- Inspectors
    public Task<IReadOnlyList<InspectorRowDto>> GetInspectorsAsync(CancellationToken ct = default)
        => QueryAsync<InspectorRowDto>($@"SELECT INSPECTOR_ID AS InspectorId, LTRIM(RTRIM(INSPECTOR_NAME)) AS InspectorName,
       INSPECTOR_EMAIL_ADDR AS InspectorEmail, INSPECTOR_PHONE_NUM AS InspectorPhone, ACTIVE_FLAG AS ActiveFlag,
       ADD_BY AS AddBy, ADD_TS AS AddTs, UPDATE_BY AS UpdateBy, UPDATE_TS AS UpdateTs
FROM [{_schema}].[INSPECTORS] ORDER BY INSPECTOR_NAME;", null, ct);

    public async Task<int> AddInspectorAsync(InspectorRequest r, string actingUser, CancellationToken ct = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(ct);
        var nextId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT ISNULL(MAX(INSPECTOR_ID), 0) + 1 FROM [{_schema}].[INSPECTORS];", cancellationToken: ct));
        try
        {
            await connection.ExecuteAsync(new CommandDefinition($@"INSERT INTO [{_schema}].[INSPECTORS]
       (INSPECTOR_ID, INSPECTOR_NAME, INSPECTOR_EMAIL_ADDR, INSPECTOR_PHONE_NUM, ACTIVE_FLAG, ADD_BY, ADD_TS)
VALUES (@InspectorId, @InspectorName, @InspectorEmail, @InspectorPhone, @ActiveFlag, @AddBy, GETDATE());",
                new { InspectorId = nextId, r.InspectorName, r.InspectorEmail, r.InspectorPhone, r.ActiveFlag, AddBy = actingUser },
                cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number is DuplicateKeyError or DuplicateKeyError2)
        {
            throw new MaintenanceConflictException("Add Failed: Inspector already exists, please try again.");
        }

        return nextId;
    }

    public async Task UpdateInspectorAsync(InspectorRequest r, string actingUser, CancellationToken ct = default)
    {
        var rows = await ExecuteAsync($@"UPDATE [{_schema}].[INSPECTORS] SET INSPECTOR_NAME = @InspectorName,
       INSPECTOR_EMAIL_ADDR = @InspectorEmail, INSPECTOR_PHONE_NUM = @InspectorPhone, ACTIVE_FLAG = @ActiveFlag,
       UPDATE_BY = @UpdateBy, UPDATE_TS = GETDATE()
WHERE INSPECTOR_ID = @InspectorId;",
            new { r.InspectorId, r.InspectorName, r.InspectorEmail, r.InspectorPhone, r.ActiveFlag, UpdateBy = actingUser }, null, ct);
        if (rows < 1)
        {
            throw new MaintenanceNotFoundException("Update Failed: Inspector does not exists.");
        }
    }

    // ---------------------------------------------------------------- Inspection unavailable days
    public Task<IReadOnlyList<UnavailableDayDto>> GetUnavailableDaysAsync(CancellationToken ct = default)
        => QueryAsync<UnavailableDayDto>($@"SELECT CONVERT(CHAR(10), INSPECTION_DATE, 101) AS InspectionDate, COMMENTS AS Comments
FROM [{_schema}].[INSPECTION_UNAVAILABLE_DAYS] ORDER BY INSPECTION_DATE DESC;", null, ct);

    public Task AddUnavailableDayAsync(UnavailableDayRequest r, CancellationToken ct = default)
        => AddAsync($@"INSERT INTO [{_schema}].[INSPECTION_UNAVAILABLE_DAYS] (INSPECTION_DATE, COMMENTS)
VALUES (CONVERT(date, @InspectionDate, 101), @Comments);",
            new { r.InspectionDate, r.Comments }, "Date", ct, duplicateMessage: "Add Failed: Date already blocked.");

    public Task UpdateUnavailableDayAsync(UnavailableDayRequest r, CancellationToken ct = default)
        => UpdateAsync($@"UPDATE [{_schema}].[INSPECTION_UNAVAILABLE_DAYS] SET COMMENTS = @Comments
WHERE INSPECTION_DATE = CONVERT(date, @InspectionDate, 101);",
            new { r.InspectionDate, r.Comments }, "Update Failed: Date does not exists.", ct);

    public Task DeleteUnavailableDayAsync(string inspectionDate, CancellationToken ct = default)
        => DeleteAsync($@"DELETE FROM [{_schema}].[INSPECTION_UNAVAILABLE_DAYS] WHERE INSPECTION_DATE = CONVERT(date, @InspectionDate, 101);",
            new { InspectionDate = inspectionDate }, "Delete Failed: Date does not exists.", ct);

    // ---------------------------------------------------------------- Inspection controls
    public async Task<InspectionControlDto?> GetInspectionControlAsync(CancellationToken ct = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<InspectionControlDto>(new CommandDefinition(
            $"SELECT ID AS Id, MAX_SLOTS_PER_DAY AS MaxSlotsPerDay FROM [{_schema}].[INSPECTION_CONTROLS] WHERE ID = 1;",
            cancellationToken: ct));
    }

    public async Task UpdateInspectionControlAsync(InspectionControlRequest r, CancellationToken ct = default)
    {
        // The legacy row is a singleton keyed on ID = 1; upsert so a fresh environment still works.
        var rows = await ExecuteAsync($@"UPDATE [{_schema}].[INSPECTION_CONTROLS] SET MAX_SLOTS_PER_DAY = @MaxSlotsPerDay WHERE ID = 1;",
            new { r.MaxSlotsPerDay }, null, ct);
        if (rows < 1)
        {
            await ExecuteAsync($@"INSERT INTO [{_schema}].[INSPECTION_CONTROLS] (ID, MAX_SLOTS_PER_DAY) VALUES (1, @MaxSlotsPerDay);",
                new { r.MaxSlotsPerDay }, null, ct);
        }
    }

    // ---------------------------------------------------------------- Shared helpers
    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        return rows.ToList();
    }

    private async Task AddAsync(string sql, object parameters, string entityLabel, CancellationToken ct, string? duplicateMessage = null)
    {
        try
        {
            await using var connection = await _context.CreateOpenConnectionAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number is DuplicateKeyError or DuplicateKeyError2)
        {
            throw new MaintenanceConflictException(duplicateMessage ?? $"Add Failed: {entityLabel} already exists, please try again.");
        }
        catch (SqlException ex) when (ex.Number == ForeignKeyError)
        {
            throw new MaintenanceConflictException("Add Failed: referenced code does not exist.");
        }
    }

    private async Task UpdateAsync(string sql, object parameters, string notFoundMessage, CancellationToken ct)
    {
        var rows = await ExecuteAsync(sql, parameters, null, ct);
        if (rows < 1)
        {
            throw new MaintenanceNotFoundException(notFoundMessage);
        }
    }

    private async Task DeleteAsync(string sql, object parameters, string notFoundMessage, CancellationToken ct)
    {
        var rows = await ExecuteAsync(sql, parameters, null, ct);
        if (rows < 1)
        {
            throw new MaintenanceNotFoundException(notFoundMessage);
        }
    }

    private async Task<int> ExecuteAsync(string sql, object parameters, string? entityLabel, CancellationToken ct)
    {
        try
        {
            await using var connection = await _context.CreateOpenConnectionAsync(ct);
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number == ForeignKeyError)
        {
            throw new MaintenanceConflictException("Cannot delete, code is in use");
        }
        catch (SqlException ex) when ((ex.Number is DuplicateKeyError or DuplicateKeyError2) && entityLabel is not null)
        {
            throw new MaintenanceConflictException($"Add Failed: {entityLabel} already exists, please try again.");
        }
    }
}
