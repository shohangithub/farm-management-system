namespace Farm360.Domain.Feeding.Enums;

public enum FeedCategory
{
    Forage = 1,
    Concentrate = 2,
    Mineral = 3,
    Additive = 4,
    Silage = 5,
    Byproduct = 6
}

public enum TargetAnimalType
{
    Cattle = 1,
    Goat = 2,
    Sheep = 3,
    Buffalo = 4
}

public enum ScheduleFrequency
{
    OnceDaily = 1,
    TwiceDaily = 2,
    ThriceDaily = 3,
    AdLibitum = 4
}

public enum FormulaStatus
{
    Draft = 1,
    Active = 2,
    Archived = 3
}

public enum FeedingPlanType
{
    FixedQuantity = 1,
    WeightPercentage = 2,
    AgeBased = 3,
    SevenDay = 4,
    FifteenDay = 5,
    ThirtyDay = 6,
    Custom = 7
}

public enum DailyFeedingEntryStatus
{
    Pending = 1,
    Confirmed = 2,
    Skipped = 3,
    Adjusted = 4,
    Exception = 5
}

public enum FeedingPurpose
{
    Fattening = 1,
    Dairy = 2,
    Breeding = 3,
    Maintenance = 4,
    Growth = 5,
    Gestation = 6,
    Lactation = 7,
    Finishing = 8,
    Starter = 9,
    Transition = 10
}

public enum FeedingPlanStatus
{
    Draft = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ReconciliationStatus
{
    Pending = 1,
    Reviewed = 2,
    Approved = 3,
    Rejected = 4
}
