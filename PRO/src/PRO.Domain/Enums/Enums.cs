namespace PRO.Domain.Enums;

public enum EntityStatus
{
    Active = 1,
    Inactive = 0
}

public enum EmployeeStatus
{
    Active = 1,
    Inactive = 0
}

public enum RoleType
{
    HeadquartersAdmin = 1,
    BranchAdmin = 2,
    RegionAdmin = 3,
    Employee = 4
}

public enum PermissionType
{
    View = 1,
    Create = 2,
    Edit = 3,
    Delete = 4,
    Export = 5,
    Print = 6
}

public enum SyncStatus
{
    Pending = 0,
    Syncing = 1,
    Synced = 2,
    Conflict = 3,
    Failed = 4
}

public enum CustomerType
{
    Major = 1,
    Sub = 2
}

public enum CustomerStatus
{
    Active = 1,
    Merged = 0,
    Deleted = -1
}

public enum OrderStatus
{
    Pending = 1,
    Assigned = 2,
    Delivering = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
    Draft = 0
}

public enum PaymentStatus
{
    Unpaid = 0,
    PartialPaid = 1,
    Paid = 2,
    Legal = 3
}

public enum ProductStatus
{
    Active = 1,
    Inactive = 0
}

public enum DeliveryPersonStatus
{
    Available = 1,
    Busy = 2,
    Off = 0
}

public enum SettlementStatus
{
    Completed = 1,
    Cancelled = 0
}

public enum CloseBehavior
{
    MinimizeToTray = 0,
    Exit = 1
}

public enum ConflictResolution
{
    TimestampFirst = 0,
    HeadquartersFirst = 1,
    Manual = 2
}

public enum Gender
{
    Unknown = 0,
    Male = 1,
    Female = 2
}

public enum LoginMethod
{
    AccountPassword = 0,
    WeChatWork = 1
}

public enum OpportunityStage
{
    Trial,
    Communication,
    Quotation,
    Negotiation,
    Won,
    Lost
}

public enum InventoryCheckStatus
{
    InProgress,
    Completed,
    Confirmed
}
