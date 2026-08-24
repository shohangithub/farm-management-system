using System.Collections.Generic;

namespace Farm360.Domain.Intelligence.Projections;

public sealed record FatteningProjectionResult(
    FatteningProjectionInputs Inputs,
    IReadOnlyList<FatteningProjectionDay> Days,
    FatteningProjectionSummary Summary);
