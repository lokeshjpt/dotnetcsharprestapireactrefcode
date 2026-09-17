using Dapper;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Persistence.Repositories;

public sealed class WorkPermitRepository : IWorkPermitRepository
{
    private readonly DapperContext _context;
    private readonly string _schema;

    public WorkPermitRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions)
    {
        _context = context;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<IReadOnlyList<WorkPermit>> GeneratePermitsAsync(string appId, DateTime permitExpiryDate, CancellationToken cancellationToken = default)
    {
        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var works = (await connection.QueryAsync<WorkRow>(new CommandDefinition($@"
SELECT w.work_id AS WorkId,
       (SELECT MIN(s.work_specs_id) FROM [{_schema}].[APP_WORK_SPECS] s
        WHERE s.application_id = w.application_id AND s.work_id = w.work_id) AS WorkSpecsId
FROM [{_schema}].[APP_WORKS] w
WHERE w.application_id = @AppId
ORDER BY w.work_id;", new { AppId = appId }, cancellationToken: cancellationToken))).ToList();

        if (works.Count == 0)
        {
            works.Add(new WorkRow { WorkId = 1, WorkSpecsId = 1 });
        }

        var suffix = appId.Length >= 7 ? appId[^7..] : appId.PadLeft(7, '0');
        var issued = DateTime.UtcNow;
        var permits = new List<WorkPermit>();

        for (var index = 0; index < works.Count; index++)
        {
            var permitNumber = $"{suffix}-{index + 1:00}";
            var permit = new WorkPermit
            {
                PermitNumber = permitNumber,
                AppId = appId,
                WorkId = works[index].WorkId,
                WorkSpecsId = works[index].WorkSpecsId ?? 1,
                PermitIssuedDate = issued,
                PermitExpireDate = permitExpiryDate,
                StatusCode = "POPEN"
            };

            var sql = $@"
INSERT INTO [{_schema}].[APP_WORK_PERMITS]
    (permit_number, application_id, work_id, work_specs_id, permit_issued_date, permit_expire_date, STATUS_CODE, add_by, add_ts)
VALUES
    (@PermitNumber, @AppId, @WorkId, @WorkSpecsId, @PermitIssuedDate, @PermitExpireDate, @StatusCode, @AddBy, @AddTs);";
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                permit.PermitNumber,
                permit.AppId,
                permit.WorkId,
                permit.WorkSpecsId,
                permit.PermitIssuedDate,
                permit.PermitExpireDate,
                permit.StatusCode,
                AddBy = "intra-approval",
                AddTs = issued
            }, cancellationToken: cancellationToken));
            permits.Add(permit);
        }

        return permits;
    }

    private sealed class WorkRow
    {
        public int WorkId { get; init; }
        public int? WorkSpecsId { get; init; }
    }
}
