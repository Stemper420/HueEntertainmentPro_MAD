using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HueEntertainmentPro.Database.Migrations
{
  /// <inheritdoc />
  public partial class AddArtNetSettings : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<bool>(
          name: "ArtNetEnabled",
          table: "ProAreas",
          type: "INTEGER",
          nullable: false,
          defaultValue: false);

      migrationBuilder.AddColumn<string>(
          name: "ArtNetBindAddress",
          table: "ProAreas",
          type: "TEXT",
          nullable: true);

      migrationBuilder.AddColumn<string>(
          name: "ArtNetTimeoutMode",
          table: "ProAreas",
          type: "TEXT",
          nullable: false,
          defaultValue: "Blackout");

      migrationBuilder.AddColumn<int>(
          name: "ArtNetUniverse",
          table: "ProAreaGroups",
          type: "INTEGER",
          nullable: false,
          defaultValue: 0);

      migrationBuilder.AddColumn<int>(
          name: "ArtNetStartChannel",
          table: "ProAreaGroups",
          type: "INTEGER",
          nullable: false,
          defaultValue: 1);

      migrationBuilder.AddColumn<string>(
          name: "ArtNetFixtureMode",
          table: "ProAreaGroups",
          type: "TEXT",
          nullable: false,
          defaultValue: "Rgb3");

      migrationBuilder.AddColumn<string>(
          name: "ArtNetLightOrderJson",
          table: "ProAreaGroups",
          type: "TEXT",
          nullable: true);

      migrationBuilder.Sql(
        @"UPDATE ProAreaGroups
          SET ArtNetUniverse = (
            SELECT COUNT(*)
            FROM ProAreaGroups AS previous
            WHERE previous.ProAreaId = ProAreaGroups.ProAreaId
              AND (
                previous.CreatedDate < ProAreaGroups.CreatedDate
                OR (previous.CreatedDate = ProAreaGroups.CreatedDate AND previous.Id <= ProAreaGroups.Id)
              )
          ) - 1;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(
          name: "ArtNetEnabled",
          table: "ProAreas");

      migrationBuilder.DropColumn(
          name: "ArtNetBindAddress",
          table: "ProAreas");

      migrationBuilder.DropColumn(
          name: "ArtNetTimeoutMode",
          table: "ProAreas");

      migrationBuilder.DropColumn(
          name: "ArtNetUniverse",
          table: "ProAreaGroups");

      migrationBuilder.DropColumn(
          name: "ArtNetStartChannel",
          table: "ProAreaGroups");

      migrationBuilder.DropColumn(
          name: "ArtNetFixtureMode",
          table: "ProAreaGroups");

      migrationBuilder.DropColumn(
          name: "ArtNetLightOrderJson",
          table: "ProAreaGroups");
    }
  }
}
