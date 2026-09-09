using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record UpdateInvestorCommand(
    Guid TenantId,
    Guid Id,
    string Name,
    decimal? AgreedProfitSharePercentage,
    string? Email,
    string? Phone,
    string? NationalId,
    string? Notes
) : IRequest<InvestorDto>;

public class UpdateInvestorCommandHandler : IRequestHandler<UpdateInvestorCommand, InvestorDto>
{
    private readonly IInvestorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateInvestorCommandHandler(IInvestorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<InvestorDto> Handle(UpdateInvestorCommand request, CancellationToken cancellationToken)
    {
        var investor = await _repository.GetByIdWithTransactionsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Investor), request.Id);

        if (investor.TenantId != request.TenantId)
            throw new NotFoundException(nameof(Investor), request.Id);

        investor.UpdateDetails(
            request.Name,
            request.AgreedProfitSharePercentage,
            request.Email,
            request.Phone,
            request.NationalId,
            request.Notes
        );

        _repository.Update(investor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var allInvestors = await _repository.GetByFarmIdAsync(investor.FarmId, cancellationToken: cancellationToken);
        var totalActiveCapital = allInvestors.Where(i => i.IsActive).Sum(i => i.CurrentCapitalBdt);
        
        decimal effectiveShare = investor.AgreedProfitSharePercentage
            ?? (totalActiveCapital > 0 ? Math.Round((investor.CurrentCapitalBdt / totalActiveCapital) * 100m, 2) : 0m);

        return new InvestorDto(
            investor.Id,
            investor.FarmId,
            investor.Name,
            investor.Email,
            investor.Phone,
            investor.NationalId,
            investor.InvestmentDate,
            investor.TotalInvestedBdt,
            investor.TotalWithdrawnBdt,
            investor.CurrentCapitalBdt,
            investor.TotalProfitPaidBdt,
            investor.AgreedProfitSharePercentage,
            effectiveShare,
            investor.Notes,
            investor.IsActive,
            investor.CreatedAtUtc,
            investor.Transactions.Select(t => new InvestorTransactionDto(
                t.Id,
                t.InvestorId,
                t.FarmId,
                t.Type.ToString(),
                t.AmountBdt,
                t.TransactionDate,
                t.ReferenceId,
                t.Notes,
                t.FinancialTransactionId,
                t.CreatedAtUtc
            )).ToList()
        );
    }
}
