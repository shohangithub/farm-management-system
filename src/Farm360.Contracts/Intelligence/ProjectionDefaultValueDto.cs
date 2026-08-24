namespace Farm360.Contracts.Intelligence;

public sealed record ProjectionDefaultValueDto<T>(
    T Value,
    ProjectionSourceCode Source,
    string SourceLabel);
