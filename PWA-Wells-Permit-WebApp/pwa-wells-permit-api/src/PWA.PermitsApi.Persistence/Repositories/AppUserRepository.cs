using Dapper;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Persistence.Repositories;

public sealed class AppUserRepository : IAppUserRepository
{
    private readonly DapperContext _context;
    private readonly string _schema;

    public AppUserRepository(DapperContext context, IOptions<DatabaseOptions> databaseOptions)
    {
        _context = context;
        _schema = string.IsNullOrWhiteSpace(databaseOptions.Value.DbSchema) ? "EEAOWN" : databaseOptions.Value.DbSchema;
    }

    public async Task<IReadOnlyCollection<string>> GetActiveEmailsAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT LTRIM(RTRIM(email_id))
FROM [{_schema}].[app_users]
WHERE active = 1;";

        await using var connection = await _context.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
