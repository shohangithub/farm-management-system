using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record CreateInvestorCommand(
    Guid TenantId,
    Guid FarmId,
    string Name,
    decimal InitialInvestmentBdt,
    DateTime InvestmentDate,
    decimal? AgreedProfitSharePercentage = null,
    string? Email = null,
    string? Phone = null,
    string? NationalId = null,
    string? Notes = null,
    string? ReferenceId = null
) : IRequest<InvestorDto>;

public class CreateInvestorCommandHandler : IRequestHandler<CreateInvestorCommand, InvestorDto>
{
    private readonly IInvestorRepository _investorRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInvestorCommandHandler(
        IInvestorRepository investorRepository,
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _investorRepository = investorRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<InvestorDto> Handle(CreateInvestorCommand request, CancellationToken cancellationToken)
    {
        var investor = Investor.Create(
            request.TenantId,
            request.FarmId,
            request.Name,
            request.InitialInvestmentBdt,
            request.InvestmentDate,
            request.AgreedProfitSharePercentage,
            request.Email,
            request.Phone,
            request.NationalId,
            request.Notes,
            request.ReferenceId
        );

        _investorRepository.Add(investor);

        // If initial capital was invested, also post an entry to the General Ledger
        if (request.InitialInvestmentBdt > 0)
        {
            var glTx = FinancialTransaction.Create(
                request.TenantId,
                request.FarmId,
                TransactionType.Income,
                TransactionCategory.InvestorCapital,
                request.InitialInvestmentBdt,
                request.InvestmentDate,
                request.ReferenceId ?? string.Empty,
                request.Notes ?? string.Empty,
                $"Initial capital contribution from investor {investor.Name}"
            );

            await _transactionRepository.AddAsync(glTx, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Calculate effective share percentage across the farm
        var allInvestors = await _investorRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
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
