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
