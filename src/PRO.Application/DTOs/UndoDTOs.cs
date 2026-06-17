namespace PRO.Application.DTOs;

/// <summary>
/// 可撤销操作记录
/// </summary>
public class UndoableOperationDto
{
    public int Id { get; set; }
    public string OperationType { get; set; } = ""; // StatusChange/Delete/Assign/Update
    public string EntityType { get; set; } = ""; // Order/Customer/DeliveryPerson
    public int EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public string Description { get; set; } = "";
    public string? SnapshotData { get; set; } // JSON快照
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = "";
    public DateTime OperatedAt { get; set; }
    public DateTime UndoDeadline { get; set; }
    public bool IsUndone { get; set; }
    public DateTime? UndoneAt { get; set; }
    public bool CanUndo => !IsUndone && DateTime.Now < UndoDeadline;
    public int RemainingSeconds => CanUndo ? (int)(UndoDeadline - DateTime.Now).TotalSeconds : 0;
}

/// <summary>
/// 操作快照
/// </summary>
public class OperationSnapshot
{
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public Dictionary<string, object?> BeforeValues { get; set; } = [];
    public Dictionary<string, object?> AfterValues { get; set; } = [];
    public string? ExtraData { get; set; }
}
