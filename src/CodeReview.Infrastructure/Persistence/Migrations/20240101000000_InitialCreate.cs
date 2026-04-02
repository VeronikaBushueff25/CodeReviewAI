using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeReview.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Users table ───────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(nullable: false),
                email = table.Column<string>(maxLength: 256, nullable: false),
                username = table.Column<string>(maxLength: 50, nullable: false),
                password_hash = table.Column<string>(nullable: false),
                github_login = table.Column<string>(maxLength: 100, nullable: true),
                github_access_token = table.Column<string>(nullable: true),
                is_email_verified = table.Column<bool>(nullable: false, defaultValue: false),
                last_login_at = table.Column<DateTime>(nullable: true),
                analysis_count = table.Column<int>(nullable: false, defaultValue: 0),
                is_active = table.Column<bool>(nullable: false, defaultValue: true),
                created_at = table.Column<DateTime>(nullable: false),
                updated_at = table.Column<DateTime>(nullable: true),
                created_by = table.Column<Guid>(nullable: true),
                updated_by = table.Column<Guid>(nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_users", x => x.id));

        migrationBuilder.CreateIndex("ix_users_email", "users", "email", unique: true);
        migrationBuilder.CreateIndex("ix_users_username", "users", "username", unique: true);

        // ── Analyses table ────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "analyses",
            columns: table => new
            {
                id = table.Column<Guid>(nullable: false),
                user_id = table.Column<Guid>(nullable: false),
                title = table.Column<string>(maxLength: 200, nullable: false),
                status = table.Column<int>(nullable: false),
                language = table.Column<int>(nullable: false),
                failure_reason = table.Column<string>(nullable: true),
                started_at = table.Column<DateTime>(nullable: true),
                completed_at = table.Column<DateTime>(nullable: true),
                created_at = table.Column<DateTime>(nullable: false),
                updated_at = table.Column<DateTime>(nullable: true),
                created_by = table.Column<Guid>(nullable: true),
                updated_by = table.Column<Guid>(nullable: true),
                // CodeSource (owned)
                source_type = table.Column<int>(nullable: false),
                source_content = table.Column<string>(nullable: false),
                source_file_name = table.Column<string>(maxLength: 500, nullable: true),
                source_repo = table.Column<string>(maxLength: 200, nullable: true),
                source_pr_number = table.Column<int>(nullable: true),
                source_commit_sha = table.Column<string>(maxLength: 50, nullable: true),
                source_size_bytes = table.Column<long>(nullable: false),
                // QualityScore (owned)
                score_overall = table.Column<double>(precision: 5, scale: 2, nullable: true),
                score_architecture = table.Column<double>(precision: 5, scale: 2, nullable: true),
                score_readability = table.Column<double>(precision: 5, scale: 2, nullable: true),
                score_maintainability = table.Column<double>(precision: 5, scale: 2, nullable: true),
                // StaticMetrics (owned)
                metrics_total_lines = table.Column<int>(nullable: true),
                metrics_code_lines = table.Column<int>(nullable: true),
                metrics_comment_lines = table.Column<int>(nullable: true),
                metrics_blank_lines = table.Column<int>(nullable: true),
                metrics_avg_complexity = table.Column<double>(precision: 5, scale: 2, nullable: true),
                metrics_max_complexity = table.Column<int>(nullable: true),
                metrics_long_method_count = table.Column<int>(nullable: true),
                metrics_duplicate_blocks = table.Column<int>(nullable: true),
                metrics_duplication_pct = table.Column<double>(precision: 5, scale: 2, nullable: true),
                metrics_total_methods = table.Column<int>(nullable: true),
                metrics_total_classes = table.Column<int>(nullable: true),
                metrics_avg_method_length = table.Column<double>(precision: 5, scale: 2, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_analyses", x => x.id);
                table.ForeignKey("fk_analyses_users", x => x.user_id, "users", "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("ix_analyses_user_id", "analyses", "user_id");
        migrationBuilder.CreateIndex("ix_analyses_user_created", "analyses", ["user_id", "created_at"]);
        migrationBuilder.CreateIndex("ix_analyses_status", "analyses", "status");

        // ── Issues table ──────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "analysis_issues",
            columns: table => new
            {
                id = table.Column<Guid>(nullable: false),
                analysis_id = table.Column<Guid>(nullable: false),
                title = table.Column<string>(maxLength: 300, nullable: false),
                description = table.Column<string>(nullable: false),
                suggestion = table.Column<string>(nullable: true),
                severity = table.Column<int>(nullable: false),
                category = table.Column<int>(nullable: false),
                line_start = table.Column<int>(nullable: true),
                line_end = table.Column<int>(nullable: true),
                code_snippet = table.Column<string>(nullable: true),
                is_ai_generated = table.Column<bool>(nullable: false),
                confidence = table.Column<double>(precision: 4, scale: 3, nullable: true),
                created_at = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_analysis_issues", x => x.id);
                table.ForeignKey("fk_issues_analyses", x => x.analysis_id, "analyses", "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("ix_issues_analysis_id", "analysis_issues", "analysis_id");
        migrationBuilder.CreateIndex("ix_issues_severity", "analysis_issues", "severity");

        // ── Snapshots table ───────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "analysis_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(nullable: false),
                analysis_id = table.Column<Guid>(nullable: false),
                overall_score = table.Column<double>(precision: 5, scale: 2, nullable: false),
                architecture_score = table.Column<double>(precision: 5, scale: 2, nullable: false),
                readability_score = table.Column<double>(precision: 5, scale: 2, nullable: false),
                maintainability_score = table.Column<double>(precision: 5, scale: 2, nullable: false),
                total_issues = table.Column<int>(nullable: false),
                version = table.Column<int>(nullable: false),
                created_at = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_analysis_snapshots", x => x.id);
                table.ForeignKey("fk_snapshots_analyses", x => x.analysis_id, "analyses", "id", onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("analysis_snapshots");
        migrationBuilder.DropTable("analysis_issues");
        migrationBuilder.DropTable("analyses");
        migrationBuilder.DropTable("users");
    }
}
