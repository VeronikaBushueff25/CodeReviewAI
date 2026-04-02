using CodeReview.Domain.Entities;
using CodeReview.Domain.Enums;
using CodeReview.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeReview.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.GitHubLogin).HasColumnName("github_login").HasMaxLength(100);
        builder.Property(u => u.GitHubAccessToken).HasColumnName("github_access_token");
        builder.Property(u => u.IsEmailVerified).HasColumnName("is_email_verified");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(u => u.AnalysisCount).HasColumnName("analysis_count");
        builder.Property(u => u.IsActive).HasColumnName("is_active");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ix_users_email");
        builder.HasIndex(u => u.Username).IsUnique().HasDatabaseName("ix_users_username");

        // Ignore domain events — not persisted
        builder.Ignore(u => u.DomainEvents);
    }
}

public sealed class CodeAnalysisConfiguration : IEntityTypeConfiguration<CodeAnalysis>
{
    public void Configure(EntityTypeBuilder<CodeAnalysis> builder)
    {
        builder.ToTable("analyses");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(a => a.Language).HasColumnName("language").HasConversion<int>();
        builder.Property(a => a.FailureReason).HasColumnName("failure_reason");
        builder.Property(a => a.StartedAt).HasColumnName("started_at");
        builder.Property(a => a.CompletedAt).HasColumnName("completed_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");

        // Owned type: CodeSource (Value Object → owned entity)
        builder.OwnsOne(a => a.Source, source =>
        {
            source.Property(s => s.Type).HasColumnName("source_type").HasConversion<int>();
            source.Property(s => s.Content).HasColumnName("source_content").IsRequired();
            source.Property(s => s.FileName).HasColumnName("source_file_name").HasMaxLength(500);
            source.Property(s => s.RepositoryFullName).HasColumnName("source_repo").HasMaxLength(200);
            source.Property(s => s.PullRequestNumber).HasColumnName("source_pr_number");
            source.Property(s => s.CommitSha).HasColumnName("source_commit_sha").HasMaxLength(50);
            source.Property(s => s.ContentSizeBytes).HasColumnName("source_size_bytes");
        });

        // Owned type: QualityScore
        builder.OwnsOne(a => a.Score, score =>
        {
            score.Property(s => s.Overall).HasColumnName("score_overall").HasPrecision(5, 2);
            score.Property(s => s.Architecture).HasColumnName("score_architecture").HasPrecision(5, 2);
            score.Property(s => s.Readability).HasColumnName("score_readability").HasPrecision(5, 2);
            score.Property(s => s.Maintainability).HasColumnName("score_maintainability").HasPrecision(5, 2);
        });

        // Owned type: StaticMetrics
        builder.OwnsOne(a => a.Metrics, m =>
        {
            m.Property(x => x.TotalLines).HasColumnName("metrics_total_lines");
            m.Property(x => x.CodeLines).HasColumnName("metrics_code_lines");
            m.Property(x => x.CommentLines).HasColumnName("metrics_comment_lines");
            m.Property(x => x.BlankLines).HasColumnName("metrics_blank_lines");
            m.Property(x => x.AverageCyclomaticComplexity).HasColumnName("metrics_avg_complexity").HasPrecision(5, 2);
            m.Property(x => x.MaxCyclomaticComplexity).HasColumnName("metrics_max_complexity");
            m.Property(x => x.LongMethodCount).HasColumnName("metrics_long_method_count");
            m.Property(x => x.DuplicateBlockCount).HasColumnName("metrics_duplicate_blocks");
            m.Property(x => x.DuplicationPercentage).HasColumnName("metrics_duplication_pct").HasPrecision(5, 2);
            m.Property(x => x.TotalMethods).HasColumnName("metrics_total_methods");
            m.Property(x => x.TotalClasses).HasColumnName("metrics_total_classes");
            m.Property(x => x.AverageMethodLength).HasColumnName("metrics_avg_method_length").HasPrecision(5, 2);
        });

        builder.HasMany(a => a.Issues)
            .WithOne()
            .HasForeignKey(i => i.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Snapshots)
            .WithOne()
            .HasForeignKey(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.UserId).HasDatabaseName("ix_analyses_user_id");
        builder.HasIndex(a => new { a.UserId, a.CreatedAt }).HasDatabaseName("ix_analyses_user_created");
        builder.HasIndex(a => a.Status).HasDatabaseName("ix_analyses_status");

        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class AnalysisIssueConfiguration : IEntityTypeConfiguration<AnalysisIssue>
{
    public void Configure(EntityTypeBuilder<AnalysisIssue> builder)
    {
        builder.ToTable("analysis_issues");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.AnalysisId).HasColumnName("analysis_id");
        builder.Property(i => i.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(i => i.Description).HasColumnName("description").IsRequired();
        builder.Property(i => i.Suggestion).HasColumnName("suggestion");
        builder.Property(i => i.Severity).HasColumnName("severity").HasConversion<int>();
        builder.Property(i => i.Category).HasColumnName("category").HasConversion<int>();
        builder.Property(i => i.LineStart).HasColumnName("line_start");
        builder.Property(i => i.LineEnd).HasColumnName("line_end");
        builder.Property(i => i.CodeSnippet).HasColumnName("code_snippet");
        builder.Property(i => i.IsAiGenerated).HasColumnName("is_ai_generated");
        builder.Property(i => i.Confidence).HasColumnName("confidence").HasPrecision(4, 3);
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(i => i.AnalysisId).HasDatabaseName("ix_issues_analysis_id");
        builder.HasIndex(i => i.Severity).HasDatabaseName("ix_issues_severity");

        builder.Ignore(i => i.DomainEvents);
    }
}

public sealed class AnalysisSnapshotConfiguration : IEntityTypeConfiguration<AnalysisSnapshot>
{
    public void Configure(EntityTypeBuilder<AnalysisSnapshot> builder)
    {
        builder.ToTable("analysis_snapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.AnalysisId).HasColumnName("analysis_id");
        builder.Property(s => s.OverallScore).HasColumnName("overall_score").HasPrecision(5, 2);
        builder.Property(s => s.ArchitectureScore).HasColumnName("architecture_score").HasPrecision(5, 2);
        builder.Property(s => s.ReadabilityScore).HasColumnName("readability_score").HasPrecision(5, 2);
        builder.Property(s => s.MaintainabilityScore).HasColumnName("maintainability_score").HasPrecision(5, 2);
        builder.Property(s => s.TotalIssues).HasColumnName("total_issues");
        builder.Property(s => s.Version).HasColumnName("version");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.Ignore(s => s.DomainEvents);
    }
}
