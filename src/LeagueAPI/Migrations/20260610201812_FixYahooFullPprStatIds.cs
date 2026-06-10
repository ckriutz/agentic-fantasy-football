using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixYahooFullPprStatIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM scoring_template_rules
                WHERE "TemplateKey" = 'full-ppr';

                INSERT INTO scoring_template_rules ("TemplateKey", "StatId", "StatName", "Modifier")
                VALUES
                    ('full-ppr', 4, 'Passing Yards', 0.04),
                    ('full-ppr', 5, 'Passing Touchdowns', 4.0),
                    ('full-ppr', 6, 'Interceptions', -1.0),
                    ('full-ppr', 9, 'Rushing Yards', 0.1),
                    ('full-ppr', 10, 'Rushing Touchdowns', 6.0),
                    ('full-ppr', 11, 'Receptions', 1.0),
                    ('full-ppr', 12, 'Receiving Yards', 0.1),
                    ('full-ppr', 13, 'Receiving Touchdowns', 6.0),
                    ('full-ppr', 16, '2-Point Conversions', 2.0),
                    ('full-ppr', 18, 'Fumbles Lost', -1.0),
                    ('full-ppr', 19, 'FG Made 0-19 Yards', 3.0),
                    ('full-ppr', 20, 'FG Made 20-29 Yards', 3.0),
                    ('full-ppr', 21, 'FG Made 30-39 Yards', 3.0),
                    ('full-ppr', 22, 'FG Made 40-49 Yards', 4.0),
                    ('full-ppr', 23, 'FG Made 50+ Yards', 5.0),
                    ('full-ppr', 24, 'FG Missed 0-19 Yards', -2.0),
                    ('full-ppr', 25, 'FG Missed 20-29 Yards', 0.0),
                    ('full-ppr', 26, 'FG Missed 30-39 Yards', 0.0),
                    ('full-ppr', 27, 'FG Missed 40-49 Yards', 0.0),
                    ('full-ppr', 28, 'FG Missed 50+ Yards', 0.0),
                    ('full-ppr', 29, 'PAT Made', 1.0),
                    ('full-ppr', 30, 'PAT Missed', -1.0),
                    ('full-ppr', 32, 'Sack', 1.0),
                    ('full-ppr', 33, 'Defensive Interception', 2.0),
                    ('full-ppr', 34, 'Fumble Recovery', 2.0),
                    ('full-ppr', 35, 'Defensive/ST Touchdown', 6.0),
                    ('full-ppr', 36, 'Safety', 2.0),
                    ('full-ppr', 37, 'Blocked Kick', 2.0),
                    ('full-ppr', 50, 'Points Allowed 0', 10.0),
                    ('full-ppr', 51, 'Points Allowed 1-6', 7.0),
                    ('full-ppr', 52, 'Points Allowed 7-13', 4.0),
                    ('full-ppr', 53, 'Points Allowed 14-20', 1.0),
                    ('full-ppr', 54, 'Points Allowed 21-27', 0.0);

                UPDATE scoring_templates
                SET "UpdatedAtUtc" = TIMESTAMPTZ '2026-06-10 20:18:12+00'
                WHERE "TemplateKey" = 'full-ppr';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM scoring_template_rules
                WHERE "TemplateKey" = 'full-ppr';

                INSERT INTO scoring_template_rules ("TemplateKey", "StatId", "StatName", "Modifier")
                VALUES
                    ('full-ppr', 4, 'Passing Yards', 0.04),
                    ('full-ppr', 5, 'Passing Touchdowns', 4.0),
                    ('full-ppr', 6, 'Interceptions', -1.0),
                    ('full-ppr', 9, 'Rushing Yards', 0.1),
                    ('full-ppr', 10, 'Rushing Touchdowns', 6.0),
                    ('full-ppr', 11, 'Receptions', 1.0),
                    ('full-ppr', 12, 'Receiving Yards', 0.1),
                    ('full-ppr', 13, 'Receiving Touchdowns', 6.0),
                    ('full-ppr', 15, '2-Point Conversion (Pass)', 2.0),
                    ('full-ppr', 16, '2-Point Conversion (Rush)', 2.0),
                    ('full-ppr', 17, 'Fumbles Lost', -1.0),
                    ('full-ppr', 19, '2-Point Conversion (Rec)', 2.0),
                    ('full-ppr', 45, 'Sack', 1.0),
                    ('full-ppr', 46, 'Defensive Interception', 2.0),
                    ('full-ppr', 47, 'Fumble Recovery', 2.0),
                    ('full-ppr', 48, 'Defensive/ST Touchdown', 6.0),
                    ('full-ppr', 49, 'Safety', 2.0),
                    ('full-ppr', 50, 'Blocked Kick', 2.0),
                    ('full-ppr', 52, 'Points Allowed 0', 10.0),
                    ('full-ppr', 53, 'Points Allowed 1-6', 7.0),
                    ('full-ppr', 54, 'Points Allowed 7-13', 4.0),
                    ('full-ppr', 55, 'Points Allowed 14-20', 1.0),
                    ('full-ppr', 56, 'Points Allowed 21-27', 0.0),
                    ('full-ppr', 57, 'PAT Made', 1.0),
                    ('full-ppr', 58, 'PAT Missed', -1.0),
                    ('full-ppr', 59, 'FG Made 0-19 Yards', 3.0),
                    ('full-ppr', 60, 'FG Made 20-29 Yards', 3.0),
                    ('full-ppr', 61, 'FG Made 30-39 Yards', 3.0),
                    ('full-ppr', 62, 'FG Made 40-49 Yards', 4.0),
                    ('full-ppr', 63, 'FG Made 50+ Yards', 5.0),
                    ('full-ppr', 64, 'FG Missed 0-19 Yards', -2.0),
                    ('full-ppr', 65, 'FG Missed 20-29 Yards', 0.0),
                    ('full-ppr', 66, 'FG Missed 30-39 Yards', 0.0),
                    ('full-ppr', 67, 'FG Missed 40-49 Yards', 0.0),
                    ('full-ppr', 68, 'FG Missed 50+ Yards', 0.0);

                UPDATE scoring_templates
                SET "UpdatedAtUtc" = TIMESTAMPTZ '2026-04-23 21:57:16.482314+00'
                WHERE "TemplateKey" = 'full-ppr';
                """);
        }
    }
}
