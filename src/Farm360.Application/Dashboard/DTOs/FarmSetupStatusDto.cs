using System;
using System.Collections.Generic;

namespace Farm360.Application.Dashboard.DTOs;

/// <summary>
/// One row of the "Get your farm ready" checklist. <see cref="Key"/> is stable
/// (sheds, location, breeds, animals, weights, inventory, recipes, feedChart, plans, accounts, team);
/// the web app owns the wording and the link for each key.
/// </summary>
public sealed record FarmSetupStepDto(string Key, bool IsDone, int Count);

public sealed record FarmSetupStatusDto(
    Guid FarmId,
    int CompletedSteps,
    int TotalSteps,
    bool IsComplete,
    IReadOnlyList<FarmSetupStepDto> Steps);
