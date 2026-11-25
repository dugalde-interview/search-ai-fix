using Microsoft.Data.Sqlite;

namespace Claims;

public class ClaimsRepository
{
    private readonly SqliteConnection _db;

    public ClaimsRepository(SqliteConnection db) => _db = db;

    // MYT-3108: ORDER BY cannot be parameterized, so only these fixed SQL fragments ever reach the query.
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["date"] = "submitted_at",
        ["amount"] = "amount",
        ["status"] = "status",
    };

    private static readonly Dictionary<string, string> SortDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        ["asc"] = "ASC",
        ["desc"] = "DESC",
    };

    // Search the claims of one member. Called by the claims page of the member website and by the support tool.
    public IReadOnlyList<Claim> Search(int memberId, string? text, string? sortBy = "date", string? sortDir = "desc", int page = 0)
    {
        if (!SortColumns.TryGetValue(sortBy ?? "date", out var column))
            throw new ArgumentException("sortBy must be one of: date, amount, status.", nameof(sortBy));
        if (!SortDirections.TryGetValue(sortDir ?? "desc", out var direction))
            throw new ArgumentException("sortDir must be asc or desc.", nameof(sortDir));

        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "SELECT id, provider, description, amount, status, submitted_at FROM claims " +
            "WHERE member_id = @member AND (description LIKE @text ESCAPE '!' OR provider LIKE @text ESCAPE '!') " +
            $"ORDER BY {column} {direction} LIMIT 20 OFFSET @offset";
        cmd.Parameters.AddWithValue("@member", memberId);
        cmd.Parameters.AddWithValue("@text", ContainsPattern(text));
        cmd.Parameters.AddWithValue("@offset", page * 20);

        var results = new List<Claim>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(new Claim(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                                  reader.GetDouble(3), reader.GetString(4), reader.GetString(5)));
        return results;
    }

    // Turns user text into a LIKE "contains" pattern where every character is literal (used with ESCAPE '!').
    private static string ContainsPattern(string? text)
    {
        var escaped = (text ?? "")
            .Replace("!", "!!")    // escape the escape character first
            .Replace("%", "!%")
            .Replace("_", "!_")
            .Replace("[", "![");   // a wildcard in SQL Server (production)
        return "%" + escaped + "%";
    }
}
