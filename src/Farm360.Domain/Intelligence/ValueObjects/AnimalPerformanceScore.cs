using Farm360.Domain.Common;
using System.Collections.Generic;

namespace Farm360.Domain.Intelligence.ValueObjects;

public sealed class AnimalPerformanceScore : BaseValueObject
{
    public int ScoreOutOf100 { get; private set; }
    public string Status { get; private set; } = string.Empty; // e.g. "On Track", "Underperforming", "Excellent"
    
    private AnimalPerformanceScore() { } // EF Core
    
    public AnimalPerformanceScore(int score, string status)
    {
        ScoreOutOf100 = score;
        Status = status;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ScoreOutOf100;
        yield return Status;
    }
}
