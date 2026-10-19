using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Encurtador.Infrastructure.Persistence;

// Único lugar que conhece o código de erro de violação de unicidade de cada provider.
// PostgreSQL (23505) ainda não implementado, ver plan.md, "Pendências para o Provider PostgreSQL".
public static class UniqueConstraintDetector
{
    // SQLITE_CONSTRAINT_UNIQUE
    private const int SqliteUniqueConstraintCode = 2067;

    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintCode };
}
