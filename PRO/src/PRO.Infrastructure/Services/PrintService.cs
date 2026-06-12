using Microsoft.EntityFrameworkCore;
using PRO.Application.DTOs;
using PRO.Infrastructure.Persistence;
using System.Text;

namespace PRO.Infrastructure.Services;

/// <summary>
/// 打印服务 - 生成打印内容
/// </summary>
public class PrintService
{
    private readonly ProDbContext _dbContext;

    public PrintService(ProDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 生成订单打印数据
    /// </summary>
    public async Task<ApiResponse<PrintDataDto>> GenerateOrderPrintAsync(int orderId, int? templateId = null)
    {
        try
        {
            var order = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Include(o => o.Branch)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return ApiResponse<PrintDataDto>.Fail("订单不存在");

            var data = new OrderPrintData
            {
                OrderNo = order.OrderNo,
                CustomerName = order.Customer?.Name ?? "",
                CustomerPhone = order.Customer?.Phone ?? "",
                DeliveryAddress = order.DeliveryAddress ?? "",
                OrderDate = order.CreatedAt,
                DeliveryDate = order.DeliveryTime,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                ReceivableAmount = order.TotalAmount - order.DiscountAmount,
                Remark = order.Remark,
                CompanyName = "PRO企业管理系统",
                Items = order.Items.Select((item, index) => new OrderItemPrintData
                {
                    Index = index + 1,
                    ProductName = item.Product?.Name ?? "",
                    Specification = item.Product?.Specification,
                    Quantity = item.Quantity,
                    Unit = item.Product?.Unit ?? "",
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount
                }).ToList()
            };

            var html = GenerateOrderHtml(data);

            return ApiResponse<PrintDataDto>.Ok(new PrintDataDto
            {
                TemplateName = "订单打印",
                HtmlContent = html
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PrintDataDto>.Fail($"生成打印数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成结算单打印数据
    /// </summary>
    public async Task<ApiResponse<PrintDataDto>> GenerateSettlementPrintAsync(int settlementId)
    {
        try
        {
            var settlement = await _dbContext.Settlements
                .AsNoTracking()
                .Include(s => s.Branch)
                .Include(s => s.Orders)
                .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(s => s.Id == settlementId);

            if (settlement == null)
                return ApiResponse<PrintDataDto>.Fail("结算单不存在");

            var data = new SettlementPrintData
            {
                SettlementNo = settlement.SettlementNo,
                BranchName = settlement.Branch?.Name ?? "",
                StartDate = settlement.StartDate,
                EndDate = settlement.EndDate,
                OrderCount = settlement.OrderCount,
                TotalAmount = settlement.TotalAmount,
                ReceivedAmount = settlement.ReceivedAmount,
                UnpaidAmount = settlement.UnpaidAmount,
                Remark = settlement.Remark,
                PrintDate = DateTime.Now,
                PrintedBy = "系统",
                Orders = settlement.Orders.Select((order, index) => new SettlementOrderPrintData
                {
                    Index = index + 1,
                    OrderNo = order.OrderNo,
                    CustomerName = order.Customer?.Name ?? "",
                    OrderDate = order.CreatedAt,
                    TotalAmount = order.TotalAmount,
                    ReceivedAmount = order.ReceivedAmount,
                    UnpaidAmount = order.TotalAmount - order.ReceivedAmount,
                    PaymentStatus = order.PaymentStatus.ToString()
                }).ToList()
            };

            var html = GenerateSettlementHtml(data);

            return ApiResponse<PrintDataDto>.Ok(new PrintDataDto
            {
                TemplateName = "结算单打印",
                HtmlContent = html
            });
        }
        catch (Exception ex)
        {
            return ApiResponse<PrintDataDto>.Fail($"生成打印数据失败: {ex.Message}");
        }
    }

    private string GenerateOrderHtml(OrderPrintData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Microsoft YaHei', sans-serif; padding: 20px; }");
        sb.AppendLine(".header { text-align: center; margin-bottom: 20px; }");
        sb.AppendLine(".title { font-size: 24px; font-weight: bold; }");
        sb.AppendLine(".info { margin: 10px 0; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
        sb.AppendLine("th, td { border: 1px solid #333; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background-color: #f0f0f0; }");
        sb.AppendLine(".total { text-align: right; font-size: 18px; font-weight: bold; margin-top: 20px; }");
        sb.AppendLine(".footer { margin-top: 30px; }");
        sb.AppendLine("@media print { body { padding: 0; } }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<div class='title'>{data.CompanyName}</div>");
        sb.AppendLine("<div>销 售 订 单</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='info'>");
        sb.AppendLine($"<div>订单号：{data.OrderNo}</div>");
        sb.AppendLine($"<div>客户：{data.CustomerName}  电话：{data.CustomerPhone}</div>");
        sb.AppendLine($"<div>配送地址：{data.DeliveryAddress}</div>");
        sb.AppendLine($"<div>下单时间：{data.OrderDate:yyyy-MM-dd HH:mm}</div>");
        if (data.DeliveryDate.HasValue)
            sb.AppendLine($"<div>配送时间：{data.DeliveryDate:yyyy-MM-dd HH:mm}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>序号</th><th>产品名称</th><th>规格</th><th>数量</th><th>单位</th><th>单价</th><th>金额</th></tr>");
        foreach (var item in data.Items)
        {
            sb.AppendLine($"<tr><td>{item.Index}</td><td>{item.ProductName}</td><td>{item.Specification}</td>");
            sb.AppendLine($"<td>{item.Quantity}</td><td>{item.Unit}</td><td>¥{item.UnitPrice:N2}</td><td>¥{item.Amount:N2}</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine($"<div class='total'>合计：¥{data.TotalAmount:N2}</div>");
        if (data.DiscountAmount > 0)
            sb.AppendLine($"<div class='total'>优惠：¥{data.DiscountAmount:N2}</div>");
        sb.AppendLine($"<div class='total'>应收：¥{data.ReceivableAmount:N2}</div>");

        if (!string.IsNullOrEmpty(data.Remark))
            sb.AppendLine($"<div class='info'>备注：{data.Remark}</div>");

        sb.AppendLine("<div class='footer'>");
        sb.AppendLine($"<div>打印时间：{DateTime.Now:yyyy-MM-dd HH:mm}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string GenerateSettlementHtml(SettlementPrintData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Microsoft YaHei', sans-serif; padding: 20px; }");
        sb.AppendLine(".header { text-align: center; margin-bottom: 20px; }");
        sb.AppendLine(".title { font-size: 24px; font-weight: bold; }");
        sb.AppendLine(".info { margin: 10px 0; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
        sb.AppendLine("th, td { border: 1px solid #333; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background-color: #f0f0f0; }");
        sb.AppendLine(".summary { margin: 20px 0; padding: 15px; background-color: #f9f9f9; }");
        sb.AppendLine("@media print { body { padding: 0; } }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<div class='header'>");
        sb.AppendLine("<div class='title'>结 算 单</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='info'>");
        sb.AppendLine($"<div>结算单号：{data.SettlementNo}</div>");
        sb.AppendLine($"<div>分公司：{data.BranchName}</div>");
        sb.AppendLine($"<div>结算期间：{data.StartDate:yyyy-MM-dd} 至 {data.EndDate:yyyy-MM-dd}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='summary'>");
        sb.AppendLine($"<div>订单数量：{data.OrderCount}</div>");
        sb.AppendLine($"<div>订单总额：¥{data.TotalAmount:N2}</div>");
        sb.AppendLine($"<div>已收金额：¥{data.ReceivedAmount:N2}</div>");
        sb.AppendLine($"<div>未收金额：¥{data.UnpaidAmount:N2}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>序号</th><th>订单号</th><th>客户</th><th>下单时间</th><th>金额</th><th>已收</th><th>未收</th><th>状态</th></tr>");
        foreach (var order in data.Orders)
        {
            sb.AppendLine($"<tr><td>{order.Index}</td><td>{order.OrderNo}</td><td>{order.CustomerName}</td>");
            sb.AppendLine($"<td>{order.OrderDate:yyyy-MM-dd}</td><td>¥{order.TotalAmount:N2}</td>");
            sb.AppendLine($"<td>¥{order.ReceivedAmount:N2}</td><td>¥{order.UnpaidAmount:N2}</td><td>{order.PaymentStatus}</td></tr>");
        }
        sb.AppendLine("</table>");

        if (!string.IsNullOrEmpty(data.Remark))
            sb.AppendLine($"<div class='info'>备注：{data.Remark}</div>");

        sb.AppendLine("<div class='footer'>");
        sb.AppendLine($"<div>打印人：{data.PrintedBy}  打印时间：{data.PrintDate:yyyy-MM-dd HH:mm}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// 批量生成打印数据
    /// </summary>
    public async Task<ApiResponse<List<PrintDataDto>>> BatchGeneratePrintAsync(string type, List<int> ids)
    {
        var results = new List<PrintDataDto>();

        foreach (var id in ids)
        {
            var result = type.ToLower() switch
            {
                "order" => await GenerateOrderPrintAsync(id),
                "settlement" => await GenerateSettlementPrintAsync(id),
                _ => ApiResponse<PrintDataDto>.Fail($"不支持的打印类型：{type}")
            };

            if (result.Success && result.Data != null)
                results.Add(result.Data);
        }

        return ApiResponse<List<PrintDataDto>>.Ok(results);
    }
}
