using Microsoft.Data.SqlClient;

namespace Alisflyt.IntegrationTests;

internal static class TestDatabaseGuard
{
    public static void AssertNotDevelopmentDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.Equals(builder.InitialCatalog, "AlisflytDevelopment", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Integration tests must never use the AlisflytDevelopment database.");
    }
}
