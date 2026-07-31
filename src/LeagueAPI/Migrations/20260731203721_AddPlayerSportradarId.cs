using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerSportradarId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SportradarId",
                table: "players",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // RawJson is text holding the original Sleeper payload. Backfill without requiring a re-sync.
            // Guard empty/invalid JSON so a bad row cannot fail the migration.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pg_temp.try_jsonb_text_field(raw text, key text)
                RETURNS text
                LANGUAGE plpgsql
                AS $func$
                BEGIN
                    IF raw IS NULL OR btrim(raw) = '' OR left(btrim(raw), 1) <> '{' THEN
                        RETURN NULL;
                    END IF;

                    RETURN NULLIF(btrim((raw::jsonb) ->> key), '');
                EXCEPTION
                    WHEN others THEN
                        RETURN NULL;
                END;
                $func$;

                UPDATE players
                SET "SportradarId" = pg_temp.try_jsonb_text_field("RawJson", 'sportradar_id')
                WHERE "RawJson" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_players_SportradarId",
                table: "players",
                column: "SportradarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_players_SportradarId",
                table: "players");

            migrationBuilder.DropColumn(
                name: "SportradarId",
                table: "players");
        }
    }
}
