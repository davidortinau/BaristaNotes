using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class ShiftRatingScaleTo04 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Shift existing rating data from the historical 1-5 scale onto the
            // canonical 0-4 scale (constitution §V: 0=Terrible..4=Excellent).
            // Bounded CASE so the migration is safe against pre-existing rows
            // that may already be 0-4 (e.g. dev-time SQL meddling) or
            // out-of-range (>5 or <0). After this:
            //   NULL       -> NULL (unrated)
            //   1..5       -> 0..4 (the expected shift)
            //   0          -> 0   (already canonical; was technically invalid)
            //   <0 / >5    -> clamped into 0..4
            migrationBuilder.Sql(@"
                UPDATE ShotRecords
                SET Rating = CASE
                    WHEN Rating IS NULL THEN NULL
                    WHEN Rating BETWEEN 1 AND 5 THEN Rating - 1
                    WHEN Rating < 0 THEN 0
                    WHEN Rating > 5 THEN 4
                    ELSE Rating
                END
                WHERE Rating IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse only values that fit the post-Up canonical range so the
            // down migration is also bounded.
            migrationBuilder.Sql(@"
                UPDATE ShotRecords
                SET Rating = CASE
                    WHEN Rating IS NULL THEN NULL
                    WHEN Rating BETWEEN 0 AND 4 THEN Rating + 1
                    ELSE Rating
                END
                WHERE Rating IS NOT NULL;
            ");
        }
    }
}
