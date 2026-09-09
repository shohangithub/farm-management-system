using System;
using System.Collections.Generic;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>
/// Investor — Aggregate root for managing farm equity investors,
/// their capital contributions, withdrawals, agreed profit share ratios, and payout history.
/// </summary>
public sealed class Investor : AuditableEntity, IAggregateRoot
{
    private readonly List<InvestorTransaction> _transactions = [];

    public Guid FarmId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? NationalId { get; private set; }
    public DateTime InvestmentDate { get; private set; }
    public decimal TotalInvestedBdt { get; private set; }
    public decimal TotalWithdrawnBdt { get; private set; }
    public decimal TotalProfitPaidBdt { get; private set; }
    public decimal? AgreedProfitSharePercentage { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<InvestorTransaction> Transactions => _transactions;

    /// <summary>
    /// Current active invested equity capital: Total Injected - Total Withdrawn.
    /// </summary>
    public decimal CurrentCapitalBdt => Math.Max(0, TotalInvestedBdt - TotalWithdrawnBdt);

    private Investor() { } // For EF Core

    public static Investor Create(
        Guid tenantId,
        Guid farmId,
        string name,
        decimal initialInvestmentBdt,
        DateTime investmentDate,
        decimal? agreedProfitSharePercentage = null,
        string? email = null,
        string? phone = null,
        string? nationalId = null,
        string? notes = null,
        string? initialReferenceId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Investor name is required.", nameof(name));
        if (initialInvestmentBdt < 0)
            throw new ArgumentException("Initial investment amount cannot be negative.", nameof(initialInvestmentBdt));
        if (agreedProfitSharePercentage.HasValue && (agreedProfitSharePercentage.Value < 0 || agreedProfitSharePercentage.Value > 100))
            throw new ArgumentException("Agreed profit share percentage must be between 0 and 100.", nameof(agreedProfitSharePercentage));

        var investor = new Investor
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Name = name.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim(),
            InvestmentDate = investmentDate,
            TotalInvestedBdt = 0,
            TotalWithdrawnBdt = 0,
            TotalProfitPaidBdt = 0,
            AgreedProfitSharePercentage = agreedProfitSharePercentage,
            Notes = notes?.Trim(),
            IsActive = true
        };

        investor.SetTenantId(tenantId);

        // If there is an initial investment, record it as the first Capital Contribution transaction
        if (initialInvestmentBdt > 0)
        {
            investor.RecordTransaction(
                tenantId,
                InvestorTransactionType.CapitalContribution,
                initialInvestmentBdt,
                investmentDate,
                initialReferenceId,
                "Initial capital contribution");
        }

        return investor;
    }

    /// <summary>
    /// Records a capital injection, capital withdrawal, or profit distribution.
    /// </summary>
    public InvestorTransaction RecordTransaction(
        Guid tenantId,
        InvestorTransactionType type,
        decimal amountBdt,
        DateTime transactionDate,
        string? referenceId = null,
        string? notes = null,
        Guid? financialTransactionId = null)
    {
        if (amountBdt <= 0)
            throw new ArgumentException("Transaction amount must be greater than zero.", nameof(amountBdt));

        switch (type)
        {
            case InvestorTransactionType.CapitalContribution:
                TotalInvestedBdt += amountBdt;
                break;

            case InvestorTransactionType.CapitalWithdrawal:
                if (amountBdt > CurrentCapitalBdt)
                    throw new InvalidOperationException($"Withdrawal amount of {amountBdt:N2} BDT exceeds current capital balance of {CurrentCapitalBdt:N2} BDT.");
                TotalWithdrawnBdt += amountBdt;
                break;

            case InvestorTransactionType.ProfitDistribution:
                TotalProfitPaidBdt += amountBdt;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported transaction type: {type}");
        }

        var tx = InvestorTransaction.Create(
            tenantId,
            Id,
            FarmId,
            type,
            amountBdt,
            transactionDate,
            referenceId,
            notes,
            financialTransactionId);

        _transactions.Add(tx);
        return tx;
    }

    public void UpdateDetails(
        string name,
        decimal? agreedProfitSharePercentage,
        string? email = null,
        string? phone = null,
        string? nationalId = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Investor name is required.", nameof(name));
        if (agreedProfitSharePercentage.HasValue && (agreedProfitSharePercentage.Value < 0 || agreedProfitSharePercentage.Value > 100))
            throw new ArgumentException("Agreed profit share percentage must be between 0 and 100.", nameof(agreedProfitSharePercentage));

        Name = name.Trim();
        AgreedProfitSharePercentage = agreedProfitSharePercentage;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        Notes = notes?.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
