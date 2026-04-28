using System.Data;
using Microsoft.EntityFrameworkCore;

namespace HueArtNet.Persistence;

public static class HueArtNetDatabaseInitializer
{
  public static async Task EnsureCreatedAndMigratedAsync(HueArtNetDbContext dbContext, CancellationToken cancellationToken = default)
  {
    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    await EnsureColumnAsync(dbContext, "HubMappings", "HueBridgeId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
  }

  private static async Task EnsureColumnAsync(
    HueArtNetDbContext dbContext,
    string tableName,
    string columnName,
    string columnDefinition,
    CancellationToken cancellationToken)
  {
    var connection = dbContext.Database.GetDbConnection();
    bool shouldClose = connection.State == ConnectionState.Closed;
    if (shouldClose)
      await connection.OpenAsync(cancellationToken);

    try
    {
      await using var tableInfoCommand = connection.CreateCommand();
      tableInfoCommand.CommandText = $"PRAGMA table_info(\"{tableName}\");";
      await using (var reader = await tableInfoCommand.ExecuteReaderAsync(cancellationToken))
      {
        while (await reader.ReadAsync(cancellationToken))
        {
          if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            return;
        }
      }

      await using var alterCommand = connection.CreateCommand();
      alterCommand.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};";
      await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
    finally
    {
      if (shouldClose)
        await connection.CloseAsync();
    }
  }
}
