using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AAT.Infrastructure;

internal static class PostgresErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
}
