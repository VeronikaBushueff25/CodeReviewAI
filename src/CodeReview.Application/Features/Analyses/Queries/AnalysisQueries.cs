using AutoMapper;
using CodeReview.Application.Common.Exceptions;
using CodeReview.Application.DTOs;
using CodeReview.Domain.Enums;
using CodeReview.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace CodeReview.Application.Features.Analyses.Queries;

// ─── Get Analysis By Id ───────────────────────────────────────────────────────
public sealed record GetAnalysisByIdQuery(Guid AnalysisId, Guid RequestingUserId) : IRequest<AnalysisDetailDto>;

public sealed class GetAnalysisByIdQueryHandler : IRequestHandler<GetAnalysisByIdQuery, AnalysisDetailDto>
{
    private readonly IAnalysisRepository _repo;
    private readonly IMapper _mapper;

    public GetAnalysisByIdQueryHandler(IAnalysisRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<AnalysisDetailDto> Handle(GetAnalysisByIdQuery query, CancellationToken ct)
    {
        var analysis = await _repo.GetWithIssuesAsync(query.AnalysisId, ct)
            ?? throw new NotFoundException("CodeAnalysis", query.AnalysisId);

        if (!analysis.BelongsTo(query.RequestingUserId))
            throw new ForbiddenException();

        return _mapper.Map<AnalysisDetailDto>(analysis);
    }
}

// ─── Get Paged Analyses For User ─────────────────────────────────────────────
public sealed record GetAnalysesPagedQuery : IRequest<PagedResult<AnalysisSummaryDto>>
{
    public required Guid UserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public AnalysisStatus? StatusFilter { get; init; }
}

public sealed class GetAnalysesPagedQueryValidator : AbstractValidator<GetAnalysesPagedQuery>
{
    public GetAnalysesPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetAnalysesPagedQueryHandler : IRequestHandler<GetAnalysesPagedQuery, PagedResult<AnalysisSummaryDto>>
{
    private readonly IAnalysisRepository _repo;
    private readonly IMapper _mapper;

    public GetAnalysesPagedQueryHandler(IAnalysisRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<PagedResult<AnalysisSummaryDto>> Handle(GetAnalysesPagedQuery query, CancellationToken ct)
    {
        var (items, total) = await _repo.GetPagedByUserAsync(
            query.UserId, query.Page, query.PageSize, query.StatusFilter, ct);

        return new PagedResult<AnalysisSummaryDto>
        {
            Items = _mapper.Map<IReadOnlyList<AnalysisSummaryDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}

// ─── Delete Analysis ──────────────────────────────────────────────────────────
public sealed record DeleteAnalysisCommand(Guid AnalysisId, Guid RequestingUserId) : IRequest;

public sealed class DeleteAnalysisCommandHandler : IRequestHandler<DeleteAnalysisCommand>
{
    private readonly IAnalysisRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAnalysisCommandHandler(IAnalysisRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteAnalysisCommand cmd, CancellationToken ct)
    {
        var analysis = await _repo.GetByIdAsync(cmd.AnalysisId, ct)
            ?? throw new NotFoundException("CodeAnalysis", cmd.AnalysisId);

        if (!analysis.BelongsTo(cmd.RequestingUserId))
            throw new ForbiddenException();

        _repo.Remove(analysis);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
