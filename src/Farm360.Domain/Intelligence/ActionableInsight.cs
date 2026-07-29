using Farm360.Domain.Common;
using Farm360.Domain.Intelligence.Enums;
using System;

namespace Farm360.Domain.Intelligence;

public class ActionableInsight : AuditableEntity
{
    public Guid FarmId { get; private set; }
    public Guid? AnimalId { get; private set; }
    public Guid? BatchId { get; private set; }
    
    public InsightType Type { get; private set; }
    public InsightSeverity Severity { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? ActionData { get; private set; }
    
    public bool IsRead { get; private set; }
    public bool IsDismissed { get; private set; }
    
    private ActionableInsight() { } // EF Core
    
    public ActionableInsight(
        Guid id,
        Guid tenantId,
        Guid farmId,
        InsightType type,
        InsightSeverity severity,
        string title,
        string message,
        Guid? animalId = null,
        Guid? batchId = null,
        string? actionData = null) : base(id, tenantId)
    {
        FarmId = farmId;
        Type = type;
        Severity = severity;
        Title = title;
        Message = message;
        AnimalId = animalId;
        BatchId = batchId;
        ActionData = actionData;
    }
    
    public void MarkAsRead() => IsRead = true;
    public void Dismiss() => IsDismissed = true;
}
