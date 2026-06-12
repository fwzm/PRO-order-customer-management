using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PRO.Domain.Entities;
using PRO.Infrastructure.Persistence;
using Serilog;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 业务消息服务 - 将技术异常翻译为用户可理解的业务提示
/// 统一原则：界面显示"发生何事 + 如何处理"，技术堆栈写日志
/// 同时记录异常指标用于统计分析
/// </summary>
public class BusinessMessageService
{
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>异常翻译事件（用于统计记录）</summary>
    public event Action<BusinessErrorMetric>? OnErrorTranslated;

    public BusinessMessageService(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 翻译异常为业务消息
    /// </summary>
    public BusinessErrorMessage Translate(Exception exception, string? context = null)
    {
        // 记录完整技术日志（不包含敏感数据）
        Log.Error(exception, "业务异常: {Context}", context);

        var result = exception switch
        {
            DbUpdateConcurrencyException concEx => TranslateConcurrencyException(concEx),
            DbUpdateException dbEx => TranslateDbUpdateException(dbEx),
            NpgsqlException npgEx => TranslateNpgsqlException(npgEx),
            UnauthorizedAccessException => PermissionDenied(),
            InvalidOperationException invEx => TranslateInvalidOperation(invEx),
            TaskCanceledException => CancelledByUser(),
            TimeoutException => TimeoutError(),
            OperationCanceledException => CancelledByUser(),
            ArgumentException argEx => ValidationError(argEx.Message),
            HttpRequestException httpEx => TranslateHttpException(httpEx),
            _ => UnknownError(exception)
        };

        // 异步记录异常指标
        RecordErrorMetric(result, context);

        return result;
    }

    /// <summary>
    /// 翻译并返回可记录的日志消息（用于批量操作中记录每条失败原因）
    /// </summary>
    public string TranslateToShortMessage(Exception exception)
    {
        var bizMsg = Translate(exception);
        return $"{bizMsg.Title}: {bizMsg.UserMessage}";
    }

    private BusinessErrorMessage TranslateConcurrencyException(DbUpdateConcurrencyException ex)
    {
        return new BusinessErrorMessage
        {
            Title = "数据已被修改",
            UserMessage = "您操作的数据已被其他用户修改，请刷新后重试",
            Suggestion = "请刷新页面获取最新数据，然后重新操作",
            ErrorCode = "CONCURRENCY_CONFLICT",
            TechnicalDetail = ex.Message
        };
    }

    private BusinessErrorMessage TranslateHttpException(HttpRequestException ex)
    {
        return ex.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => PermissionDenied(),
            System.Net.HttpStatusCode.NotFound => DataNotFound(),
            System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.GatewayTimeout => TimeoutError(),
            System.Net.HttpStatusCode.ServiceUnavailable => new BusinessErrorMessage
            {
                Title = "服务暂时不可用",
                UserMessage = "服务器正在维护或繁忙，请稍后重试",
                Suggestion = "如果问题持续存在，请联系技术支持",
                ErrorCode = "SERVICE_UNAVAILABLE",
                TechnicalDetail = ex.Message
            },
            _ => NetworkError()
        };
    }

    private BusinessErrorMessage TranslateDbUpdateException(DbUpdateException ex)
    {
        var innerMessage = ex.InnerException?.Message ?? ex.Message;

        // 唯一约束冲突
        if (innerMessage.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
            innerMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            // 尝试提取字段名
            var field = ExtractFieldName(innerMessage);
            return new BusinessErrorMessage
            {
                Title = "数据已存在",
                UserMessage = $"该记录已存在，{field}不能重复",
                Suggestion = "请检查输入内容是否重复，或联系管理员",
                ErrorCode = "DUPLICATE_DATA",
                TechnicalDetail = innerMessage
            };
        }

        // 外键约束
        if (innerMessage.Contains("foreign key", StringComparison.OrdinalIgnoreCase) ||
            innerMessage.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase))
        {
            return new BusinessErrorMessage
            {
                Title = "数据关联错误",
                UserMessage = "该记录与其他数据存在关联，无法执行此操作",
                Suggestion = "请先删除或修改关联的数据，然后再重试",
                ErrorCode = "FOREIGN_KEY_VIOLATION",
                TechnicalDetail = innerMessage
            };
        }

        // 非空约束
        if (innerMessage.Contains("not-null", StringComparison.OrdinalIgnoreCase) ||
            innerMessage.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase))
        {
            return new BusinessErrorMessage
            {
                Title = "必填信息缺失",
                UserMessage = "有必填字段未填写，请检查后重试",
                Suggestion = "请确保所有标记为必填的字段都已填写",
                ErrorCode = "REQUIRED_FIELD_MISSING",
                TechnicalDetail = innerMessage
            };
        }

        return new BusinessErrorMessage
        {
            Title = "保存失败",
            UserMessage = "数据保存时发生错误，请稍后重试",
            Suggestion = "如果问题持续存在，请联系技术支持",
            ErrorCode = "DB_UPDATE_ERROR",
            TechnicalDetail = innerMessage
        };
    }

    private BusinessErrorMessage TranslateNpgsqlException(NpgsqlException ex)
    {
        if (ex.IsTransient)
        {
            return new BusinessErrorMessage
            {
                Title = "网络连接不稳定",
                UserMessage = "数据库连接暂时中断，正在自动重试...",
                Suggestion = "请稍等片刻，系统会自动重连。如果问题持续，请检查网络",
                ErrorCode = "DB_TRANSIENT_ERROR",
                TechnicalDetail = ex.Message
            };
        }

        return new BusinessErrorMessage
        {
            Title = "数据库连接失败",
            UserMessage = "无法连接到数据库，请检查网络或联系管理员",
            Suggestion = "请检查网络连接，或稍后重试",
            ErrorCode = "DB_CONNECTION_ERROR",
            TechnicalDetail = ex.Message
        };
    }

    private BusinessErrorMessage TranslateInvalidOperation(InvalidOperationException ex)
    {
        var message = ex.Message;

        if (message.Contains("并发") || message.Contains("concurrency", StringComparison.OrdinalIgnoreCase))
        {
            return ConcurrencyConflict();
        }

        if (message.Contains("库存不足") || message.Contains("stock", StringComparison.OrdinalIgnoreCase))
        {
            return InsufficientStock("", 0, 0);
        }

        if (message.Contains("状态") || message.Contains("status", StringComparison.OrdinalIgnoreCase))
        {
            return OrderStatusNotAllowed("", "");
        }

        return new BusinessErrorMessage
        {
            Title = "操作失败",
            UserMessage = "当前操作无法完成，请检查数据状态后重试",
            Suggestion = "请刷新页面获取最新数据，然后重试",
            ErrorCode = "INVALID_OPERATION",
            TechnicalDetail = message
        };
    }

    // ==================== 预定义业务消息 ====================

    public static BusinessErrorMessage DataNotFound(string entityName = "数据") => new()
    {
        Title = "数据不存在",
        UserMessage = $"找不到指定的{entityName}，可能已被删除",
        Suggestion = "请刷新列表后重试",
        ErrorCode = "NOT_FOUND"
    };

    public static BusinessErrorMessage DataAlreadyDeleted(string entityName = "数据") => new()
    {
        Title = "数据已删除",
        UserMessage = $"该{entityName}已被删除，无法执行操作",
        Suggestion = "请刷新列表查看最新状态",
        ErrorCode = "ALREADY_DELETED"
    };

    public static BusinessErrorMessage PermissionDenied(string? action = null) => new()
    {
        Title = "权限不足",
        UserMessage = action != null
            ? $"您没有「{action}」的权限"
            : "您没有执行此操作的权限",
        Suggestion = "请联系管理员分配相应权限",
        ErrorCode = "PERMISSION_DENIED"
    };

    public static BusinessErrorMessage ConcurrencyConflict() => new()
    {
        Title = "数据已被修改",
        UserMessage = "该数据已被其他用户修改，请刷新后重试",
        Suggestion = "请刷新页面获取最新数据，然后重新操作",
        ErrorCode = "CONCURRENCY_CONFLICT"
    };

    public static BusinessErrorMessage InsufficientStock(string productName, int available, int required) => new()
    {
        Title = "库存不足",
        UserMessage = $"产品「{productName}」库存不足（当前：{available}，需要：{required}）",
        Suggestion = "请减少数量或选择其他产品",
        ErrorCode = "INSUFFICIENT_STOCK"
    };

    public static BusinessErrorMessage OrderStatusNotAllowed(string currentStatus, string targetStatus) => new()
    {
        Title = "状态变更不允许",
        UserMessage = $"订单状态不允许从「{currentStatus}」变更为「{targetStatus}」",
        Suggestion = "请查看订单状态流转规则，选择正确的操作",
        ErrorCode = "ORDER_STATUS_NOT_ALLOWED"
    };

    public static BusinessErrorMessage CannotDeleteWithChildren(string entityName, string childName) => new()
    {
        Title = "无法删除",
        UserMessage = $"该{entityName}下存在关联的{childName}，无法直接删除",
        Suggestion = $"请先删除或转移关联的{childName}，然后再删除{entityName}",
        ErrorCode = "HAS_CHILDREN"
    };

    public static BusinessErrorMessage NetworkError() => new()
    {
        Title = "网络连接失败",
        UserMessage = "无法连接到服务器，请检查网络设置",
        Suggestion = "请检查网络连接，或稍后重试",
        ErrorCode = "NETWORK_ERROR"
    };

    public static BusinessErrorMessage TimeoutError() => new()
    {
        Title = "请求超时",
        UserMessage = "服务器响应超时，请稍后重试",
        Suggestion = "如果问题持续存在，请联系技术支持",
        ErrorCode = "TIMEOUT"
    };

    public static BusinessErrorMessage CancelledByUser() => new()
    {
        Title = "操作已取消",
        UserMessage = "操作已被取消",
        ErrorCode = "CANCELLED"
    };

    public static BusinessErrorMessage ValidationError(string detail) => new()
    {
        Title = "输入验证失败",
        UserMessage = "请检查输入信息是否完整和正确",
        Suggestion = detail,
        ErrorCode = "VALIDATION_ERROR"
    };

    public static BusinessErrorMessage ImportFormatError(string detail) => new()
    {
        Title = "导入文件格式错误",
        UserMessage = "导入文件格式不正确，请检查后重试",
        Suggestion = detail,
        ErrorCode = "IMPORT_FORMAT_ERROR"
    };

    public static BusinessErrorMessage ImportError(int rowNumber, string field, string detail) => new()
    {
        Title = "导入数据错误",
        UserMessage = $"第 {rowNumber} 行的「{field}」字段错误：{detail}",
        Suggestion = "请修正后重新导入",
        ErrorCode = "IMPORT_DATA_ERROR"
    };

    public static BusinessErrorMessage ExportFailed(string reason) => new()
    {
        Title = "导出失败",
        UserMessage = $"数据导出失败：{reason}",
        Suggestion = "请减少导出数量或稍后重试",
        ErrorCode = "EXPORT_FAILED"
    };

    private static BusinessErrorMessage UnknownError(Exception ex) => new()
    {
        Title = "系统异常",
        UserMessage = "系统遇到意外错误，请稍后重试",
        Suggestion = "如果问题持续存在，请联系技术支持并提供操作时间",
        ErrorCode = "UNKNOWN_ERROR",
        TechnicalDetail = ex.Message
    };

    private string ExtractFieldName(string message)
    {
        // 尝试从 PostgreSQL 错误消息中提取字段名
        // 例如: Key (Phone)=(13800138000) already exists
        var match = System.Text.RegularExpressions.Regex.Match(message, @"\((\w+)\)=\(");
        if (match.Success)
        {
            return match.Groups[1].Value switch
            {
                "Phone" => "手机号",
                "CustomerNo" => "客户编号",
                "OrderNo" => "订单号",
                "SKU" => "SKU编码",
                "EmployeeNo" => "工号",
                "Email" => "邮箱",
                _ => match.Groups[1].Value
            };
        }
        return "该信息";
    }

    // ==================== 异常指标记录 ====================

    /// <summary>
    /// 记录异常指标（异步，不影响主流程）
    /// </summary>
    private void RecordErrorMetric(BusinessErrorMessage bizMsg, string? context)
    {
        try
        {
            var module = ExtractModule(context);
            var operation = ExtractOperation(context);

            var metric = new BusinessErrorMetric
            {
                ErrorCode = bizMsg.ErrorCode,
                Module = module,
                Operation = operation,
                UserMessage = bizMsg.UserMessage,
                OccurredAt = DateTime.Now
            };

            // 触发事件（供监听器使用）
            OnErrorTranslated?.Invoke(metric);

            // 异步写入数据库
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider?.CreateScope();
                    var db = scope?.ServiceProvider.GetService<ProDbContext>();
                    if (db != null)
                    {
                        db.BusinessErrorMetrics.Add(metric);
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    // 指标记录失败不影响主流程
                    Log.Debug(ex, "异常指标记录失败（不影响业务）");
                }
            });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "构造异常指标失败");
        }
    }

    private static string ExtractModule(string? context)
    {
        if (string.IsNullOrEmpty(context)) return "General";
        if (context.Contains("订单") || context.Contains("Order")) return "Order";
        if (context.Contains("客户") || context.Contains("Customer")) return "Customer";
        if (context.Contains("产品") || context.Contains("Product")) return "Product";
        if (context.Contains("配送") || context.Contains("Delivery")) return "Delivery";
        if (context.Contains("结算") || context.Contains("Settlement")) return "Settlement";
        if (context.Contains("库存") || context.Contains("Inventory")) return "Inventory";
        if (context.Contains("导入") || context.Contains("Import")) return "Import";
        if (context.Contains("导出") || context.Contains("Export")) return "Export";
        if (context.Contains("登录") || context.Contains("Login")) return "Auth";
        return "General";
    }

    private static string ExtractOperation(string? context)
    {
        if (string.IsNullOrEmpty(context)) return "Unknown";
        if (context.Contains("创建") || context.Contains("Create")) return "Create";
        if (context.Contains("更新") || context.Contains("Update")) return "Update";
        if (context.Contains("删除") || context.Contains("Delete")) return "Delete";
        if (context.Contains("分配") || context.Contains("Assign")) return "Assign";
        if (context.Contains("导入") || context.Contains("Import")) return "Import";
        if (context.Contains("导出") || context.Contains("Export")) return "Export";
        if (context.Contains("确认") || context.Contains("Confirm")) return "Confirm";
        if (context.Contains("取消") || context.Contains("Cancel")) return "Cancel";
        if (context.Contains("登录") || context.Contains("Login")) return "Login";
        return "Unknown";
    }
}

/// <summary>
/// 业务错误消息
/// </summary>
public class BusinessErrorMessage
{
    public string Title { get; set; } = "";
    public string UserMessage { get; set; } = "";
    public string? Suggestion { get; set; }
    public string ErrorCode { get; set; } = "";
    public string? TechnicalDetail { get; set; }

    /// <summary>
    /// 格式化为完整提示
    /// </summary>
    public string Format()
    {
        var result = UserMessage;
        if (!string.IsNullOrEmpty(Suggestion))
            result += $"\n\n{Suggestion}";
        return result;
    }
}
