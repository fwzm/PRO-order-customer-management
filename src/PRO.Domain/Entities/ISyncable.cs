using PRO.Domain.Enums;

namespace PRO.Domain.Entities;

/// <summary>
/// 可同步实体接口（支持本地↔云端双向同步）
/// </summary>
public interface ISyncable
{
    SyncStatus SyncStatus { get; set; }
    long LocalTimestamp { get; set; }
    long? RemoteTimestamp { get; set; }
}
