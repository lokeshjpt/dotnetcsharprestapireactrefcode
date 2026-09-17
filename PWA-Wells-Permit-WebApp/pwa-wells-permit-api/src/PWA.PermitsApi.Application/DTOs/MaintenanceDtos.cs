namespace PWA.PermitsApi.Application.DTOs;

// Reference-data ("Code Maintenance") DTOs. These mirror the legacy intra MaintCodeServlet /
// maint_*_list.jsp screens and the underlying EEAOWN lookup tables. List records are the row
// shape returned to the grid; *Request records are the add/update payloads.

// --- City Codes (city_codes) ---
public sealed record CityCodeDto(string CityCode, string? CityName, string? CountyJuris);
public sealed record CityCodeRequest(string? CityCode, string? CityName, string? CountyJuris);

// --- State Codes (state_codes) ---
public sealed record StateCodeDto(string StateCode, string? StateName);
public sealed record StateCodeRequest(string? StateCode, string? StateName);

// --- Work Categories (work_categories) ---
public sealed record WorkCategoryRowDto(string WorkCategory, string? WorkCatDesc, string? ActiveFlag);
public sealed record WorkCategoryRequest(string? WorkCategory, string? WorkCatDesc, string? ActiveFlag);

// --- Work Types (work_types) ---
public sealed record WorkTypeRowDto(
    string WorkCategory,
    string? WorkCatDesc,
    string WorkType,
    string? WorkDesc,
    decimal? FeeRateAmt,
    string? FeeUnit,
    int? SiteMax,
    string? DwrRequired,
    string? GeologRequired,
    string? ActiveFlag);

public sealed record WorkTypeRequest(
    string? WorkCategory,
    string? WorkType,
    string? WorkDesc,
    decimal? FeeRateAmt,
    string? FeeUnit,
    int? SiteMax,
    string? DwrRequired,
    string? GeologRequired,
    string? ActiveFlag);

// --- Well Use Types (work_use_types) ---
public sealed record WellUseTypeRowDto(
    string WorkCategory,
    string? WorkCatDesc,
    string WorkType,
    string? WorkDesc,
    string WellUseType,
    string? WellUseDesc,
    string? ActiveFlag);

public sealed record WellUseTypeRequest(
    string? WorkCategory,
    string? WorkType,
    string? WellUseType,
    string? WellUseDesc,
    string? ActiveFlag);

// --- Drill Method Types (drill_method_types) ---
public sealed record DrillMethodRowDto(string DrillMethodType, string? DrillMethodName, string? ActiveFlag);
public sealed record DrillMethodRequest(string? DrillMethodType, string? DrillMethodName, string? ActiveFlag);

// --- Condition Types (condition_types) ---
public sealed record ConditionTypeRowDto(string ConditionType, string? ConditionDesc, string? ActiveFlag);
public sealed record ConditionTypeRequest(string? ConditionType, string? ConditionDesc, string? ActiveFlag);

// --- Work Condition Types (work_condition_types) ---
public sealed record WorkConditionTypeRowDto(
    string WorkCategory,
    string? WorkCatDesc,
    string WorkType,
    string? WorkDesc,
    string ConditionType,
    string? ConditionDesc);

public sealed record WorkConditionTypeRequest(string? WorkCategory, string? WorkType, string? ConditionType);

// --- Payment Types (payment_types) ---
public sealed record PaymentTypeRowDto(string PaymentType, string? PaymentDesc, decimal? ServiceCharge, int? DisplaySeq);
public sealed record PaymentTypeRequest(string? PaymentType, string? PaymentDesc, decimal? ServiceCharge, int? DisplaySeq);

// --- Status Codes (status_codes) ---
public sealed record StatusCodeRowDto(string StatusCode, string? StatusDesc, int? SortSeq);
public sealed record StatusCodeRequest(string? StatusCode, string? StatusDesc, int? SortSeq);

// --- Document Types (DOCUMENT_TYPES) ---
public sealed record DocumentTypeRowDto(string DocumentType, string? DocumentDesc);
public sealed record DocumentTypeRequest(string? DocumentType, string? DocumentDesc);

// --- Inspectors (INSPECTORS) ---
public sealed record InspectorRowDto(
    int InspectorId,
    string? InspectorName,
    string? InspectorEmail,
    string? InspectorPhone,
    string? ActiveFlag,
    string? AddBy,
    DateTime? AddTs,
    string? UpdateBy,
    DateTime? UpdateTs);

public sealed record InspectorRequest(
    int? InspectorId,
    string? InspectorName,
    string? InspectorEmail,
    string? InspectorPhone,
    string? ActiveFlag);

// --- Inspection Unavailable Days (INSPECTION_UNAVAILABLE_DAYS) ---
public sealed record UnavailableDayDto(string InspectionDate, string? Comments);
public sealed record UnavailableDayRequest(string? InspectionDate, string? Comments);

// --- Inspection Controls / Max Slots Per Day (INSPECTION_CONTROLS) ---
public sealed record InspectionControlDto(int Id, int? MaxSlotsPerDay);
public sealed record InspectionControlRequest(int? MaxSlotsPerDay);
