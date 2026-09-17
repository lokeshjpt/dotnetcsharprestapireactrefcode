using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Exceptions;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace PWA.PermitsApi.Application.Services;

// "Code Maintenance" service. Reads fall back to an empty list when the database is unreachable
// (parity with the read-only ReferenceService). Writes apply the legacy validate()/default rules,
// normalise Y/N flags, then delegate to the repository (which owns duplicate / in-use / not-found).
public sealed class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository _repository;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(IMaintenanceRepository repository, IPermitNotificationService notifications, ILogger<MaintenanceService> logger)
    {
        _repository = repository;
        _notifications = notifications;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Cities
    public Task<IReadOnlyList<CityCodeDto>> GetCitiesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetCitiesAsync(ct));

    public Task AddCityAsync(CityCodeRequest r, CancellationToken ct = default)
    {
        var code = Required(r.CityCode, "City Code");
        var name = Required(r.CityName, "City Name");
        return _repository.AddCityAsync(new CityCodeRequest(code, name, Trim(r.CountyJuris)), ct);
    }

    public Task UpdateCityAsync(CityCodeRequest r, CancellationToken ct = default)
    {
        var code = Required(r.CityCode, "City Code");
        var name = Required(r.CityName, "City Name");
        return _repository.UpdateCityAsync(new CityCodeRequest(code, name, Trim(r.CountyJuris)), ct);
    }

    public Task DeleteCityAsync(string cityCode, CancellationToken ct = default)
        => _repository.DeleteCityAsync(Required(cityCode, "City Code"), ct);

    // ----------------------------------------------------------------- States
    public Task<IReadOnlyList<StateCodeDto>> GetStatesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetStatesAsync(ct));

    public Task AddStateAsync(StateCodeRequest r, CancellationToken ct = default)
        => _repository.AddStateAsync(new StateCodeRequest(Required(r.StateCode, "State Code"), Required(r.StateName, "State Name")), ct);

    public Task UpdateStateAsync(StateCodeRequest r, CancellationToken ct = default)
        => _repository.UpdateStateAsync(new StateCodeRequest(Required(r.StateCode, "State Code"), Required(r.StateName, "State Name")), ct);

    public Task DeleteStateAsync(string stateCode, CancellationToken ct = default)
        => _repository.DeleteStateAsync(Required(stateCode, "State Code"), ct);

    // ----------------------------------------------------------------- Work categories
    public Task<IReadOnlyList<WorkCategoryRowDto>> GetWorkCategoriesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetWorkCategoriesAsync(ct));

    public Task AddWorkCategoryAsync(WorkCategoryRequest r, CancellationToken ct = default)
        => _repository.AddWorkCategoryAsync(new WorkCategoryRequest(
            Required(r.WorkCategory, "Work Category"), Required(r.WorkCatDesc, "Work Category Description"), Flag(r.ActiveFlag, "Y")), ct);

    public Task UpdateWorkCategoryAsync(WorkCategoryRequest r, CancellationToken ct = default)
        => _repository.UpdateWorkCategoryAsync(new WorkCategoryRequest(
            Required(r.WorkCategory, "Work Category"), Required(r.WorkCatDesc, "Work Category Description"), Flag(r.ActiveFlag, "Y")), ct);

    public Task DeleteWorkCategoryAsync(string workCategory, CancellationToken ct = default)
        => _repository.DeleteWorkCategoryAsync(Required(workCategory, "Work Category"), ct);

    // ----------------------------------------------------------------- Work types
    public Task<IReadOnlyList<WorkTypeRowDto>> GetWorkTypesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetWorkTypesAsync(ct));

    public Task AddWorkTypeAsync(WorkTypeRequest r, CancellationToken ct = default)
        => _repository.AddWorkTypeAsync(ValidateWorkType(r), ct);

    public Task UpdateWorkTypeAsync(WorkTypeRequest r, CancellationToken ct = default)
        => _repository.UpdateWorkTypeAsync(ValidateWorkType(r), ct);

    public Task DeleteWorkTypeAsync(string workCategory, string workType, CancellationToken ct = default)
        => _repository.DeleteWorkTypeAsync(Required(workCategory, "Work Category"), Required(workType, "Work Type"), ct);

    private static WorkTypeRequest ValidateWorkType(WorkTypeRequest r)
    {
        var errors = new List<string>();
        var cat = Collect(errors, r.WorkCategory, "Work Category");
        var type = Collect(errors, r.WorkType, "Work Type");
        var desc = Collect(errors, r.WorkDesc, "Work Description");
        var feeUnit = Collect(errors, r.FeeUnit, "Fee Unit");
        if (r.FeeRateAmt is null)
        {
            errors.Add("Fee Rate Amount is a required field.");
        }
        var dwr = Flag(r.DwrRequired, "N");
        var geolog = Flag(r.GeologRequired, "N");
        if (dwr == "Y" && geolog == "Y")
        {
            errors.Add("A Work Type cannot require both a DWR and a Geolog.");
        }
        ThrowIfInvalid(errors);
        // site_max <= 0 is stored as NULL (parity with the legacy update path).
        var siteMax = r.SiteMax is > 0 ? r.SiteMax : null;
        return new WorkTypeRequest(cat, type, desc, r.FeeRateAmt, feeUnit, siteMax, dwr, geolog, Flag(r.ActiveFlag, "Y"));
    }

    // ----------------------------------------------------------------- Well use types
    public Task<IReadOnlyList<WellUseTypeRowDto>> GetWellUseTypesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetWellUseTypesAsync(ct));

    public Task AddWellUseTypeAsync(WellUseTypeRequest r, CancellationToken ct = default)
        => _repository.AddWellUseTypeAsync(ValidateWellUse(r), ct);

    public Task UpdateWellUseTypeAsync(WellUseTypeRequest r, CancellationToken ct = default)
        => _repository.UpdateWellUseTypeAsync(ValidateWellUse(r), ct);

    public Task DeleteWellUseTypeAsync(string workCategory, string workType, string wellUseType, CancellationToken ct = default)
        => _repository.DeleteWellUseTypeAsync(
            Required(workCategory, "Work Category"), Required(workType, "Work Type"), Required(wellUseType, "Well Use Type"), ct);

    private static WellUseTypeRequest ValidateWellUse(WellUseTypeRequest r)
    {
        var errors = new List<string>();
        var cat = Collect(errors, r.WorkCategory, "Work Category");
        var type = Collect(errors, r.WorkType, "Work Type");
        var use = Collect(errors, r.WellUseType, "Well Use Type");
        var desc = Collect(errors, r.WellUseDesc, "Well Use Description");
        ThrowIfInvalid(errors);
        return new WellUseTypeRequest(cat, type, use, desc, Flag(r.ActiveFlag, "Y"));
    }

    // ----------------------------------------------------------------- Drill methods
    public Task<IReadOnlyList<DrillMethodRowDto>> GetDrillMethodsAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetDrillMethodsAsync(ct));

    public Task AddDrillMethodAsync(DrillMethodRequest r, CancellationToken ct = default)
        => _repository.AddDrillMethodAsync(new DrillMethodRequest(
            Required(r.DrillMethodType, "Drill Method Type"), Required(r.DrillMethodName, "Drill Method Name"), Flag(r.ActiveFlag, "Y")), ct);

    public Task UpdateDrillMethodAsync(DrillMethodRequest r, CancellationToken ct = default)
        => _repository.UpdateDrillMethodAsync(new DrillMethodRequest(
            Required(r.DrillMethodType, "Drill Method Type"), Required(r.DrillMethodName, "Drill Method Name"), Flag(r.ActiveFlag, "Y")), ct);

    public Task DeleteDrillMethodAsync(string drillMethodType, CancellationToken ct = default)
        => _repository.DeleteDrillMethodAsync(Required(drillMethodType, "Drill Method Type"), ct);

    // ----------------------------------------------------------------- Condition types
    public Task<IReadOnlyList<ConditionTypeRowDto>> GetConditionTypesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetConditionTypesAsync(ct));

    public Task AddConditionTypeAsync(ConditionTypeRequest r, CancellationToken ct = default)
        => _repository.AddConditionTypeAsync(new ConditionTypeRequest(
            Required(r.ConditionType, "Condition Type"), Required(r.ConditionDesc, "Condition Description"), Flag(r.ActiveFlag, "Y")), ct);

    public Task UpdateConditionTypeAsync(ConditionTypeRequest r, CancellationToken ct = default)
        => _repository.UpdateConditionTypeAsync(new ConditionTypeRequest(
            Required(r.ConditionType, "Condition Type"), Required(r.ConditionDesc, "Condition Description"), Flag(r.ActiveFlag, "Y")), ct);

    public Task DeleteConditionTypeAsync(string conditionType, CancellationToken ct = default)
        => _repository.DeleteConditionTypeAsync(Required(conditionType, "Condition Type"), ct);

    // ----------------------------------------------------------------- Work condition types
    public Task<IReadOnlyList<WorkConditionTypeRowDto>> GetWorkConditionTypesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetWorkConditionTypesAsync(ct));

    public Task AddWorkConditionTypeAsync(WorkConditionTypeRequest r, CancellationToken ct = default)
        => _repository.AddWorkConditionTypeAsync(new WorkConditionTypeRequest(
            Required(r.WorkCategory, "Work Category"), Required(r.WorkType, "Work Type"), Required(r.ConditionType, "Condition Type")), ct);

    public Task DeleteWorkConditionTypeAsync(string workCategory, string workType, string conditionType, CancellationToken ct = default)
        => _repository.DeleteWorkConditionTypeAsync(
            Required(workCategory, "Work Category"), Required(workType, "Work Type"), Required(conditionType, "Condition Type"), ct);

    // ----------------------------------------------------------------- Payment types
    public Task<IReadOnlyList<PaymentTypeRowDto>> GetPaymentTypesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetPaymentTypesAsync(ct));

    public Task AddPaymentTypeAsync(PaymentTypeRequest r, CancellationToken ct = default)
        => _repository.AddPaymentTypeAsync(ValidatePaymentType(r), ct);

    public Task UpdatePaymentTypeAsync(PaymentTypeRequest r, CancellationToken ct = default)
        => _repository.UpdatePaymentTypeAsync(ValidatePaymentType(r), ct);

    public Task DeletePaymentTypeAsync(string paymentType, CancellationToken ct = default)
        => _repository.DeletePaymentTypeAsync(Required(paymentType, "Payment Type"), ct);

    private static PaymentTypeRequest ValidatePaymentType(PaymentTypeRequest r)
    {
        var errors = new List<string>();
        var type = Collect(errors, r.PaymentType, "Payment Type");
        var desc = Collect(errors, r.PaymentDesc, "Payment Description");
        ThrowIfInvalid(errors);
        // Legacy servlet coerces a blank service charge / display sequence to 0.
        return new PaymentTypeRequest(type, desc, r.ServiceCharge ?? 0m, r.DisplaySeq ?? 0);
    }

    // ----------------------------------------------------------------- Status codes
    public Task<IReadOnlyList<StatusCodeRowDto>> GetStatusCodesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetStatusCodesAsync(ct));

    public Task AddStatusCodeAsync(StatusCodeRequest r, CancellationToken ct = default)
        => _repository.AddStatusCodeAsync(ValidateStatus(r), ct);

    public Task UpdateStatusCodeAsync(StatusCodeRequest r, CancellationToken ct = default)
        => _repository.UpdateStatusCodeAsync(ValidateStatus(r), ct);

    public Task DeleteStatusCodeAsync(string statusCode, CancellationToken ct = default)
        => _repository.DeleteStatusCodeAsync(Required(statusCode, "Status Code"), ct);

    private static StatusCodeRequest ValidateStatus(StatusCodeRequest r)
    {
        var errors = new List<string>();
        var code = Collect(errors, r.StatusCode, "Status Code");
        var desc = Collect(errors, r.StatusDesc, "Status Description");
        if (r.SortSeq is null)
        {
            errors.Add("Display Sequence is a required field.");
        }
        ThrowIfInvalid(errors);
        return new StatusCodeRequest(code, desc, r.SortSeq);
    }

    // ----------------------------------------------------------------- Document types
    public Task<IReadOnlyList<DocumentTypeRowDto>> GetDocumentTypesAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetDocumentTypesAsync(ct));

    public Task AddDocumentTypeAsync(DocumentTypeRequest r, CancellationToken ct = default)
        => _repository.AddDocumentTypeAsync(new DocumentTypeRequest(
            Required(r.DocumentType, "Document Type"), Required(r.DocumentDesc, "Document Description")), ct);

    public Task UpdateDocumentTypeAsync(DocumentTypeRequest r, CancellationToken ct = default)
        => _repository.UpdateDocumentTypeAsync(new DocumentTypeRequest(
            Required(r.DocumentType, "Document Type"), Required(r.DocumentDesc, "Document Description")), ct);

    public Task DeleteDocumentTypeAsync(string documentType, CancellationToken ct = default)
        => _repository.DeleteDocumentTypeAsync(Required(documentType, "Document Type"), ct);

    // ----------------------------------------------------------------- Inspectors
    public Task<IReadOnlyList<InspectorRowDto>> GetInspectorsAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetInspectorsAsync(ct));

    public Task<int> AddInspectorAsync(InspectorRequest r, string actingUser, CancellationToken ct = default)
        => _repository.AddInspectorAsync(ValidateInspector(r, requireId: false), actingUser, ct);

    public Task UpdateInspectorAsync(InspectorRequest r, string actingUser, CancellationToken ct = default)
        => _repository.UpdateInspectorAsync(ValidateInspector(r, requireId: true), actingUser, ct);

    private static InspectorRequest ValidateInspector(InspectorRequest r, bool requireId)
    {
        var errors = new List<string>();
        var name = Collect(errors, r.InspectorName, "Inspector Name");
        var digits = new string((r.InspectorPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            errors.Add("Inspector Phone Number is a required field.");
        }
        else if (digits.Length < 10)
        {
            errors.Add("Inspector Phone Number must contain at least 10 digits.");
        }
        if (requireId && r.InspectorId is null)
        {
            errors.Add("Inspector Id is a required field.");
        }
        var email = Trim(r.InspectorEmail);
        if (!string.IsNullOrEmpty(email) && !(email.Contains('@') && email.Contains('.')))
        {
            errors.Add("Inspector Email is not a valid email address.");
        }
        ThrowIfInvalid(errors);
        return new InspectorRequest(r.InspectorId, name, string.IsNullOrEmpty(email) ? null : email,
            digits.Length == 0 ? null : digits, Flag(r.ActiveFlag, "Y"));
    }

    // ----------------------------------------------------------------- Inspection unavailable days
    public Task<IReadOnlyList<UnavailableDayDto>> GetUnavailableDaysAsync(CancellationToken ct = default)
        => SafeList(() => _repository.GetUnavailableDaysAsync(ct));

    public Task AddUnavailableDayAsync(UnavailableDayRequest r, CancellationToken ct = default)
        => _repository.AddUnavailableDayAsync(new UnavailableDayRequest(
            Required(r.InspectionDate, "Inspection Date"), Required(r.Comments, "Comments")), ct);

    public Task UpdateUnavailableDayAsync(UnavailableDayRequest r, CancellationToken ct = default)
        => _repository.UpdateUnavailableDayAsync(new UnavailableDayRequest(
            Required(r.InspectionDate, "Inspection Date"), Required(r.Comments, "Comments")), ct);

    public Task DeleteUnavailableDayAsync(string inspectionDate, CancellationToken ct = default)
        => _repository.DeleteUnavailableDayAsync(Required(inspectionDate, "Inspection Date"), ct);

    // ----------------------------------------------------------------- Inspection controls
    public Task<InspectionControlDto?> GetInspectionControlAsync(CancellationToken ct = default)
        => _repository.GetInspectionControlAsync(ct);

    public Task UpdateInspectionControlAsync(InspectionControlRequest r, CancellationToken ct = default)
    {
        if (r.MaxSlotsPerDay is null or 0)
        {
            throw new MaintenanceValidationException(new[] { "Maximum Slots Per Day is a required field and cannot be zero." });
        }
        return _repository.UpdateInspectionControlAsync(r, ct);
    }

    // ----------------------------------------------------------------- Helpers
    private async Task<IReadOnlyList<T>> SafeList<T>(Func<Task<IReadOnlyList<T>>> query)
    {
        try
        {
            return await query();
        }
        catch (MaintenanceConflictException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A read/lookup failed (e.g. DB unreachable). Degrade to an empty list so the maintenance
            // screen still renders, but audit-notify staff since the failure is otherwise silent.
            _logger.LogWarning(ex, "Maintenance list query for {Type} failed; returning empty list.", typeof(T).Name);
            await _notifications.SendSystemExceptionAuditAsync("MaintenanceService.SafeList", "Query for " + typeof(T).Name, ex, CancellationToken.None);
            return Array.Empty<T>();
        }
    }

    private static string Trim(string? value) => (value ?? string.Empty).Trim();

    private static string Required(string? value, string field)
    {
        var trimmed = Trim(value);
        if (trimmed.Length == 0)
        {
            throw new MaintenanceValidationException(new[] { $"{field} is a required field." });
        }
        return trimmed;
    }

    // Collect a required value into an error list without throwing immediately so a form can report
    // every missing field at once (parity with the legacy validate() aggregation).
    private static string Collect(List<string> errors, string? value, string field)
    {
        var trimmed = Trim(value);
        if (trimmed.Length == 0)
        {
            errors.Add($"{field} is a required field.");
        }
        return trimmed;
    }

    private static void ThrowIfInvalid(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new MaintenanceValidationException(errors);
        }
    }

    private static string Flag(string? value, string fallback)
    {
        var trimmed = Trim(value);
        if (trimmed.Length == 0)
        {
            return fallback;
        }
        return trimmed.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)
            || trimmed == "1"
            ? "Y" : "N";
    }
}
