using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Entities;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Infrastructure.WeChat;
using PRO.Infrastructure.Common;
using PRO.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PRO.Desktop.ViewModels;

public partial class ProductListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<ProductListItem> _products = new();

    [ObservableProperty]
    private ProductListItem? _selectedProduct;

    [ObservableProperty]
    private ProductStatus? _filterStatus;

    [ObservableProperty]
    private int? _filterCategoryId;

    [ObservableProperty]
    private ObservableCollection<ProductCategoryDto> _categories = new();

    public ObservableCollection<ProductCategoryDto> CategoriesWithAll
    {
        get
        {
            var list = new ObservableCollection<ProductCategoryDto>
            {
                new() { Id = 0, Name = "全部分类" }
            };
            foreach (var c in Categories)
                list.Add(c);
            return list;
        }
    }

    public ProductListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            await LoadCategoriesAsync();
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError($"产品加载失败: {ex.Message}"); }
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _dbContext.ProductCategories.AsNoTracking().ToListAsync();
        Categories = new ObservableCollection<ProductCategoryDto>(
            categories.Select(c => new ProductCategoryDto { Id = c.Id, Name = c.Name }));
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var query = _dbContext.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                query = query.Where(p => p.Name.Contains(SearchKeyword) || p.SKU.Contains(SearchKeyword));
            }

            if (FilterStatus.HasValue)
            {
                query = query.Where(p => p.Status == FilterStatus.Value);
            }

            if (FilterCategoryId.HasValue && FilterCategoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == FilterCategoryId.Value);
            }

            TotalCount = await query.CountAsync();

            var items = await query.OrderBy(p => p.SKU)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            Products = new ObservableCollection<ProductListItem>(items.Select(p => new ProductListItem
            {
                Id = p.Id,
                SKU = p.SKU,
                Name = p.Name,
                Specification = p.Specification,
                AverageSalePrice = p.AverageSalePrice,
                ReferencePrice = p.ReferencePrice,
                Stock = p.Stock,
                Unit = p.Unit,
                CategoryName = p.Category?.Name,
                Status = p.Status,
                UpdatedAt = p.UpdatedAt
            }));
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

    [RelayCommand]
    private void NewProduct()
    {
        var editVm = App.Services.GetService(typeof(ProductEditViewModel)) as ProductEditViewModel
            ?? throw new InvalidOperationException("无法创建产品编辑视图模型");
        
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };
        
        var dialog = new Views.ProductEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void EditProduct(ProductListItem? product)
    {
        if (product == null) return;
        
        var editVm = App.Services.GetService(typeof(ProductEditViewModel)) as ProductEditViewModel
            ?? throw new InvalidOperationException("无法创建产品编辑视图模型");
        
        editVm.LoadProduct(product.Id);
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };
        
        var dialog = new Views.ProductEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        try
        {
            var products = await _dbContext.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .ToListAsync();

            var dialog = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                FileName = $"产品数据_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("产品列表");

                var headers = new[] { "SKU", "名称", "规格", "参考价", "销售均价", "库存", "单位", "分类", "状态" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                var row = 2;
                foreach (var p in products)
                {
                    worksheet.Cell(row, 1).Value = p.SKU;
                    worksheet.Cell(row, 2).Value = p.Name;
                    worksheet.Cell(row, 3).Value = p.Specification;
                    worksheet.Cell(row, 4).Value = p.ReferencePrice;
                    worksheet.Cell(row, 5).Value = p.AverageSalePrice;
                    worksheet.Cell(row, 6).Value = p.Stock;
                    worksheet.Cell(row, 7).Value = p.Unit;
                    worksheet.Cell(row, 8).Value = p.Category?.Name;
                    worksheet.Cell(row, 9).Value = p.Status == ProductStatus.Active ? "启用" : "停用";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);
                ShowSuccess($"导出成功，共 {products.Count} 条记录");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
    }
}

public partial class DeliveryPersonListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<DeliveryPersonListItem> _deliveryPersons = new();

    [ObservableProperty]
    private DeliveryPersonListItem? _selectedDeliveryPerson;

    [ObservableProperty]
    private DeliveryPersonStatus? _filterStatus;

    [ObservableProperty]
    private ObservableCollection<OrderListItem> _pendingOrders = new();

    public DeliveryPersonListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _ = LoadDataAsync();
    }

    partial void OnFilterStatusChanged(DeliveryPersonStatus? value)
    {
        _ = ResetToFirstPageAndLoadAsync();
    }

    protected override async Task LoadDataAsync()

    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _dbContext.DeliveryPersons.AsNoTracking().Include(d => d.Branch).AsQueryable();

            if (!CurrentSession.Current.IsHeadquartersAdmin)
            {
                query = query.Where(d => d.BranchId == branchId);
            }

            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                query = query.Where(d => d.Name.Contains(SearchKeyword) || d.Phone.Contains(SearchKeyword));
            }

            if (FilterStatus.HasValue)
            {
                query = query.Where(d => d.Status == FilterStatus.Value);
            }

            TotalCount = await query.CountAsync();

            var items = await query.OrderBy(d => d.Name)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            DeliveryPersons = new ObservableCollection<DeliveryPersonListItem>(items.Select(d => new DeliveryPersonListItem
            {
                Id = d.Id,
                Name = d.Name,
                Phone = d.Phone,
                BranchId = d.BranchId,
                BranchName = d.Branch?.Name ?? "",
                ServiceArea = d.ServiceArea,
                VehicleNumber = d.VehicleNumber,
                WeChatId = d.WeChatId,
                CurrentLoad = d.CurrentLoad,
                MaxLoad = d.MaxLoad,
                Status = d.Status,
                StatusName = d.Status switch
                {
                    DeliveryPersonStatus.Available => "可用",
                    DeliveryPersonStatus.Busy => "忙碌",
                    DeliveryPersonStatus.Off => "休息",
                    _ => "未知"
                }
            }));
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

    [RelayCommand]
    private void NewDeliveryPerson()
    {
        var editVm = App.Services.GetService(typeof(DeliveryPersonEditViewModel)) as DeliveryPersonEditViewModel
            ?? throw new InvalidOperationException("无法创建配送员编辑视图模型");
        
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };
        
        var dialog = new Views.DeliveryPersonEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void EditDeliveryPerson(DeliveryPersonListItem? person)
    {
        if (person == null) return;
        
        var editVm = App.Services.GetService(typeof(DeliveryPersonEditViewModel)) as DeliveryPersonEditViewModel
            ?? throw new InvalidOperationException("无法创建配送员编辑视图模型");
        
        editVm.LoadDeliveryPerson(person.Id);
        editVm.OnSaveCompleted = async () => { await LoadDataAsync(); };
        
        var dialog = new Views.DeliveryPersonEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task LoadPendingOrdersAsync()
    {
        var branchId = CurrentSession.CurrentBranchId;
        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.BranchId == branchId && o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt)
            .Take(50)
            .ToListAsync();

        PendingOrders = new ObservableCollection<OrderListItem>(orders.Select(o => new OrderListItem
        {
            Id = o.Id,
            OrderNo = o.OrderNo,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.Name ?? "",
            TotalAmount = o.TotalAmount,
            DeliveryAddress = o.DeliveryAddress,
            DeliveryLongitude = o.DeliveryLongitude,
            DeliveryLatitude = o.DeliveryLatitude,
            CreatedAt = o.CreatedAt
        }));
    }

    [RelayCommand]
    private Task DeleteDeliveryPersonAsync(DeliveryPersonListItem? person)
    {
        if (person == null)
        {
            return Task.CompletedTask;
        }

        MessageBox.Show("当前基础修复版未扩展删除流程，如需停用配送员，请进入编辑页调整状态。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ManualAssignAsync(OrderListItem? order)
    {
        if (order == null) return Task.CompletedTask;
        
        MessageBox.Show("当前基础修复版保留了入口，请在订单管理页面执行人工分配。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task AutoAssignAsync()
    {
        MessageBox.Show("自动分配仍属于业务能力，当前基础修复版仅保留入口，不继续扩展。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }


    [RelayCommand]
    private async Task ExportForGaodeAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.BranchId == branchId && 
                    o.Status == OrderStatus.Assigned && 
                    o.DeliveryLongitude.HasValue && o.DeliveryLatitude.HasValue)
                .ToListAsync();

            if (orders.Count == 0)
            {
                MessageBox.Show("没有可导出的配送订单", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                FileName = $"高德路径规划_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("配送路线");

                var headers = new[] { "名称", "*经度", "*纬度", "*地址", "颜色", "图标(外轮廓)", "图标(填充物)", "描述", "文件夹" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                var today = DateTime.Now.ToString("yyMMdd");
                var branchName = (await _dbContext.Branches.FindAsync(branchId))?.Name ?? "分公司";
                var groupPrefix = $"{today}{branchName}配送";

                var row = 2;
                var groupNum = 1;
                var itemsInGroup = 0;
                
                foreach (var o in orders.OrderBy(o => o.DeliveryTime))
                {
                    if (itemsInGroup > 0 && itemsInGroup % 20 == 0) groupNum++;

                    worksheet.Cell(row, 1).Value = o.Customer?.Name ?? o.OrderNo;
                    worksheet.Cell(row, 2).Value = o.DeliveryLongitude ?? 0;
                    worksheet.Cell(row, 3).Value = o.DeliveryLatitude ?? 0;
                    worksheet.Cell(row, 4).Value = o.DeliveryAddress;
                    worksheet.Cell(row, 8).Value = $"{o.OrderNo}\n客户：{o.Customer?.Name}\n金额：¥{o.TotalAmount}";
                    worksheet.Cell(row, 9).Value = $"{groupPrefix}/订单组{groupNum}";
                    
                    row++;
                    itemsInGroup++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);
                ShowSuccess($"高德路径规划导出成功，共 {orders.Count} 个配送点");
            }
        }
        catch (Exception ex)
        {
            ShowError($"导出失败: {ex.Message}");
        }
    }
}

public partial class SettlementListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<SettlementListItem> _settlements = new();

    [ObservableProperty]
    private SettlementDetailDto? _selectedSettlement;

    [ObservableProperty]
    private DateTime _filterStartDate = DateTime.Now.AddDays(-30);

    [ObservableProperty]
    private DateTime _filterEndDate = DateTime.Now;

    [ObservableProperty]
    private bool _isCreatingSettlement;

    [ObservableProperty]
    private int? _selectedBranchId;

    [ObservableProperty]
    private ObservableCollection<BranchListItem> _branches = new();

    public SettlementListViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            await LoadBranchesAsync();
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError($"结算加载失败: {ex.Message}"); }
    }

    private async Task LoadBranchesAsync()
    {
        var branches = await _dbContext.Branches.AsNoTracking().ToListAsync();
        Branches = new ObservableCollection<BranchListItem>(
            branches.Select(b => new BranchListItem { Id = b.Id, Name = b.Name }));
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var query = _dbContext.Settlements
                .AsNoTracking()
                .Include(s => s.Branch)
                .Include(s => s.ConfirmedBy)
                .Where(s => s.CreatedAt >= FilterStartDate && s.CreatedAt <= FilterEndDate.AddDays(1));

            if (!CurrentSession.Current.IsHeadquartersAdmin)
            {
                query = query.Where(s => s.BranchId == branchId);
            }
            else if (SelectedBranchId.HasValue)
            {
                query = query.Where(s => s.BranchId == SelectedBranchId.Value);
            }

            TotalCount = await query.CountAsync();

            var items = await query.OrderByDescending(s => s.CreatedAt)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            Settlements = new ObservableCollection<SettlementListItem>(items.Select(s => new SettlementListItem
            {
                Id = s.Id,
                SettlementNo = s.SettlementNo,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                BranchId = s.BranchId,
                BranchName = s.Branch?.Name ?? "",
                ConfirmedByName = s.ConfirmedBy?.Name ?? "",
                OrderCount = s.OrderCount,
                TotalAmount = s.TotalAmount,
                ReceivedAmount = s.ReceivedAmount,
                UnpaidAmount = s.UnpaidAmount,
                Status = s.Status,
                PdfPath = s.PdfPath,
                CreatedAt = s.CreatedAt
            }));
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

    [RelayCommand]
    private async Task NewSettlementAsync()
    {
        // 直接触发结算创建
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

            // 更新订单关联
            foreach (var order in orders)
            {
                order.SettlementId = settlement.Id;
            }
            await _dbContext.SaveChangesAsync();

            // 生成PDF
            var pdfPath = await GenerateSettlementPdfAsync(settlement, orders);
            settlement.PdfPath = pdfPath;
            await _dbContext.SaveChangesAsync();

            ShowSuccess($"结算成功，共 {orders.Count} 个订单。");
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
                    // 汇总信息
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

                    // 订单明细
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

                    // 签字区
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
        
        // 打开PDF
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

public partial class WorkScheduleViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    [ObservableProperty]
    private int _selectedMonth;

    [ObservableProperty]
    private int _selectedYear;

    [ObservableProperty]
    private ObservableCollection<CalendarScheduleItem> _calendarItems = new();

    [ObservableProperty]
    private DailyPlanView? _currentDailyPlan;

    [ObservableProperty]
    private ObservableCollection<EmployeeListItem> _employees = new();

    [ObservableProperty]
    private EmployeeListItem? _selectedEmployee;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private ObservableCollection<PlanDraftItem> _drafts = new();

    [ObservableProperty]
    private ObservableCollection<WeeklyScheduleRow> _weeklyScheduleRows = new();

    [ObservableProperty]
    private DateTime _weekStartDate = DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek + 1);

    public WorkScheduleViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _selectedMonth = DateTime.Now.Month;
        _selectedYear = DateTime.Now.Year;
        SelectedEmployee = Employees.FirstOrDefault(e => e.Id == CurrentSession.CurrentEmployeeId);

        _ = LoadScheduleInitAsync();
    }

    private async Task LoadScheduleInitAsync()
    {
        try
        {
            await LoadEmployeesAsync();
            await LoadCalendarAsync();
            await LoadDraftsAsync();
            await LoadWeeklyScheduleAsync();
        }
        catch (Exception ex) { ShowError($"工作计划加载失败: {ex.Message}"); }
    }

    partial void OnSelectedEmployeeChanged(EmployeeListItem? value)
    {
        _ = ReloadScheduleContextAsync();
    }

    private async Task ReloadScheduleContextAsync()
    {
        await LoadCalendarAsync();
        await LoadDailyPlanAsync(SelectedDate);
    }

    private async Task LoadEmployeesAsync()

    {
        var branchId = CurrentSession.CurrentBranchId;
        var query = _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Role)
            .Where(e => e.BranchId == branchId && e.Status == EmployeeStatus.Active);

        if (!CurrentSession.IsAdmin)
        {
            query = query.Where(e => e.Id == CurrentSession.CurrentEmployeeId);
        }

        var employees = await query.ToListAsync();

        Employees = new ObservableCollection<EmployeeListItem>(employees.Select(e => new EmployeeListItem
        {
            Id = e.Id,
            Name = e.Name,
            EmployeeNo = e.EmployeeNo,
            DepartmentName = e.Department?.Name ?? "",
            RoleName = e.Role?.Name ?? "",
            Status = e.Status
        }));

        if (SelectedEmployee == null && Employees.Any())
        {
            SelectedEmployee = Employees.First();
        }
    }

    [RelayCommand]
    private async Task LoadCalendarAsync()
    {
        IsLoading = true;
        try
        {
            var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;
            var startDate = new DateTime(SelectedYear, SelectedMonth, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var schedules = await _dbContext.WorkSchedules
                .AsNoTracking()
                .Where(s => s.EmployeeId == employeeId && s.ScheduleDate >= startDate && s.ScheduleDate <= endDate)
                .ToListAsync();

            CalendarItems.Clear();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var schedule = schedules.FirstOrDefault(s => s.ScheduleDate.Date == date.Date);
                CalendarItems.Add(new CalendarScheduleItem
                {
                    Date = date,
                    IsWorkday = schedule != null,
                    WorkStartTime = schedule?.WorkStartTime.ToString(@"hh\:mm"),
                    WorkEndTime = schedule?.WorkEndTime.ToString(@"hh\:mm"),
                    TotalWorkMinutes = schedule?.TotalWorkMinutes ?? 0,
                    ScheduleType = schedule?.ScheduleType ?? "Rest",
                    HasChanged = schedule?.HasChanged ?? false
                });
            }
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

    private async Task LoadWeeklyScheduleAsync()
    {
        try
        {
            var branchId = CurrentSession.CurrentBranchId;
            var employees = await _dbContext.Employees
                .AsNoTracking()
                .Where(e => e.BranchId == branchId && e.Status == EmployeeStatus.Active)
                .OrderBy(e => e.Name)
                .ToListAsync();

            var weekEnd = WeekStartDate.AddDays(7);
            var schedules = await _dbContext.WorkSchedules
                .AsNoTracking()
                .Where(s => employees.Select(e => e.Id).Contains(s.EmployeeId)
                    && s.ScheduleDate >= WeekStartDate && s.ScheduleDate < weekEnd)
                .ToListAsync();

            var rows = new ObservableCollection<WeeklyScheduleRow>();
            foreach (var emp in employees)
            {
                var row = new WeeklyScheduleRow { EmployeeName = emp.Name };
                for (int i = 0; i < 7; i++)
                {
                    var day = WeekStartDate.AddDays(i);
                    var sch = schedules.FirstOrDefault(s => s.EmployeeId == emp.Id && s.ScheduleDate.Date == day.Date);
                    row.Days[i] = sch != null
                        ? $"{sch.WorkStartTime:hh\\:mm}-{sch.WorkEndTime:hh\\:mm}"
                        : "休息";
                }
                rows.Add(row);
            }
            WeeklyScheduleRows = rows;
        }
        catch (Exception ex) { ShowError($"加载周视图失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        if (SelectedMonth == 1)
        {
            SelectedMonth = 12;
            SelectedYear--;
        }
        else
        {
            SelectedMonth--;
        }

        var day = Math.Min(SelectedDate.Day, DateTime.DaysInMonth(SelectedYear, SelectedMonth));
        await LoadDailyPlanAsync(new DateTime(SelectedYear, SelectedMonth, day));
        await LoadCalendarAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        if (SelectedMonth == 12)
        {
            SelectedMonth = 1;
            SelectedYear++;
        }
        else
        {
            SelectedMonth++;
        }

        var day = Math.Min(SelectedDate.Day, DateTime.DaysInMonth(SelectedYear, SelectedMonth));
        await LoadDailyPlanAsync(new DateTime(SelectedYear, SelectedMonth, day));
        await LoadCalendarAsync();
    }

    [RelayCommand]
    private async Task LoadDailyPlanAsync(DateTime? date)
    {
        if (!date.HasValue) return;

        SelectedDate = date.Value;
        var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;


        var schedule = await _dbContext.WorkSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.ScheduleDate.Date == date.Value.Date);

        var plans = await _dbContext.WorkPlans
            .AsNoTracking()
            .Include(p => p.Customer)
            .Where(p => p.EmployeeId == employeeId && p.PlanDate.Date == date.Value.Date)
            .ToListAsync();

        CurrentDailyPlan = new DailyPlanView
        {
            Date = date.Value,
            EmployeeId = employeeId,
            EmployeeName = SelectedEmployee?.Name ?? "",
            Schedule = schedule != null ? new WorkScheduleListItem
            {
                Id = schedule.Id,
                EmployeeId = schedule.EmployeeId,
                ScheduleDate = schedule.ScheduleDate,
                WorkStartTime = schedule.WorkStartTime,
                WorkEndTime = schedule.WorkEndTime,
                BreakStartTime = schedule.BreakStartTime,
                BreakEndTime = schedule.BreakEndTime,
                TotalWorkMinutes = schedule.TotalWorkMinutes,
                ScheduleType = schedule.ScheduleType,
                HasChanged = schedule.HasChanged
            } : null,
            Plans = plans.Select(p => new WorkPlanDto
            {
                Id = p.Id,
                EmployeeId = p.EmployeeId,
                PlanDate = p.PlanDate,
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                DurationMinutes = p.DurationMinutes,
                Content = p.Content,
                CustomerId = p.CustomerId,
                CustomerName = p.Customer?.Name,
                PlanType = p.PlanType,
                ExecutionStatus = p.ExecutionStatus
            }).ToList(),
            TotalWorkMinutes = schedule?.TotalWorkMinutes ?? 0,
            TotalPlanMinutes = plans.Sum(p => p.DurationMinutes)
        };
    }

    private async Task LoadDraftsAsync()
    {
        var employeeId = CurrentSession.CurrentEmployeeId;
        var drafts = await _dbContext.PlanDrafts
            .AsNoTracking()
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        Drafts = new ObservableCollection<PlanDraftItem>(drafts.Select(d => new PlanDraftItem
        {
            Id = d.Id,
            Title = d.Content.Length > 50 ? d.Content.Substring(0, 50) + "..." : d.Content,
            Content = d.Content,
            DraftType = d.DraftType,
            CreatedAt = d.CreatedAt
        }));
    }

    [RelayCommand]
    private async Task CreateScheduleAsync()
    {
        var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;
        if (employeeId == 0) { ShowError("请先选择员工"); return; }

        try
        {
            var existing = await _dbContext.WorkSchedules
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.ScheduleDate.Date == SelectedDate.Date);

            if (existing == null)
            {
                _dbContext.WorkSchedules.Add(new WorkSchedule
                {
                    EmployeeId = employeeId,
                    BranchId = CurrentSession.CurrentBranchId,
                    ScheduleDate = SelectedDate.Date,
                    WorkStartTime = new TimeSpan(9, 0, 0),
                    WorkEndTime = new TimeSpan(18, 0, 0),
                    TotalWorkMinutes = 480,
                    ScheduleType = "Normal",
                    HasChanged = true,
                    CreatedById = CurrentSession.CurrentEmployeeId,
                    CreatedAt = DateTime.Now
                });
                await _dbContext.SaveChangesAsync();
                ShowSuccess("排班已创建");
            }
            else
            {
                ShowSuccess("该日期已有排班，请在右侧编辑");
            }

            await LoadCalendarAsync();
            await LoadDailyPlanAsync(SelectedDate);
        }
        catch (Exception ex) { ShowError($"创建排班失败: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (CurrentDailyPlan?.Schedule == null) return;

        try
        {
            var schedule = CurrentDailyPlan.Schedule;
            var existing = await _dbContext.WorkSchedules
                .FirstOrDefaultAsync(s => s.EmployeeId == schedule.EmployeeId && s.ScheduleDate.Date == schedule.ScheduleDate.Date);

            if (existing != null)
            {
                existing.WorkStartTime = schedule.WorkStartTime;
                existing.WorkEndTime = schedule.WorkEndTime;
                existing.BreakStartTime = schedule.BreakStartTime;
                existing.BreakEndTime = schedule.BreakEndTime;
                existing.TotalWorkMinutes = schedule.TotalWorkMinutes;
                existing.ScheduleType = schedule.ScheduleType;
                existing.HasChanged = true;
            }
            else
            {
                _dbContext.WorkSchedules.Add(new WorkSchedule
                {
                    EmployeeId = schedule.EmployeeId,
                    ScheduleDate = schedule.ScheduleDate,
                    WorkStartTime = schedule.WorkStartTime,
                    WorkEndTime = schedule.WorkEndTime,
                    BreakStartTime = schedule.BreakStartTime,
                    BreakEndTime = schedule.BreakEndTime,
                    TotalWorkMinutes = schedule.TotalWorkMinutes,
                    ScheduleType = schedule.ScheduleType,
                    HasChanged = false
                });
            }

            await _dbContext.SaveChangesAsync();

            // 通知员工排班变动（如果适用）
            if (existing != null && schedule.EmployeeId != CurrentSession.CurrentEmployeeId)
            {
                await NotifyEmployeeScheduleChangeAsync(schedule.EmployeeId);
            }

            ShowSuccess("保存成功");
            IsEditMode = false;
            await LoadCalendarAsync();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 通知员工排班变动（通过企业微信）
    /// </summary>
    private async Task NotifyEmployeeScheduleChangeAsync(int employeeId)
    {
        try
        {
            var encryptionService = App.Services.GetService(typeof(IEncryptionService)) as IEncryptionService;
            var httpClientFactory = App.Services.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;
            if (encryptionService == null || httpClientFactory == null) return;

            var weChatService = new WeChatService(httpClientFactory, encryptionService, _dbContext);
            var schedule = CurrentDailyPlan?.Schedule;
            if (schedule == null) return;

            var scheduleInfo = $"{schedule.ScheduleDate:yyyy-MM-dd}\n工作时间: {schedule.WorkStartTime} - {schedule.WorkEndTime}";
            await weChatService.NotifyScheduleChangeAsync(employeeId, scheduleInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"通知排班变动失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreatePlanAsync()
    {
        var employeeId = SelectedEmployee?.Id ?? CurrentSession.CurrentEmployeeId;
        if (employeeId == 0) { ShowError("请先选择员工"); return; }

        try
        {
            _dbContext.WorkPlans.Add(new WorkPlan
            {
                EmployeeId = employeeId,
                PlanDate = SelectedDate.Date,
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                DurationMinutes = 60,
                Content = "新工作计划",
                PlanType = "Other",
                ExecutionStatus = "Pending",
                CreatedAt = DateTime.Now
            });
            await _dbContext.SaveChangesAsync();
            ShowSuccess("工作计划已创建");

            await LoadDailyPlanAsync(SelectedDate);
        }
        catch (Exception ex) { ShowError($"创建计划失败: {ex.Message}"); }
    }


    private async Task SaveAsDraftAsync(string title, string content)
    {
        try
        {
            var draft = new PlanDraft
            {
                EmployeeId = CurrentSession.CurrentEmployeeId,
                Content = $"标题:{title}\n{content}",
                DraftType = "Personal",
                CreatedAt = DateTime.Now
            };

            _dbContext.PlanDrafts.Add(draft);
            await _dbContext.SaveChangesAsync();
            ShowSuccess("草稿保存成功");
            await LoadDraftsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PublishDraftAsync(PlanDraftItem? draft)
    {
        if (draft == null) return;

        try
        {
            var existing = await _dbContext.PlanDrafts.FindAsync(draft.Id);
            if (existing != null)
            {
                _dbContext.PlanDrafts.Remove(existing);
                await _dbContext.SaveChangesAsync();
            }

            ShowSuccess("草稿已发布");
            await LoadDraftsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"发布失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteDraftAsync(PlanDraftItem? draft)
    {
        if (draft == null) return;

        try
        {
            var existing = await _dbContext.PlanDrafts.FindAsync(draft.Id);
            if (existing != null)
            {
                _dbContext.PlanDrafts.Remove(existing);
                await _dbContext.SaveChangesAsync();
            }

            ShowSuccess("草稿已删除");
            await LoadDraftsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }
}

public class PlanDraftItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string DraftType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class DailyPlanView
{
    public DateTime Date { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public WorkScheduleListItem? Schedule { get; set; }
    public List<WorkPlanDto> Plans { get; set; } = new();
    public int TotalWorkMinutes { get; set; }
    public int TotalPlanMinutes { get; set; }
}

public class WorkScheduleListItem
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public TimeSpan WorkStartTime { get; set; }
    public TimeSpan WorkEndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
    public int TotalWorkMinutes { get; set; }
    public string ScheduleType { get; set; } = "";
    public bool HasChanged { get; set; }
}

public class CalendarScheduleItem
{
    public DateTime Date { get; set; }
    public bool IsWorkday { get; set; }
    public string? WorkStartTime { get; set; }
    public string? WorkEndTime { get; set; }
    public int TotalWorkMinutes { get; set; }
    public string ScheduleType { get; set; } = "";
    public bool HasChanged { get; set; }
}

public class WeeklyScheduleRow
{
    public string EmployeeName { get; set; } = "";
    public string DayMon { get => Days[0]; set => Days[0] = value; }
    public string DayTue { get => Days[1]; set => Days[1] = value; }
    public string DayWed { get => Days[2]; set => Days[2] = value; }
    public string DayThu { get => Days[3]; set => Days[3] = value; }
    public string DayFri { get => Days[4]; set => Days[4] = value; }
    public string DaySat { get => Days[5]; set => Days[5] = value; }
    public string DaySun { get => Days[6]; set => Days[6] = value; }
    public string[] Days { get; set; } = new string[7];
}

public partial class SystemSettingsViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;

    [ObservableProperty]
    private CloseBehavior _closeBehavior = CloseBehavior.MinimizeToTray;

    [ObservableProperty]
    private bool _enableNotification = true;

    [ObservableProperty]
    private bool _enableSound = true;

    [ObservableProperty]
    private bool _autoSyncOnStartup = true;

    [ObservableProperty]
    private int _syncIntervalMinutes = 30;

    [ObservableProperty]
    private SyncConfigDto? _syncConfig;

    [ObservableProperty]
    private LocalSettingDto? _localSettings;

    // 是否总部管理员（控制高级设置可见性）
    public bool IsHeadquartersAdmin => CurrentSession.Current?.IsHeadquartersAdmin ?? false;

    // 是否可以编辑设置（总部管理员或分公司管理员可编辑，普通员工仅查看）
    public bool CanEditSettings => CurrentSession.Current?.IsHeadquartersAdmin == true
        || CurrentSession.Current?.IsBranchAdmin == true;

    // 总部管理员功能 ViewModel（懒加载）
    private HeadquartersAdminViewModel? _headquartersAdminVM;
    public HeadquartersAdminViewModel? HeadquartersAdminVM
    {
        get
        {
            if (IsHeadquartersAdmin && _headquartersAdminVM == null)
            {
                _headquartersAdminVM = App.Services.GetService(typeof(HeadquartersAdminViewModel)) as HeadquartersAdminViewModel;
            }
            return _headquartersAdminVM;
        }
    }

    // 字段管理 ViewModel（懒加载）
    private FieldManagerViewModel? _fieldMgr;
    public FieldManagerViewModel FieldMgr
    {
        get
        {
            if (_fieldMgr == null)
            {
                _fieldMgr = new FieldManagerViewModel(_dbContext);
                _ = _fieldMgr.LoadDistrictsCommand.ExecuteAsync(null);
                _ = _fieldMgr.LoadTagsCommand.ExecuteAsync(null);
                _ = _fieldMgr.LoadCategoriesCommand.ExecuteAsync(null);
            }
            return _fieldMgr;
        }
    }

    public SystemSettingsViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _dbContext.LocalSettings.AsNoTracking().ToListAsync();

        var closeBehavior = settings.FirstOrDefault(s => s.SettingKey == "CloseBehavior");
        CloseBehavior = closeBehavior != null ? (CloseBehavior)int.Parse(closeBehavior.SettingValue) : CloseBehavior.MinimizeToTray;

        var notification = settings.FirstOrDefault(s => s.SettingKey == "EnableNotification");
        EnableNotification = notification == null || bool.Parse(notification.SettingValue);

        var sound = settings.FirstOrDefault(s => s.SettingKey == "EnableSound");
        EnableSound = sound == null || bool.Parse(sound.SettingValue);

        var autoSync = settings.FirstOrDefault(s => s.SettingKey == "AutoSyncOnStartup");
        AutoSyncOnStartup = autoSync == null || bool.Parse(autoSync.SettingValue);

        var interval = settings.FirstOrDefault(s => s.SettingKey == "SyncIntervalMinutes");
        SyncIntervalMinutes = interval != null ? int.Parse(interval.SettingValue) : 30;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            await SaveSettingAsync("CloseBehavior", ((int)CloseBehavior).ToString());
            await SaveSettingAsync("EnableNotification", EnableNotification.ToString());
            await SaveSettingAsync("EnableSound", EnableSound.ToString());
            await SaveSettingAsync("AutoSyncOnStartup", AutoSyncOnStartup.ToString());
            await SaveSettingAsync("SyncIntervalMinutes", SyncIntervalMinutes.ToString());

            ShowSuccess("保存成功");
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenDepartmentManagement()
    {
        try
        {
            var treeVm = App.Services.GetService(typeof(DepartmentTreeViewModel)) as DepartmentTreeViewModel;
            if (treeVm == null) return;
            var dialog = new Views.DepartmentManagementWindow(treeVm) { Owner = System.Windows.Application.Current.MainWindow };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowError($"打开部门管理失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenUserManagement()
    {
        try
        {
            var employeeListVm = App.Services.GetService(typeof(EmployeeListViewModel)) as EmployeeListViewModel;
            if (employeeListVm == null) return;
            var dialog = new Views.EmployeeListWindow(employeeListVm) { Owner = System.Windows.Application.Current.MainWindow };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowError($"打开用户管理失败: {ex.Message}");
        }
    }

    private async Task SaveSettingAsync(string key, string value)

    {
        var setting = await _dbContext.LocalSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting == null)
        {
            setting = new LocalSetting { SettingKey = key, SettingType = "String" };
            _dbContext.LocalSettings.Add(setting);
        }
        setting.SettingValue = value;
        setting.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
    }

    // ================================================================
    // 字段管理嵌套 ViewModel（商圈、标签、产品分类）
    // ================================================================
    public partial class FieldManagerViewModel : ViewModelBase
    {
        private readonly ProDbContext _db;

        public FieldManagerViewModel(ProDbContext db) { _db = db; }

        // === 商圈 ===
        [ObservableProperty] private string _newDistrictName = string.Empty;
        [ObservableProperty] private string _newDistrictCity = string.Empty;
        [ObservableProperty] private bool _newDistrictIsActive = true;
        [ObservableProperty] private ObservableCollection<BusinessDistrict> _businessDistricts = new();
        public List<string> CityList { get; } = new()
        {
            "北京","上海","广州","深圳","杭州","成都","武汉","南京","重庆","天津","苏州","西安","长沙","郑州","东莞","青岛","沈阳","宁波","昆明","大连","厦门","合肥","佛山","福州","哈尔滨","济南","温州","长春","石家庄","常州","无锡","南宁","贵阳","太原","南昌","中山","惠州","海口","兰州","珠海","乌鲁木齐","绍兴","呼和浩特","泉州","南通","徐州","潍坊","唐山","烟台"
        };

        [RelayCommand]
        private async Task LoadDistrictsAsync()
        {
            var branchId = CurrentSession.CurrentBranchId;
            var list = await _db.BusinessDistricts
                .Where(d => d.BranchId == branchId)
                .OrderBy(d => d.Name).ToListAsync();
            BusinessDistricts = new ObservableCollection<BusinessDistrict>(list);
        }

        [RelayCommand]
        private async Task AddDistrictAsync()
        {
            if (string.IsNullOrWhiteSpace(NewDistrictName)) return;
            try
            {
                var d = new BusinessDistrict
                {
                    Name = NewDistrictName,
                    City = NewDistrictCity,
                    BranchId = CurrentSession.CurrentBranchId,
                    Status = NewDistrictIsActive ? "Active" : "Inactive",
                    CreatedAt = DateTime.Now
                };
                _db.BusinessDistricts.Add(d);
                await _db.SaveChangesAsync();
                NewDistrictName = string.Empty;
                NewDistrictCity = string.Empty;
                NewDistrictIsActive = true;
                await LoadDistrictsAsync();
            }
            catch (Exception ex) { Serilog.Log.Error(ex, "新增商圈失败"); }
        }

        [RelayCommand]
        private async Task EditDistrictAsync(BusinessDistrict? d)
        {
            if (d == null) return;
            d.Status = d.Status == "Active" ? "Inactive" : "Active";
            await _db.SaveChangesAsync();
            await LoadDistrictsAsync();
        }

        [RelayCommand]
        private async Task DeleteDistrictAsync(BusinessDistrict? d)
        {
            if (d == null) return;
            _db.BusinessDistricts.Remove(d);
            await _db.SaveChangesAsync();
            await LoadDistrictsAsync();
        }

        // === 标签 ===
        [ObservableProperty] private string _newTagName = string.Empty;
        [ObservableProperty] private string _newTagColor = "#007AFF";
        [ObservableProperty] private ObservableCollection<CustomerTag> _customerTags = new();

        [RelayCommand]
        private async Task LoadTagsAsync()
        {
            var list = await _db.CustomerTags.OrderBy(t => t.Name).ToListAsync();
            CustomerTags = new ObservableCollection<CustomerTag>(list);
        }

        [RelayCommand]
        private async Task AddTagAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTagName)) return;
            try
            {
                _db.CustomerTags.Add(new CustomerTag
                {
                    Name = NewTagName,
                    Color = NewTagColor,
                    BranchId = CurrentSession.CurrentBranchId
                });
                await _db.SaveChangesAsync();
                NewTagName = string.Empty;
                await LoadTagsAsync();
            }
            catch (Exception ex) { Serilog.Log.Error(ex, "新增标签失败"); }
        }

        [RelayCommand]
        private async Task EditTagAsync(CustomerTag? t)
        {
            if (t == null) return;
            try { await _db.SaveChangesAsync(); await LoadTagsAsync(); }
            catch (Exception ex) { Serilog.Log.Error(ex, "编辑标签失败"); }
        }

        [RelayCommand]
        private async Task DeleteTagAsync(CustomerTag? t)
        {
            if (t == null) return;
            _db.CustomerTags.Remove(t);
            await _db.SaveChangesAsync();
            await LoadTagsAsync();
        }

        // === 产品分类 ===
        [ObservableProperty] private string _newCategoryName = string.Empty;
        [ObservableProperty] private int _newCategorySortOrder;
        [ObservableProperty] private ObservableCollection<ProductCategory> _productCategories = new();

        [RelayCommand]
        private async Task LoadCategoriesAsync()
        {
            var list = await _db.ProductCategories.OrderBy(c => c.SortOrder).ToListAsync();
            ProductCategories = new ObservableCollection<ProductCategory>(list);
        }

        [RelayCommand]
        private async Task AddCategoryAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName)) return;
            try
            {
                _db.ProductCategories.Add(new ProductCategory
                {
                    Name = NewCategoryName,
                    SortOrder = NewCategorySortOrder
                });
                await _db.SaveChangesAsync();
                NewCategoryName = string.Empty;
                NewCategorySortOrder = 0;
                await LoadCategoriesAsync();
            }
            catch (Exception ex) { Serilog.Log.Error(ex, "新增产品分类失败"); }
        }

        [RelayCommand]
        private async Task EditCategoryAsync(ProductCategory? c)
        {
            if (c == null) return;
            try { await _db.SaveChangesAsync(); await LoadCategoriesAsync(); }
            catch (Exception ex) { Serilog.Log.Error(ex, "编辑产品分类失败"); }
        }

        [RelayCommand]
        private async Task DeleteCategoryAsync(ProductCategory? c)
        {
            if (c == null) return;
            _db.ProductCategories.Remove(c);
            await _db.SaveChangesAsync();
            await LoadCategoriesAsync();
        }
    }
}

public partial class HeadquartersAdminViewModel : ViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private WeChatService? _weChatService;

    [ObservableProperty]
    private ObservableCollection<BranchListItem> _branches = new();

    [ObservableProperty]
    private ObservableCollection<EmployeeListItem> _allEmployees = new();

    [ObservableProperty]
    private WeChatConfigDto? _weChatConfig;

    [ObservableProperty]
    private SyncConfigDto? _syncConfig;

    // ========== 企业微信配置属性 ==========
    [ObservableProperty]
    private string _weChatCorpId = string.Empty;

    [ObservableProperty]
    private string _weChatCorpSecret = string.Empty;

    [ObservableProperty]
    private string _weChatAgentId = string.Empty;

    [ObservableProperty]
    private string _weChatWebhookUrl = string.Empty;

    [ObservableProperty]
    private bool _isWeChatEnabled;

    [ObservableProperty]
    private bool _isWeChatConnected;

    [ObservableProperty]
    private string _weChatStatus = "未连接";

    [ObservableProperty]
    private bool _isWeChatConnecting;

    [ObservableProperty]
    private bool _isWeChatHardcoded;

    // ========== Webhook 管理属性 ==========
    [ObservableProperty]
    private ObservableCollection<WebhookListItem> _webhooks = new();

    [ObservableProperty]
    private WebhookListItem? _selectedWebhook;

    [ObservableProperty]
    private string _newWebhookName = string.Empty;

    [ObservableProperty]
    private string _newWebhookUrl = string.Empty;

    [ObservableProperty]
    private string _newWebhookTrigger = string.Empty;

    [ObservableProperty]
    private string _newWebhookRemark = string.Empty;

    [ObservableProperty]
    private bool _isEditingWebhook;

    [ObservableProperty]
    private int _editingWebhookId;

    [ObservableProperty]
    private string _syncResult = "";

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private ObservableCollection<BackupRecordDto> _backupRecords = new();

    [ObservableProperty]
    private ObservableCollection<OperationLogDto> _operationLogs = new();

    public HeadquartersAdminViewModel()
    {
        _dbContext = App.Services.GetService(typeof(ProDbContext)) as ProDbContext 
            ?? throw new InvalidOperationException("无法获取数据库上下文");
        _httpClientFactory = App.Services.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory
            ?? throw new InvalidOperationException("无法获取 HttpClientFactory");
        SyncConfig = new SyncConfigDto();
        
        if (CurrentSession.Current.IsHeadquartersAdmin)
        {
            _ = InitializeAsync();
        }
    }

    private WeChatService GetWeChatService()

    {
        if (_weChatService == null)
        {
            var encryptionService = App.Services.GetService(typeof(IEncryptionService)) as IEncryptionService
                ?? throw new InvalidOperationException("无法获取 EncryptionService");

            _weChatService = new WeChatService(_httpClientFactory, encryptionService, _dbContext);
        }
        return _weChatService;
    }

    private async Task InitializeAsync()
    {
        await LoadWeChatConfigAsync();
        await LoadWebhooksAsync();
        await LoadBranchesAsync();
        await LoadEmployeesAsync();
        await LoadBackupRecordsAsync();
        await LoadSyncConfigAsync();
        await LoadOperationLogsAsync();
    }


    private async Task LoadDataAsync()
    {
        await InitializeAsync();
    }

    /// <summary>
    /// 加载操作日志
    /// </summary>
    private async Task LoadOperationLogsAsync()
    {
        try
        {
            var logs = await _dbContext.OperationLogs
                .AsNoTracking()
                .OrderByDescending(l => l.OperatedAt)
                .Take(200)
                .Select(l => new OperationLogDto
                {
                    Id = l.Id,
                    OperatorId = l.OperatorId,
                    OperatorName = l.Operator != null ? l.Operator.Name : l.OperatorNo,
                    OperatorNo = l.OperatorNo,
                    Module = l.Module,
                    OperationType = l.OperationType,
                    Content = l.Content,
                    Result = l.Result,
                    OperatedAt = l.OperatedAt
                })
                .ToListAsync();

            OperationLogs = new ObservableCollection<OperationLogDto>(logs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载操作日志失败: {ex.Message}");
        }
    }

    // ========== 企业微信配置方法 ==========
    private async Task LoadWeChatConfigAsync()
    {
        // 优先使用硬编码配置（应用内只读）
        if (HardcodedConfig.HasWeChatConfig)
        {
            WeChatCorpId = HardcodedConfig.WeChatCorpId;
            WeChatCorpSecret = HardcodedConfig.WeChatCorpSecret;
            WeChatAgentId = HardcodedConfig.WeChatAgentId;
            IsWeChatEnabled = HardcodedConfig.WeChatEnabled;
            IsWeChatHardcoded = true;
            return;
        }

        // 后备：从数据库读取
        try
        {
            var service = GetWeChatService();
            var config = await service.GetWeChatConfigAsync();
            if (config != null)
            {
                WeChatCorpId = config.CorpId;
                WeChatCorpSecret = config.CorpSecret;
                WeChatAgentId = config.AgentId;
                WeChatWebhookUrl = config.DefaultWebhookUrl ?? "";
                IsWeChatEnabled = config.IsEnabled;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"加载企业微信配置失败: {ex.Message}"); }
        IsWeChatHardcoded = false;
    }

    [RelayCommand]
    private async Task SaveWeChatConfigAsync()
    {
        try
        {
            var service = GetWeChatService();
            var config = new PRO.Infrastructure.WeChat.WeChatConfig
            {
                CorpId = WeChatCorpId,
                CorpSecret = WeChatCorpSecret,
                AgentId = WeChatAgentId,
                DefaultWebhookUrl = WeChatWebhookUrl,
                IsEnabled = IsWeChatEnabled
            };

            await service.SaveWeChatConfigAsync(config);
            ShowSuccess("企业微信配置已保存");
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TestWeChatConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(WeChatCorpId) || string.IsNullOrWhiteSpace(WeChatCorpSecret))
        {
            ShowError("请先填写企业ID和应用Secret");
            return;
        }

        IsWeChatConnecting = true;
        WeChatStatus = "正在连接...";

        try
        {
            var service = GetWeChatService();
            var config = new PRO.Infrastructure.WeChat.WeChatConfig
            {
                CorpId = WeChatCorpId,
                CorpSecret = WeChatCorpSecret
            };
            await service.SaveWeChatConfigAsync(config);

            var success = await service.TestConnectionAsync();
            if (success)
            {
                IsWeChatConnected = true;
                WeChatStatus = "连接成功";
                ShowSuccess("企业微信连接成功！");
            }
            else
            {
                IsWeChatConnected = false;
                WeChatStatus = "连接失败";
                ShowError("企业微信连接失败，请检查配置");
            }
        }
        catch (Exception ex)
        {
            IsWeChatConnected = false;
            WeChatStatus = "连接失败";
            ShowError($"连接失败: {ex.Message}");
        }
        finally
        {
            IsWeChatConnecting = false;
        }
    }

    [RelayCommand]
    private async Task SyncOrganizationAsync()
    {
        if (!IsWeChatConnected)
        {
            ShowError("请先测试企业微信连接");
            return;
        }

        IsSyncing = true;
        SyncResult = "正在同步组织架构...";

        try
        {
            var service = GetWeChatService();
            var result = await service.SyncOrganizationAsync();
            if (result.Success)
            {
                SyncResult = result.Message ?? "同步成功";
                ShowSuccess($"组织架构同步完成：{result.Message}");
            }
            else
            {
                SyncResult = "同步失败";
                ShowError($"同步失败：{result.Message}");
            }
        }
        catch (Exception ex)
        {
            SyncResult = "同步失败";
            ShowError($"同步失败: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task PullWeChatCustomersAsync()
    {
        if (!IsWeChatConnected)
        {
            ShowError("请先测试企业微信连接");
            return;
        }

        IsSyncing = true;
        SyncResult = "正在拉取企业微信联系人...";

        try
        {
            var service = GetWeChatService();
            var result = await service.PullNewCustomersAsync();
            if (result.Success)
            {
                SyncResult = result.Message ?? "拉取成功";
                ShowSuccess($"企业微信联系人同步完成：{result.Message}");
            }
            else
            {
                SyncResult = "拉取失败";
                ShowError($"拉取失败：{result.Message}");
            }
        }
        catch (Exception ex)
        {
            SyncResult = "拉取失败";
            ShowError($"拉取失败: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    // ========== Webhook 管理方法 ==========
    private async Task LoadWebhooksAsync()
    {
        try
        {
            var result = await _dbContext.Webhooks
                .AsNoTracking()
                .OrderByDescending(w => w.CreatedAt)
                .Take(50)
                .ToListAsync();

            Webhooks = new ObservableCollection<WebhookListItem>(result.Select(w => new WebhookListItem
            {
                Id = w.Id,
                Name = w.Name,
                WebhookUrl = w.WebhookUrl,
                TriggerCondition = w.TriggerCondition,
                Remark = w.Remark,
                IsEnabled = w.IsEnabled,
                LastTestTime = w.LastTestTime,
                LastTestResult = w.LastTestResult
            }));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"加载Webhooks失败: {ex.Message}"); }
    }

    [RelayCommand]
    private void NewWebhook()
    {
        IsEditingWebhook = true;
        EditingWebhookId = 0;
        NewWebhookName = "";
        NewWebhookUrl = "";
        NewWebhookTrigger = "";
        NewWebhookRemark = "";
    }

    [RelayCommand]
    private void EditWebhook(WebhookListItem? webhook)
    {
        if (webhook == null) return;
        
        IsEditingWebhook = true;
        EditingWebhookId = webhook.Id;
        NewWebhookName = webhook.Name;
        NewWebhookUrl = webhook.WebhookUrl;
        NewWebhookTrigger = webhook.TriggerCondition ?? "";
        NewWebhookRemark = webhook.Remark ?? "";
    }

    [RelayCommand]
    private async Task SaveWebhookAsync()
    {
        if (string.IsNullOrWhiteSpace(NewWebhookName) || string.IsNullOrWhiteSpace(NewWebhookUrl))
        {
            ShowError("请填写Webhook名称和地址");
            return;
        }

        try
        {
            if (EditingWebhookId > 0)
            {
                // 更新
                var webhook = await _dbContext.Webhooks.FindAsync(EditingWebhookId);
                if (webhook != null)
                {
                    webhook.Name = NewWebhookName;
                    webhook.WebhookUrl = NewWebhookUrl;
                    webhook.TriggerCondition = NewWebhookTrigger;
                    webhook.Remark = NewWebhookRemark;
                    webhook.UpdatedAt = DateTime.Now;
                }
            }
            else
            {
                // 新增
                var webhook = new PRO.Domain.Entities.Webhook
                {
                    Name = NewWebhookName,
                    WebhookUrl = NewWebhookUrl,
                    TriggerCondition = NewWebhookTrigger,
                    Remark = NewWebhookRemark,
                    IsEnabled = true,
                    CreatedAt = DateTime.Now
                };
                _dbContext.Webhooks.Add(webhook);
            }

            await _dbContext.SaveChangesAsync();
            await LoadWebhooksAsync();
            IsEditingWebhook = false;
            ShowSuccess("Webhook保存成功");
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelWebhookEdit()
    {
        IsEditingWebhook = false;
        EditingWebhookId = 0;
    }

    [RelayCommand]
    private async Task TestWebhookAsync(WebhookListItem? webhook)
    {
        if (webhook == null) return;

        try
        {
            var service = GetWeChatService();
            var message = new WebhookMessage
            {
                MsgType = "text",
                Content = $"【测试消息】\n这是一条来自 PRO 系统的 Webhook 测试消息\n\n时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            };

            var success = await service.SendWebhookMessageAsync(webhook.WebhookUrl, message);

            // 更新测试状态
            var entity = await _dbContext.Webhooks.FindAsync(webhook.Id);
            if (entity != null)
            {
                entity.LastTestTime = DateTime.Now;
                entity.LastTestResult = success ? "success" : "failed";
                await _dbContext.SaveChangesAsync();
            }

            await LoadWebhooksAsync();

            if (success)
            {
                ShowSuccess($"Webhook测试成功！");
            }
            else
            {
                ShowError("Webhook测试失败，请检查URL是否正确");
            }
        }
        catch (Exception ex)
        {
            ShowError($"测试失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteWebhookAsync(WebhookListItem? webhook)
    {
        if (webhook == null) return;

        var result = MessageBox.Show(
            $"确定要删除 Webhook「{webhook.Name}」吗？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var entity = await _dbContext.Webhooks.FindAsync(webhook.Id);
            if (entity != null)
            {
                _dbContext.Webhooks.Remove(entity);
                await _dbContext.SaveChangesAsync();
            }

            await LoadWebhooksAsync();
            ShowSuccess("Webhook已删除");
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }

    public class SyncDiffItem
    {
        public string FieldName { get; set; } = "";
        public string DiffType { get; set; } = "";
        public string LocalType { get; set; } = "";
        public string RemoteType { get; set; } = "";
    }

    private async Task LoadBranchesAsync()
    {
        var branches = await _dbContext.Branches.AsNoTracking().ToListAsync();
        Branches = new ObservableCollection<BranchListItem>(branches.Select(b => new BranchListItem
        {
            Id = b.Id,
            Name = b.Name,
            Code = b.Code,
            Address = b.Address,
            Phone = b.Phone,
            Status = b.Status
        }));
    }

    private async Task LoadEmployeesAsync()
    {
        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Branch)
            .Include(e => e.Department)
            .Include(e => e.Role)
            .ToListAsync();

        AllEmployees = new ObservableCollection<EmployeeListItem>(employees.Select(e => new EmployeeListItem
        {
            Id = e.Id,
            Name = e.Name,
            EmployeeNo = e.EmployeeNo,
            BranchName = e.Branch?.Name ?? "",
            DepartmentName = e.Department?.Name ?? "",
            RoleName = e.Role?.Name ?? "",
            Status = e.Status,
            CreatedAt = e.CreatedAt
        }));
    }

    private async Task LoadBackupRecordsAsync()
    {
        var records = await _dbContext.BackupRecords
            .AsNoTracking()
            .OrderByDescending(r => r.BackupTime)
            .Take(30)
            .ToListAsync();

        BackupRecords = new ObservableCollection<BackupRecordDto>(records.Select(r => new BackupRecordDto
        {
            Id = r.Id,
            FileName = r.FileName,
            BackupType = r.BackupType,
            FileSize = r.FileSize,
            BackupTime = r.BackupTime,
            ExpireTime = r.ExpireTime,
            Status = r.Status
        }));
    }

    private async Task LoadSyncConfigAsync()
    {
        var settings = await _dbContext.LocalSettings
            .AsNoTracking()
            .Where(s => s.SettingKey == "HeadquartersAutoSyncEnabled"
                || s.SettingKey == "HeadquartersSyncIntervalMinutes"
                || s.SettingKey == "HeadquartersConflictResolution")
            .ToListAsync();

        var autoSync = settings.FirstOrDefault(s => s.SettingKey == "HeadquartersAutoSyncEnabled");
        var interval = settings.FirstOrDefault(s => s.SettingKey == "HeadquartersSyncIntervalMinutes");
        var conflict = settings.FirstOrDefault(s => s.SettingKey == "HeadquartersConflictResolution");

        var conflictResolution = ConflictResolution.TimestampFirst;
        if (conflict != null && Enum.TryParse<ConflictResolution>(conflict.SettingValue, out var parsedConflict))
        {
            conflictResolution = parsedConflict;
        }

        SyncConfig = new SyncConfigDto
        {
            AutoSyncEnabled = autoSync != null && bool.TryParse(autoSync.SettingValue, out var autoSyncEnabled) && autoSyncEnabled,
            SyncIntervalMinutes = interval != null && int.TryParse(interval.SettingValue, out var syncInterval) ? syncInterval : 30,
            ConflictResolution = conflictResolution
        };
    }

    private async Task SaveLocalSettingAsync(string key, string value)
    {
        var setting = await _dbContext.LocalSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
        if (setting == null)
        {
            setting = new PRO.Domain.Entities.LocalSetting
            {
                SettingKey = key,
                SettingType = "String"
            };
            _dbContext.LocalSettings.Add(setting);
        }

        setting.SettingValue = value;
        setting.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();
    }

    [RelayCommand]
    private async Task SaveSyncConfigAsync()
    {
        try
        {
            SyncConfig ??= new SyncConfigDto();
            await SaveLocalSettingAsync("HeadquartersAutoSyncEnabled", SyncConfig.AutoSyncEnabled.ToString());
            await SaveLocalSettingAsync("HeadquartersSyncIntervalMinutes", SyncConfig.SyncIntervalMinutes.ToString());
            await SaveLocalSettingAsync("HeadquartersConflictResolution", SyncConfig.ConflictResolution.ToString());
            ShowSuccess("同步基础配置已保存");
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (IsSyncing) return;

        IsSyncing = true;
        SyncResult = "正在刷新同步状态...";
        try
        {
            await LoadSyncConfigAsync();
            await LoadOperationLogsAsync();

            var pendingCount = await _dbContext.OperationLogs
                .AsNoTracking()
                .CountAsync(o => o.SyncStatus == SyncStatus.Pending);

            SyncResult = pendingCount > 0
                ? $"发现 {pendingCount} 条待同步操作日志，请在结算或外部同步服务可用后同步。"
                : "当前无待同步操作";
            ShowSuccess(SyncResult);
        }
        catch (Exception ex)
        {
            SyncResult = "同步状态刷新失败";
            ShowError($"同步失败: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        try
        {
            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PRO", "backups");
            Directory.CreateDirectory(backupDir);

            var fileName = $"pro_backup_man_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var filePath = Path.Combine(backupDir, fileName);

            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PRO", "pro.db");

            if (File.Exists(dbPath))
            {
                File.Copy(dbPath, filePath, true);
                var fileInfo = new FileInfo(filePath);

                _dbContext.BackupRecords.Add(new BackupRecord
                {
                    FileName = fileName,
                    FilePath = filePath,
                    BackupType = "手动",
                    FileSize = fileInfo.Length,
                    BackupTime = DateTime.Now,
                    ExpireTime = DateTime.Now.AddDays(30),
                    Status = "Success"
                });
                await _dbContext.SaveChangesAsync();

                await LoadBackupRecordsAsync();
                ShowSuccess($"手动备份完成：{fileName}");
            }
            else
            {
                ShowError("数据库文件不存在");
            }
        }
        catch (Exception ex)
        {
            ShowError($"备份失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CleanupBackupAsync()
    {
        try
        {
            var expiredRecords = await _dbContext.BackupRecords
                .Where(r => r.ExpireTime <= DateTime.Now)
                .ToListAsync();

            foreach (var record in expiredRecords)
            {
                if (!string.IsNullOrWhiteSpace(record.FilePath) && File.Exists(record.FilePath))
                {
                    try
                    {
                        File.Delete(record.FilePath);
                    }
                    catch
                    {
                        // 文件删除失败时保留数据库记录状态刷新结果即可
                    }
                }
            }

            if (expiredRecords.Count > 0)
            {
                _dbContext.BackupRecords.RemoveRange(expiredRecords);
                await _dbContext.SaveChangesAsync();
            }

            await LoadBackupRecordsAsync();
            ShowSuccess(expiredRecords.Count > 0 ? $"已清理 {expiredRecords.Count} 条过期备份记录" : "当前没有需要清理的过期备份");
        }
        catch (Exception ex)
        {
            ShowError($"清理失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(int backupId)
    {
        var backup = await _dbContext.BackupRecords.FindAsync(backupId);
        if (backup == null)
        {
            ShowError("备份记录不存在");
            return;
        }

        var result = MessageBox.Show(
            $"确定要从备份文件 [{backup.FileName}] 恢复数据吗？\n当前数据将被覆盖，此操作不可逆！",
            "确认恢复",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            if (!string.IsNullOrEmpty(backup.FilePath) && File.Exists(backup.FilePath))
            {
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PRO", "pro.db");

                // 先创建当前数据备份
                var preRestoreDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PRO", "backups");
                Directory.CreateDirectory(preRestoreDir);
                var preBackupPath = Path.Combine(preRestoreDir, $"pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                if (File.Exists(dbPath))
                {
                    File.Copy(dbPath, preBackupPath, true);
                }

                // 还原
                File.Copy(backup.FilePath, dbPath, true);
                ShowSuccess("数据恢复成功，请重新启动应用");
            }
            else
            {
                ShowError("备份文件不存在");
            }
        }
        catch (Exception ex)
        {
            ShowError($"恢复失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteBackupAsync(int backupId)
    {
        try
        {
            var backup = await _dbContext.BackupRecords.FindAsync(backupId);
            if (backup == null)
            {
                ShowError("备份记录不存在");
                return;
            }

            if (!string.IsNullOrWhiteSpace(backup.FilePath) && File.Exists(backup.FilePath))
            {
                try
                {
                    File.Delete(backup.FilePath);
                }
                catch
                {
                    // 文件删除失败不阻断记录删除，避免残留脏数据
                }
            }

            _dbContext.BackupRecords.Remove(backup);
            await _dbContext.SaveChangesAsync();
            await LoadBackupRecordsAsync();
            ShowSuccess("备份记录已删除");
        }
        catch (Exception ex)
        {
            ShowError($"删除失败: {ex.Message}");
        }
    }


}
