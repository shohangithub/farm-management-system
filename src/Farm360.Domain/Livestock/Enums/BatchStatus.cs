namespace Farm360.Domain.Livestock.Enums;

public enum BatchStatus
{
    /// <summary>Batch is currently active and contains animals.</summary>
    Active = 1,
    
    /// <summary>Batch has been archived and is no longer actively managed.</summary>
    Archived = 2
}
