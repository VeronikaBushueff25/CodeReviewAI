using CodeReview.Application.Common.Exceptions;
using CodeReview.Domain.Interfaces;
using MediatR;

namespace CodeReview.Application.Features.GitHub.Commands;

public sealed record DisconnectGitHubCommand(Guid UserId) : IRequest;

public sealed class DisconnectGitHubCommandHandler : IRequestHandler<DisconnectGitHubCommand>
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DisconnectGitHubCommandHandler(IUserRepository userRepo, IUnitOfWork unitOfWork)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DisconnectGitHubCommand cmd, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(cmd.UserId, ct)
            ?? throw new NotFoundException("User", cmd.UserId);

        user.DisconnectGitHub();
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
