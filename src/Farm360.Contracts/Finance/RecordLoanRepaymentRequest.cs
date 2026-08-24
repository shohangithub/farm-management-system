using System;

namespace Farm360.Contracts.Finance;

public record RecordLoanRepaymentRequest(
    decimal AmountBdt,
    DateTime RepaymentDate,
    string ReferenceId = "",
    string Notes = ""
);
