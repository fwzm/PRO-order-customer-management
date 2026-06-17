namespace PRO.Domain.Entities;

/// <summary>
/// 导出历史记录
/// </summary>
public class ExportHistory
{
    public int Id { get; set; }

    /// <summary>导出文件名</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>导出类型（Customer/Order/Product等）</summary>
    public string ExportType { get; set; } = string.Empty;

    /// <summary>导出格式（Excel/CSV等）</summary>
    public string Format { get; set; } = "Excel";

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>记录数</summary>
    public int RecordCount { get; set; }

    /// <summary>导出状态（Success/Failed/Pending）</summary>
    public string Status { get; set; } = "Success";

    /// <summary>操作人ID</summary>
    public int OperatorId { get; set; }

    /// <summary>操作人姓名</summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>导出时间</summary>
    public DateTime ExportTime { get; set; } = DateTime.Now;

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 导入历史记录
/// </summary>
public class ImportHistory
{
    public int Id { get; set; }

    /// <summary>导入文件名</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>导入类型（Customer/Order/Product等）</summary>
    public string ImportType { get; set; } = string.Empty;

    /// <summary>导入格式（Excel/CSV等）</summary>
    public string Format { get; set; } = "Excel";

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>总记录数</summary>
    public int TotalCount { get; set; }

    /// <summary>成功记录数</summary>
    public int SuccessCount { get; set; }

    /// <summary>失败记录数</summary>
    public int FailedCount { get; set; }

    /// <summary>导入状态（Success/Failed/Partial/Pending）</summary>
    public string Status { get; set; } = "Success";

    /// <summary>操作人ID</summary>
    public int OperatorId { get; set; }

    /// <summary>操作人姓名</summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>分公司ID</summary>
    public int BranchId { get; set; }

    /// <summary>导入时间</summary>
    public DateTime ImportTime { get; set; } = DateTime.Now;

    /// <summary>错误日志（JSON格式）</summary>
    public string? ErrorLog { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
