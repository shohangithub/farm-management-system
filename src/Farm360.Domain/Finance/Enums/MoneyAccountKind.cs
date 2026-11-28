namespace Farm360.Domain.Finance.Enums;

/// <summary>Where a farm keeps its money: cash in hand, a bank account or a mobile wallet (bKash, Nagad…).</summary>
public enum MoneyAccountKind
{
    Cash = 0,
    Bank = 1,
    MobileWallet = 2,
}
