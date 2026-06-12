using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PRO.Desktop.ViewModels;

public partial class SettlementListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly ISettlementService _settlementService;
    private readonly IBranchService _branchService;
    private readonly SettlementOverviewService _overviewService;
    private readonly AuditService _auditService;

    protected override string EntityTypeName => "结算单";

    [ObservableProperty]
    private ObservableCollection<SettlementListItem> _settlements = [];

    [ObservableProperty]
    private SettlementDetailDto? _selectedSettlement;

    [ObservableProperty]
    private DateTime _filterStartDate = DateTime.Now.AddDays(-30);

    [ObservableProperty]
    private DateTime _filterEndDate = DateTime.Now;

    protected override bool HasActiveFilters() => SelectedBranchId.HasValue;

    [RelayCommand]
    private void ClearAllFilters()
    {
        SelectedBranchId = null;
        SearchKeyword = null;
        InvalidateCountCache();
        RunInBackground(ResetToFirstPageAndLoadAsync(), "清除筛选失败");
    }

    private ICommand? _clearFiltersCommand;
    protected override ICommand? ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearAllFilters);

    [ObservableProperty]
    private bool _isCreatingSettlement;

    [ObservableProperty]
    private int? _selectedBranchId;

    [ObservableProperty]
    private ObservableCollection<BranchListItem> _branches = [];

    [ObservableProperty]
    private SettlementOverviewDto? _overview;

    public SettlementListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _settlementService = App.Services.GetRequiredService<ISettlementService>();
        _branchService = App.Services.GetRequiredService<IBranchService>();
        _overviewService = App.Services.GetRequiredService<SettlementOverviewService>();
        _auditService = App.Services.GetRequiredService<AuditService>();

        RunInBackground(InitAsync(), "初始化应收账款失败");
    }

    private async Task InitAsync()
    {
        try
        {
            await LoadBranchesAsync();
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowBusinessException(ex, "加载结算数据"); ShowLoadFailedState(); }
    }

    private async Task LoadBranchesAsync()
    {
        var result = await _branchService.GetListAsync(new PagedRequest { PageIndex = 1, PageSize = 100 });
        if (result.Success && result.Data != null)
            Branches = new ObservableCollection<BranchListItem>(result.Data.Items);
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            int? branchId = CurrentSession.Current.IsHeadquartersAdmin ? SelectedBranchId : CurrentSession.CurrentBranchId;
            var request = new PagedRequest
            {
                PageIndex = PageIndex,
                PageSize = PageSize,
                Keyword = null
            };

            var result = await _settlementService.GetListAsync(request, branchId: branchId);

            if (result.Success && result.Data != null)
            {
                var filtered = result.Data.Items
                    .Where(s => s.CreatedAt >= FilterStartDate && s.CreatedAt <= FilterEndDate.AddDays(1))
                    .ToList();
                TotalCount = filtered.Count;
                Settlements = new ObservableCollection<SettlementListItem>(filtered);
                UpdateEmptyState();
            }

            await LoadOverviewAsync(branchId ?? CurrentSession.CurrentBranchId);
        }
        catch (Exception ex)
        {
            ShowError($"加载数据失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadOverviewAsync(int branchId)
    {
        var result = await _overviewService.GetOverviewAsync(branchId);
        if (result.Success && result.Data != null)
        {
            Overview = result.Data;
        }
    }

    [RelayCommand]
    private async Task NewSettlementAsync()
    {
        await CreateSettlementAsync();
    }

    [RelayCommand]
    private Task QueryAsync()
    {
        return ResetToFirstPageAndLoadAsync();
    }

    [RelayCommand]
    private async Task CreateSettlementAsync()
    {
        if (!CheckSettlementPermission("Create")) return;

        try
        {
            var branchId = SelectedBranchId ?? CurrentSession.CurrentBranchId;

            var orders = await _dbContext.Orders
                .Where(o => o.BranchId == branchId &&
                    o.Status == OrderStatus.Completed &&
                    o.SettlementId == null)
                .ToListAsync();

            if (orders.Count == 0)
            {
                MessageBox.Show("没有可结算的订单", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ConfirmAction(
                "创建结算",
                $"确认创建结算单？\n订单数：{orders.Count}\n总金额：¥{orders.Sum(o => o.TotalAmount):N2}\n未收款：¥{orders.Sum(o => o.TotalAmount - o.ReceivedAmount):N2}"))
            {
                return;
            }

            var settlement = new Settlement
            {
                SettlementNo = $"STL{DateTime.Now:yyyyMMddHHmmss}",
                BranchId = branchId,
                StartDate = orders.Min(o => o.CreatedAt),
                EndDate = orders.Max(o => o.CreatedAt),
                OrderCount = orders.Count,
                TotalAmount = orders.Sum(o => o.TotalAmount),
                ReceivedAmount = orders.Sum(o => o.ReceivedAmount),
                UnpaidAmount = orders.Sum(o => o.TotalAmount - o.ReceivedAmount),
                ConfirmedById = CurrentSession.CurrentEmployeeId,
                Status = SettlementStatus.Completed,
                CreatedAt = DateTime.Now
            };

            _dbContext.Settlements.Add(settlement);
            await _dbContext.SaveChangesAsync();

            foreach (var order in orders)
            {
                order.SettlementId = settlement.Id;
            }
            await _dbContext.SaveChangesAsync();

            var pdfPath = await GenerateSettlementPdfAsync(settlement, orders);
            settlement.PdfPath = pdfPath;
            await _dbContext.SaveChangesAsync();

            ShowSuccess($"结算成功，共 {orders.Count} 个订单。");
            await _auditService.LogSettlementCreateAsync(
                CurrentSession.CurrentEmployeeId,
                settlement.Id,
                settlement.SettlementNo,
                settlement.OrderCount,
                settlement.TotalAmount);
            IsCreatingSettlement = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowError($"结算失败: {ex.Message}");
        }
    }

    private async Task<string> GenerateSettlementPdfAsync(Settlement settlement, List<Order> orders)
    {
        var branch = await _dbContext.Branches.FindAsync(settlement.BranchId);
        var savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PRO结算单",
            $"{settlement.SettlementNo}.pdf");

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(branch?.Name ?? "分公司").FontSize(20).Bold();
                    col.Item().AlignCenter().Text("结算单").FontSize(16);
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Text($"结算单号：{settlement.SettlementNo}");
                        row.RelativeItem().Text($"结算日期：{settlement.CreatedAt:yyyy-MM-dd}");
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Border(1).Padding(10).Column(c =>
                        {
                            c.Item().Text("汇总信息").Bold();
                            c.Item().PaddingTop(5).Text($"结算期间：{settlement.StartDate:yyyy-MM-dd} 至 {settlement.EndDate:yyyy-MM-dd}");
                            c.Item().Text($"订单数量：{settlement.OrderCount}");
                            c.Item().Text($"订单总额：¥{settlement.TotalAmount:N2}");
                            c.Item().Text($"已收款：¥{settlement.ReceivedAmount:N2}");
                            c.Item().Text($"未收款：¥{settlement.UnpaidAmount:N2}");
                        });
                    });

                    col.Item().PaddingTop(20).Text("订单明细").Bold();
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("订单号").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("客户").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("金额").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("已收").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("状态").Bold();
                        });

                        foreach (var order in orders.Take(50))
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(order.OrderNo);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(order.Customer?.Name ?? "");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"¥{order.TotalAmount:N2}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"¥{order.ReceivedAmount:N2}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(order.PaymentStatus.ToString());
                        }
                    });

                    col.Item().PaddingTop(50).Row(row =>
                    {
                        row.RelativeItem().Text("确认人签字：________________");
                        row.RelativeItem().AlignRight().Text("日期：________________");
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("第 ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                    x.Span(" 页");
                });
            });
        }).GeneratePdf(savePath);

        return savePath;
    }

    [RelayCommand]
    private Task ViewSettlementAsync(SettlementListItem? settlement)
    {
        if (settlement == null) return Task.CompletedTask;

        if (!string.IsNullOrEmpty(settlement.PdfPath) && File.Exists(settlement.PdfPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = settlement.PdfPath,
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("PDF文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task DownloadPdfAsync(SettlementListItem? settlement)
    {
        if (settlement == null) return Task.CompletedTask;

        try
        {
            if (!string.IsNullOrEmpty(settlement.PdfPath) && File.Exists(settlement.PdfPath))
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "PDF文件|*.pdf",
                    FileName = $"结算单_{settlement.SettlementNo}"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.Copy(settlement.PdfPath, dialog.FileName, true);
                    ShowSuccess("下载成功");
                }
            }
            else
            {
                MessageBox.Show("PDF文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowError($"下载失败: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BatchDownloadPdfAsync()
    {
        try
        {
            var settlements = await _dbContext.Settlements
                .AsNoTracking()
                .Where(s => s.CreatedAt >= FilterStartDate && s.CreatedAt <= FilterEndDate.AddDays(1))
                .Where(s => !string.IsNullOrEmpty(s.PdfPath))
                .ToListAsync();

            var exportFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PRO结算单",
                "批量导出",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            Directory.CreateDirectory(exportFolder);

            var count = 0;
            foreach (var s in settlements)
            {
                if (!string.IsNullOrEmpty(s.PdfPath) && File.Exists(s.PdfPath))
                {
                    var destPath = Path.Combine(exportFolder, $"{s.SettlementNo}.pdf");
                    File.Copy(s.PdfPath, destPath, true);
                    count++;
                }
            }

            if (count > 0)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exportFolder,
                    UseShellExecute = true
                });
                ShowSuccess($"已导出 {count} 个结算 PDF 到 {exportFolder}");
            }
            else
            {
                ShowError("没有找到可导出的结算单PDF");
            }
        }
        catch (Exception ex)
        {
            ShowError($"下载失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelCreate()
    {
        IsCreatingSettlement = false;
    }
}
