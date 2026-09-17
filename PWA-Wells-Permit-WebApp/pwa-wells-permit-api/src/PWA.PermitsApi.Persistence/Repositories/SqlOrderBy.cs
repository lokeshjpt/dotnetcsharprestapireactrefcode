namespace PWA.PermitsApi.Persistence.Repositories;

/// <summary>
/// Helpers for composing safe ORDER BY clauses for server-side paged searches.
/// </summary>
internal static class SqlOrderBy
{
    /// <summary>
    /// Builds an ORDER BY body that ends with a unique tiebreaker column so OFFSET/FETCH paging stays
    /// deterministic when the primary sort value ties. The tiebreaker is omitted when it is the same
    /// column as the primary sort, because SQL Server rejects a column appearing more than once in the
    /// ORDER BY list (error 169).
    /// </summary>
    public static string WithTiebreaker(string sortColumn, string direction, string tiebreaker)
    {
        return string.Equals(sortColumn, tiebreaker, StringComparison.OrdinalIgnoreCase)
            ? $"{sortColumn} {direction}"
            : $"{sortColumn} {direction}, {tiebreaker} {direction}";
    }
}
