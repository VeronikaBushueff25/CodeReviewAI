using AutoMapper;
using CodeReview.Application.Common.Exceptions;
using CodeReview.Application.DTOs;
using CodeReview.Domain.Interfaces;
using MediatR;

namespace CodeReview.Application.Features.Users.Commands;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IUserRepository _repo;
    private readonly IMapper _mapper;

    public GetCurrentUserQueryHandler(IUserRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<UserDto> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await _repo.GetByIdAsync(query.UserId, ct)
            ?? throw new NotFoundException("User", query.UserId);

        return _mapper.Map<UserDto>(user);
    }
}
