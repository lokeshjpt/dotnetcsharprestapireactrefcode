using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

/// <summary>
/// Minimal read/insert access to the X_INSPECTION_WORKBOOK* tables. These are filled in by the
/// intra app during a field inspection; the ecomm rewrite maps them so the schema is covered.
/// </summary>
public interface IInspectionWorkbookRepository
{
    Task<InspectionWorkbook?> GetByApplicationAsync(string applicationId, CancellationToken cancellationToken = default);
    Task CreateAsync(InspectionWorkbook workbook, CancellationToken cancellationToken = default);
}
