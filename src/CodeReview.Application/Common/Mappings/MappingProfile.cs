using AutoMapper;
using CodeReview.Application.DTOs;
using CodeReview.Domain.Entities;
using CodeReview.Domain.ValueObjects;

namespace CodeReview.Application.Common.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CodeAnalysis, AnalysisSummaryDto>()
            .ForMember(d => d.IssueCount, o => o.MapFrom(s => s.Issues.Count))
            .ForMember(d => d.CriticalIssueCount, o => o.MapFrom(s => s.Issues.Count(i => i.Severity == Domain.Enums.IssueSeverity.Critical)))
            .ForMember(d => d.SourceType, o => o.MapFrom(s => s.Source.Type));

        CreateMap<CodeAnalysis, AnalysisDetailDto>();

        CreateMap<QualityScore, QualityScoreDto>();

        CreateMap<StaticMetrics, StaticMetricsDto>();

        CreateMap<AnalysisIssue, AnalysisIssueDto>();

        CreateMap<CodeSource, CodeSourceDto>();

        CreateMap<User, UserDto>()
            .ForMember(d => d.HasGitHubConnected, o => o.MapFrom(s => s.HasGitHubConnected));
    }
}
