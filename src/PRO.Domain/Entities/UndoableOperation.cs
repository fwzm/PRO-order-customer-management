namespace PRO.Domain.Entities;

public class UndoableOperation
{
    public int Id { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SnapshotData { get; set; }
    public int OperatorId { get; set; }
    public DateTime OperatedAt { get; set; } = DateTime.Now;
    public DateTime UndoDeadline { get; set; }
    public bool IsUndone { get; set; }
    public DateTime? UndoneAt { get; set; }
    public Employee? Operator { get; set; }
}
