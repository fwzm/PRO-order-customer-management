namespace PRO.Infrastructure.Services;

/// <summary>
/// 业务错误消息服务 - 提供可操作的错误提示
/// </summary>
public static class BusinessMessages
{
    // ==================== 数据库/连接相关 ====================
    public static BusinessError DatabaseConnectionFailed => new()
    {
        Title = "数据库连接失败",
        Message = "无法连接到数据库服务器",
        Suggestion = "请检查网络连接是否正常，或联系管理员确认数据库服务是否运行",
        Actions = new[] { "重试", "查看详情" }
    };

    public static BusinessError NetworkTimeout => new()
    {
        Title = "网络超时",
        Message = "请求超时，可能是网络不稳定",
        Suggestion = "请检查网络连接后重试",
        Actions = new[] { "重试" }
    };

    // ==================== 通用实体相关 ====================
    public static BusinessError EntityNotFound(string entityName) => new()
    {
        Title = $"{entityName}不存在",
        Message = $"找不到指定的{entityName}",
        Suggestion = "请确认数据是否已被删除，或刷新列表后重试",
        Actions = new[] { "刷新列表" }
    };

    public static BusinessError EntityHasChildren(string entityName, string childName) => new()
    {
        Title = $"{entityName}有关联数据",
        Message = $"该{entityName}下存在{childName}，无法直接删除",
        Suggestion = $"请先处理关联的{childName}，或联系管理员处理",
        Actions = new[] { $"查看{childName}" }
    };

    // ==================== 客户相关 ====================
    public static BusinessError CustomerNotFound => EntityNotFound("客户");

    public static BusinessError CustomerHasOrders => new()
    {
        Title = "客户有关联订单",
        Message = "该客户存在历史订单，无法直接删除",
        Suggestion = "如需删除，请先处理关联订单，或使用「合并客户」功能",
        Actions = new[] { "查看订单", "合并客户" }
    };

    public static BusinessError CustomerWeChatBound => new()
    {
        Title = "客户已绑定企微",
        Message = "该客户已关联企业微信，无法删除",
        Suggestion = "如需删除，请先在企微后台解除客户关系",
        Actions = new[] { "查看详情" }
    };

    public static BusinessError CustomerPhoneDuplicate(string existingName) => new()
    {
        Title = "手机号已存在",
        Message = $"该手机号已关联客户「{existingName}」",
        Suggestion = "建议合并客户，或确认是否为同一客户",
        Actions = new[] { "查看已有客户", "合并客户", "继续保存" }
    };

    public static BusinessError CustomerNameDuplicate(double similarity) => new()
    {
        Title = "疑似重复客户",
        Message = $"发现相似度 {similarity:P0} 的客户",
        Suggestion = "建议先查看已有客户信息，避免重复录入",
        Actions = new[] { "查看相似客户", "忽略并继续" }
    };

    // ==================== 订单相关 ====================
    public static BusinessError OrderNotFound => EntityNotFound("订单");

    public static BusinessError OrderAlreadySettled => new()
    {
        Title = "订单已结算",
        Message = "该订单已完成结算，无法修改或删除",
        Suggestion = "如需调整，请先撤销结算",
        Actions = new[] { "查看结算单" }
    };

    public static BusinessError OrderStatusInvalid(string currentStatus, string targetStatus, string validStatuses) => new()
    {
        Title = "状态变更不允许",
        Message = $"不允许从「{currentStatus}」直接变更为「{targetStatus}」",
        Suggestion = $"当前状态只能变更为：{validStatuses}",
        Actions = new[] { "查看状态流转图" }
    };

    public static BusinessError OrderCancelReasonRequired => new()
    {
        Title = "取消原因必填",
        Message = "取消订单时必须填写取消原因",
        Suggestion = "请填写取消原因，便于后续追溯",
        Actions = Array.Empty<string>()
    };

    public static BusinessError OrderDraftExpired => new()
    {
        Title = "草稿已过期",
        Message = "该草稿订单已超过有效期，系统将自动确认",
        Suggestion = "如需取消，请立即操作",
        Actions = new[] { "确认草稿", "取消订单" }
    };

    // ==================== 配送员相关 ====================
    public static BusinessError DeliveryPersonNotFound => EntityNotFound("配送员");

    public static BusinessError DeliveryPersonOverloaded(string personName) => new()
    {
        Title = "配送员已满载",
        Message = $"配送员「{personName}」当前负载已满，无法继续分配",
        Suggestion = "请选择其他配送员，或等待当前配送完成后重新分配",
        Actions = new[] { "查看配送员列表", "自动分配" }
    };

    public static BusinessError DeliveryPersonUnavailable(string personName) => new()
    {
        Title = "配送员不可用",
        Message = $"配送员「{personName}」当前状态不可用",
        Suggestion = "请选择其他配送员",
        Actions = new[] { "查看配送员列表" }
    };

    public static BusinessError DeliveryPersonHasOrders => new()
    {
        Title = "配送员有未完成订单",
        Message = "该配送员有未完成的配送订单，无法删除",
        Suggestion = "请先将订单重新分配给其他配送员",
        Actions = new[] { "查看订单" }
    };

    // ==================== 产品相关 ====================
    public static BusinessError ProductNotFound => EntityNotFound("产品");

    public static BusinessError ProductHasOrders => new()
    {
        Title = "产品有关联订单",
        Message = "该产品已关联历史订单，无法删除",
        Suggestion = "如需停用产品，可以将状态改为「停用」而非删除",
        Actions = new[] { "查看订单", "停用产品" }
    };

    public static BusinessError ProductSkuDuplicate => new()
    {
        Title = "SKU已存在",
        Message = "该SKU编码已被其他产品使用",
        Suggestion = "请使用不同的SKU编码",
        Actions = new[] { "查看已有产品" }
    };

    public static BusinessError ProductStockInsufficient(string productName, int available, int required) => new()
    {
        Title = "库存不足",
        Message = $"产品「{productName}」库存不足：当前 {available}，需要 {required}",
        Suggestion = "请减少订单数量，或等待补货后再下单",
        Actions = new[] { "查看库存" }
    };

    // ==================== 结算相关 ====================
    public static BusinessError SettlementNoOrders => new()
    {
        Title = "无可结算订单",
        Message = "所选时间范围内没有可结算的订单",
        Suggestion = "请检查订单状态是否为「已完成」且未结算",
        Actions = new[] { "查看订单列表" }
    };

    public static BusinessError SettlementDateInvalid => new()
    {
        Title = "结算日期无效",
        Message = "结算结束日期不能早于开始日期",
        Suggestion = "请重新选择结算日期范围",
        Actions = Array.Empty<string>()
    };

    public static BusinessError SettlementPdfMissing => new()
    {
        Title = "结算PDF不存在",
        Message = "结算PDF文件不存在或已被移动",
        Suggestion = "请在桌面端结算页面重新生成PDF后再下载",
        Actions = new[] { "重新生成PDF" }
    };

    public static BusinessError SettlementPdfNotGenerated => new()
    {
        Title = "结算PDF未生成",
        Message = "该结算单尚未生成PDF，请在桌面端结算页面生成",
        Suggestion = "请先生成结算PDF，再执行下载操作",
        Actions = new[] { "生成PDF" }
    };

    public static BusinessError SettlementPdfNoDownloadable => new()
    {
        Title = "没有可下载的结算PDF",
        Message = "没有可下载的结算PDF",
        Suggestion = "请确认所选结算单已生成PDF且文件仍存在",
        Actions = new[] { "查看结算单" }
    };

    // ==================== 分公司相关 ====================
    public static BusinessError BranchNotFound => EntityNotFound("分公司");

    public static BusinessError BranchHasEmployees => new()
    {
        Title = "分公司下有员工",
        Message = "该分公司下存在员工，无法删除",
        Suggestion = "请先将员工转移到其他分公司",
        Actions = new[] { "查看员工" }
    };

    public static BusinessError BranchCodeDuplicate => new()
    {
        Title = "分公司编码重复",
        Message = "分公司编码已存在",
        Suggestion = "请更换一个未使用的分公司编码",
        Actions = Array.Empty<string>()
    };

    // ==================== 员工相关 ====================
    public static BusinessError EmployeeNotFound => EntityNotFound("员工");

    public static BusinessError PasswordTooShort => new()
    {
        Title = "密码不符合要求",
        Message = "密码长度不能少于6位",
        Suggestion = "密码应包含字母和数字，长度6-20位",
        Actions = Array.Empty<string>()
    };

    public static BusinessError EmployeeNoDuplicate => new()
    {
        Title = "工号重复",
        Message = "工号已存在",
        Suggestion = "请更换一个未使用的员工工号",
        Actions = Array.Empty<string>()
    };

    // ==================== 权限相关 ====================
    public static BusinessError PermissionDenied(string action) => new()
    {
        Title = "权限不足",
        Message = $"您没有「{action}」的权限",
        Suggestion = "请联系管理员分配相应权限",
        Actions = new[] { "查看权限说明" }
    };

    public static BusinessError AccountLocked(int minutes) => new()
    {
        Title = "账号已锁定",
        Message = $"密码错误次数过多，账号已锁定 {minutes} 分钟",
        Suggestion = "请等待锁定时间结束后重试，或联系管理员解锁",
        Actions = new[] { "联系管理员" }
    };

    public static BusinessError PasswordExpired => new()
    {
        Title = "密码已过期",
        Message = "您的密码已超过90天未修改",
        Suggestion = "为保障账户安全，请立即修改密码",
        Actions = new[] { "修改密码", "稍后提醒" }
    };

    // ==================== 企微相关 ====================
    public static BusinessError WeChatTokenInvalid => new()
    {
        Title = "企微Token无效",
        Message = "企业微信访问令牌已失效",
        Suggestion = "请在系统设置中重新测试企微连接",
        Actions = new[] { "去设置", "重新测试" }
    };

    public static BusinessError WeChatSyncFailed(string reason) => new()
    {
        Title = "企微同步失败",
        Message = $"数据同步失败：{reason}",
        Suggestion = "请检查企微配置是否正确，或稍后重试",
        Actions = new[] { "查看配置", "重试同步" }
    };

    public static BusinessError WeChatConfigIncomplete => new()
    {
        Title = "企微配置不完整",
        Message = "企业微信配置信息不完整，无法完成操作",
        Suggestion = "请在系统设置中完善企微配置",
        Actions = new[] { "去设置" }
    };

    // ==================== 工作计划相关 ====================
    public static BusinessError WorkPlanNotFound => EntityNotFound("工作计划");

    public static BusinessError WorkScheduleNotFound => EntityNotFound("排班记录");

    public static BusinessError ScheduleDateInvalid => new()
    {
        Title = "日期无效",
        Message = "结束日期不能早于开始日期",
        Suggestion = "请重新选择日期范围",
        Actions = Array.Empty<string>()
    };

    // ==================== 导入导出相关 ====================
    public static BusinessError ExportDataTooLarge(int count) => new()
    {
        Title = "导出数据量过大",
        Message = $"当前有 {count} 条数据需要导出，可能导致系统卡顿",
        Suggestion = "建议缩小筛选范围后导出，或使用「快速导出」模式",
        Actions = new[] { "快速导出", "缩小范围" }
    };

    public static BusinessError ImportFormatError(string detail) => new()
    {
        Title = "导入格式错误",
        Message = $"导入文件格式不符合要求：{detail}",
        Suggestion = "请下载模板文件，按模板格式填写后重新导入",
        Actions = new[] { "下载模板", "查看帮助" }
    };

    // ==================== 草稿相关 ====================
    public static BusinessError DraftNotFound => EntityNotFound("草稿");

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 获取格式化的错误消息
    /// </summary>
    public static string FormatError(BusinessError error)
    {
        return error.Format();
    }

    /// <summary>
    /// 将 BusinessError 转为 ApiResponse 格式
    /// </summary>
    public static Application.DTOs.ApiResponse<T> ToApiResponse<T>(BusinessError error)
    {
        return Application.DTOs.ApiResponse<T>.Fail(error.Message);
    }
}

/// <summary>
/// 业务错误信息
/// </summary>
public class BusinessError
{
    /// <summary>错误标题</summary>
    public string Title { get; set; } = "";

    /// <summary>错误描述</summary>
    public string Message { get; set; } = "";

    /// <summary>处理建议</summary>
    public string? Suggestion { get; set; }

    /// <summary>可执行操作</summary>
    public string[] Actions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 格式化为用户友好的错误消息
    /// </summary>
    public string Format()
    {
        var result = $"{Title}\n{Message}";
        if (!string.IsNullOrEmpty(Suggestion))
            result += $"\n\n建议：{Suggestion}";
        return result;
    }

    /// <summary>
    /// 转为 ApiResponse 格式
    /// </summary>
    public Application.DTOs.ApiResponse<T> ToResponse<T>()
    {
        return Application.DTOs.ApiResponse<T>.Fail(Message);
    }

    /// <summary>
    /// 转为 ApiResponse 格式（无泛型参数）
    /// </summary>
    public Application.DTOs.ApiResponse<bool> ToResponse()
    {
        return Application.DTOs.ApiResponse<bool>.Fail(Message);
    }
}
