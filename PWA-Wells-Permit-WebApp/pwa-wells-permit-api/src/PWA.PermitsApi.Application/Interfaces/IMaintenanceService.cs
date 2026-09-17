using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

// "Code Maintenance" module: reference-table CRUD with the legacy validation/duplicate/in-use
// rules layered on top of the repository. Reads never throw (fall back to an empty list on a
// database outage); writes throw MaintenanceValidation/Conflict/NotFound exceptions.
public interface IMaintenanceService
{
    // Cities
    Task<IReadOnlyList<CityCodeDto>> GetCitiesAsync(CancellationToken ct = default);
    Task AddCityAsync(CityCodeRequest request, CancellationToken ct = default);
    Task UpdateCityAsync(CityCodeRequest request, CancellationToken ct = default);
    Task DeleteCityAsync(string cityCode, CancellationToken ct = default);

    // States
    Task<IReadOnlyList<StateCodeDto>> GetStatesAsync(CancellationToken ct = default);
    Task AddStateAsync(StateCodeRequest request, CancellationToken ct = default);
    Task UpdateStateAsync(StateCodeRequest request, CancellationToken ct = default);
    Task DeleteStateAsync(string stateCode, CancellationToken ct = default);

    // Work categories
    Task<IReadOnlyList<WorkCategoryRowDto>> GetWorkCategoriesAsync(CancellationToken ct = default);
    Task AddWorkCategoryAsync(WorkCategoryRequest request, CancellationToken ct = default);
    Task UpdateWorkCategoryAsync(WorkCategoryRequest request, CancellationToken ct = default);
    Task DeleteWorkCategoryAsync(string workCategory, CancellationToken ct = default);

    // Work types
    Task<IReadOnlyList<WorkTypeRowDto>> GetWorkTypesAsync(CancellationToken ct = default);
    Task AddWorkTypeAsync(WorkTypeRequest request, CancellationToken ct = default);
    Task UpdateWorkTypeAsync(WorkTypeRequest request, CancellationToken ct = default);
    Task DeleteWorkTypeAsync(string workCategory, string workType, CancellationToken ct = default);

    // Well use types
    Task<IReadOnlyList<WellUseTypeRowDto>> GetWellUseTypesAsync(CancellationToken ct = default);
    Task AddWellUseTypeAsync(WellUseTypeRequest request, CancellationToken ct = default);
    Task UpdateWellUseTypeAsync(WellUseTypeRequest request, CancellationToken ct = default);
    Task DeleteWellUseTypeAsync(string workCategory, string workType, string wellUseType, CancellationToken ct = default);

    // Drill methods
    Task<IReadOnlyList<DrillMethodRowDto>> GetDrillMethodsAsync(CancellationToken ct = default);
    Task AddDrillMethodAsync(DrillMethodRequest request, CancellationToken ct = default);
    Task UpdateDrillMethodAsync(DrillMethodRequest request, CancellationToken ct = default);
    Task DeleteDrillMethodAsync(string drillMethodType, CancellationToken ct = default);

    // Condition types
    Task<IReadOnlyList<ConditionTypeRowDto>> GetConditionTypesAsync(CancellationToken ct = default);
    Task AddConditionTypeAsync(ConditionTypeRequest request, CancellationToken ct = default);
    Task UpdateConditionTypeAsync(ConditionTypeRequest request, CancellationToken ct = default);
    Task DeleteConditionTypeAsync(string conditionType, CancellationToken ct = default);

    // Work condition types
    Task<IReadOnlyList<WorkConditionTypeRowDto>> GetWorkConditionTypesAsync(CancellationToken ct = default);
    Task AddWorkConditionTypeAsync(WorkConditionTypeRequest request, CancellationToken ct = default);
    Task DeleteWorkConditionTypeAsync(string workCategory, string workType, string conditionType, CancellationToken ct = default);

    // Payment types
    Task<IReadOnlyList<PaymentTypeRowDto>> GetPaymentTypesAsync(CancellationToken ct = default);
    Task AddPaymentTypeAsync(PaymentTypeRequest request, CancellationToken ct = default);
    Task UpdatePaymentTypeAsync(PaymentTypeRequest request, CancellationToken ct = default);
    Task DeletePaymentTypeAsync(string paymentType, CancellationToken ct = default);

    // Status codes
    Task<IReadOnlyList<StatusCodeRowDto>> GetStatusCodesAsync(CancellationToken ct = default);
    Task AddStatusCodeAsync(StatusCodeRequest request, CancellationToken ct = default);
    Task UpdateStatusCodeAsync(StatusCodeRequest request, CancellationToken ct = default);
    Task DeleteStatusCodeAsync(string statusCode, CancellationToken ct = default);

    // Document types
    Task<IReadOnlyList<DocumentTypeRowDto>> GetDocumentTypesAsync(CancellationToken ct = default);
    Task AddDocumentTypeAsync(DocumentTypeRequest request, CancellationToken ct = default);
    Task UpdateDocumentTypeAsync(DocumentTypeRequest request, CancellationToken ct = default);
    Task DeleteDocumentTypeAsync(string documentType, CancellationToken ct = default);

    // Inspectors
    Task<IReadOnlyList<InspectorRowDto>> GetInspectorsAsync(CancellationToken ct = default);
    Task<int> AddInspectorAsync(InspectorRequest request, string actingUser, CancellationToken ct = default);
    Task UpdateInspectorAsync(InspectorRequest request, string actingUser, CancellationToken ct = default);

    // Inspection unavailable days
    Task<IReadOnlyList<UnavailableDayDto>> GetUnavailableDaysAsync(CancellationToken ct = default);
    Task AddUnavailableDayAsync(UnavailableDayRequest request, CancellationToken ct = default);
    Task UpdateUnavailableDayAsync(UnavailableDayRequest request, CancellationToken ct = default);
    Task DeleteUnavailableDayAsync(string inspectionDate, CancellationToken ct = default);

    // Inspection controls (max slots per day)
    Task<InspectionControlDto?> GetInspectionControlAsync(CancellationToken ct = default);
    Task UpdateInspectionControlAsync(InspectionControlRequest request, CancellationToken ct = default);
}
