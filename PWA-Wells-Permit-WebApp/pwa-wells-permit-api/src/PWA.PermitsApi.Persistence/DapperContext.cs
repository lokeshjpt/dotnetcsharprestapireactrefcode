using Microsoft.Data.SqlClient;

namespace PWA.PermitsApi.Persistence;

public sealed class DapperContext
{
    private readonly string _connectionString;

    public DapperContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqlConnection CreateConnection() => new(_connectionString);

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
