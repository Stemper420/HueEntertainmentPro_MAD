using HueArtNet.Core.Profiles;
using HueArtNet.Persistence;
using HueArtNet.Persistence.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HueArtNet.Tests.Persistence;

public class ShowProfileRepositoryTests
{
  [Fact]
  public async Task SaveAsync_roundtrips_profile_and_protects_hue_credentials()
  {
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<HueArtNetDbContext>()
      .UseSqlite(connection)
      .Options;

    await using (var setupContext = new HueArtNetDbContext(options))
      await setupContext.Database.EnsureCreatedAsync();

    var profile = CreateProfile();
    await using (var saveContext = new HueArtNetDbContext(options))
    {
      var repository = new ShowProfileRepository(saveContext, new PrefixCredentialProtector());
      await repository.SaveAsync(profile, CancellationToken.None);
    }

    await using (var assertContext = new HueArtNetDbContext(options))
    {
      var storedMapping = await assertContext.HubMappings.SingleAsync();
      Assert.NotEqual("app-key", storedMapping.ProtectedApplicationKey);
      Assert.NotEqual("ent-key", storedMapping.ProtectedEntertainmentKey);
      Assert.StartsWith("protected:", storedMapping.ProtectedApplicationKey);
      Assert.StartsWith("protected:", storedMapping.ProtectedEntertainmentKey);
    }

    await using (var loadContext = new HueArtNetDbContext(options))
    {
      var repository = new ShowProfileRepository(loadContext, new PrefixCredentialProtector());
      var loaded = await repository.GetAsync(profile.Id, CancellationToken.None);

      Assert.NotNull(loaded);
      Assert.Equal(profile.Name, loaded.Name);
      Assert.Equal(FailSafeMode.Blackout, loaded.FailSafe.Mode);
      Assert.Equal("app-key", loaded.HubMappings.Single().ApplicationKey);
      Assert.Equal("ent-key", loaded.HubMappings.Single().EntertainmentKey);
      Assert.Equal("hue-bridge-id", loaded.HubMappings.Single().HueBridgeId);
      Assert.Equal(new[] { 3, 2, 1 }, loaded.HubMappings.Single().LightOrder);
    }
  }

  [Fact]
  public async Task SaveAsync_replaces_existing_mapping_rows()
  {
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<HueArtNetDbContext>()
      .UseSqlite(connection)
      .Options;

    await using (var setupContext = new HueArtNetDbContext(options))
      await setupContext.Database.EnsureCreatedAsync();

    var profile = CreateProfile();
    await using (var saveContext = new HueArtNetDbContext(options))
    {
      var repository = new ShowProfileRepository(saveContext, new PrefixCredentialProtector());
      await repository.SaveAsync(profile, CancellationToken.None);
      profile.HubMappings = new List<HubMapping>
      {
        CreateMapping("192.168.1.22", universe: 2)
      };
      await repository.SaveAsync(profile, CancellationToken.None);
    }

    await using var assertContext = new HueArtNetDbContext(options);
    Assert.Equal(1, await assertContext.HubMappings.CountAsync());
    Assert.Equal(2, (await assertContext.HubMappings.SingleAsync()).Universe);
  }

  [Fact]
  public async Task SaveAsync_does_not_return_stale_mappings_when_repository_reuses_one_context()
  {
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<HueArtNetDbContext>()
      .UseSqlite(connection)
      .Options;

    await using (var setupContext = new HueArtNetDbContext(options))
      await setupContext.Database.EnsureCreatedAsync();

    var profile = CreateProfile();
    await using var context = new HueArtNetDbContext(options);
    var repository = new ShowProfileRepository(context, new PrefixCredentialProtector());
    await repository.SaveAsync(profile, CancellationToken.None);
    var firstLoad = await repository.GetAsync(profile.Id, CancellationToken.None);
    Assert.NotNull(firstLoad);
    Assert.Equal(0, firstLoad.HubMappings.Single().Universe);

    profile.HubMappings = new List<HubMapping>
    {
      CreateMapping("192.168.1.22", universe: 2)
    };
    await repository.SaveAsync(profile, CancellationToken.None);
    var secondLoad = await repository.GetAsync(profile.Id, CancellationToken.None);

    Assert.NotNull(secondLoad);
    var mapping = Assert.Single(secondLoad.HubMappings);
    Assert.Equal(2, mapping.Universe);
    Assert.Equal("192.168.1.22", mapping.BridgeIp);
  }

  [Fact]
  public async Task EnsureCreatedAndMigratedAsync_adds_hue_bridge_id_column_to_existing_database()
  {
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    await using (var command = connection.CreateCommand())
    {
      command.CommandText = """
        CREATE TABLE ShowProfiles (
          Id TEXT NOT NULL CONSTRAINT PK_ShowProfiles PRIMARY KEY,
          Name TEXT NOT NULL,
          IsDefault INTEGER NOT NULL,
          ArtNetBindAddress TEXT NULL,
          ArtNetPort INTEGER NOT NULL,
          FailSafeMode TEXT NOT NULL,
          FailSafeTimeoutMilliseconds INTEGER NOT NULL,
          FramesPerSecond INTEGER NOT NULL,
          BrightnessLimit REAL NOT NULL
        );

        CREATE TABLE HubMappings (
          Id TEXT NOT NULL CONSTRAINT PK_HubMappings PRIMARY KEY,
          ShowProfileId TEXT NOT NULL,
          Name TEXT NOT NULL,
          BridgeId TEXT NOT NULL,
          BridgeIp TEXT NOT NULL,
          ProtectedApplicationKey TEXT NOT NULL,
          ProtectedEntertainmentKey TEXT NOT NULL,
          EntertainmentGroupId TEXT NOT NULL,
          Universe INTEGER NOT NULL,
          StartChannel INTEGER NOT NULL,
          FixtureMode TEXT NOT NULL,
          LightOrderJson TEXT NOT NULL,
          Enabled INTEGER NOT NULL,
          Required INTEGER NOT NULL,
          CONSTRAINT FK_HubMappings_ShowProfiles_ShowProfileId FOREIGN KEY (ShowProfileId) REFERENCES ShowProfiles (Id) ON DELETE CASCADE
        );
        """;
      await command.ExecuteNonQueryAsync();
    }

    var options = new DbContextOptionsBuilder<HueArtNetDbContext>()
      .UseSqlite(connection)
      .Options;

    await using var context = new HueArtNetDbContext(options);
    await HueArtNetDatabaseInitializer.EnsureCreatedAndMigratedAsync(context, CancellationToken.None);

    await using var assertCommand = connection.CreateCommand();
    assertCommand.CommandText = "SELECT HueBridgeId FROM HubMappings LIMIT 0;";
    await assertCommand.ExecuteNonQueryAsync();
  }

  private static ShowProfile CreateProfile()
  {
    return new ShowProfile
    {
      Name = "Stored profile",
      IsDefault = true,
      ArtNetInput = new ArtNetInputConfig { BindAddress = "0.0.0.0", Port = 6454 },
      FailSafe = new FailSafeConfig { Mode = FailSafeMode.Blackout, Timeout = TimeSpan.FromSeconds(3) },
      Output = new OutputConfig { FramesPerSecond = 20, BrightnessLimit = 0.8 },
      HubMappings = new List<HubMapping>
      {
        CreateMapping("192.168.1.20", universe: 0)
      }
    };
  }

  private static HubMapping CreateMapping(string bridgeIp, int universe)
  {
    return new HubMapping
    {
      Name = "Hub",
      BridgeId = Guid.NewGuid(),
      HueBridgeId = "hue-bridge-id",
      BridgeIp = bridgeIp,
      ApplicationKey = "app-key",
      EntertainmentKey = "ent-key",
      EntertainmentGroupId = Guid.NewGuid(),
      Universe = universe,
      StartChannel = 1,
      FixtureMode = FixtureMode.Rgb3,
      LightOrder = new List<int> { 3, 2, 1 },
      Enabled = true
    };
  }

  private sealed class PrefixCredentialProtector : ICredentialProtector
  {
    public string Protect(string value)
    {
      return $"protected:{value}";
    }

    public string Unprotect(string protectedValue)
    {
      return protectedValue["protected:".Length..];
    }
  }
}
