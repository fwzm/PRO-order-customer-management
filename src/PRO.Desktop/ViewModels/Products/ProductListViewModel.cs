using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRO.Application.DTOs;
using PRO.Application.Interfaces;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using ClosedXML.Excel;

namespace PRO.Desktop.ViewModels;

public partial class ProductListViewModel : PagedViewModelBase
{
    private readonly ProDbContext _dbContext;
    private readonly IProductService _productService;

    protected override string EntityTypeName => "产品";

    [ObservableProperty]
    private ObservableCollection<ProductListItem> _products = [];

    [ObservableProperty]
    private ProductListItem? _selectedProduct;

    [ObservableProperty]
    private ProductStatus? _filterStatus;

    [ObservableProperty]
    private int? _filterCategoryId;

    [ObservableProperty]
    private ObservableCollection<ProductCategoryDto> _categories = [];

    protected override bool HasActiveFilters() =>
        FilterStatus != null || (FilterCategoryId.HasValue && FilterCategoryId.Value > 0);

    [RelayCommand]
    private void ClearAllFilters()
    {
        FilterStatus = null;
        FilterCategoryId = null;
        SearchKeyword = null;
        InvalidateCountCache();
        RunInBackground(ResetToFirstPageAndLoadAsync(), "清除筛选失败");
    }

    private ICommand? _clearFiltersCommand;
    protected override ICommand? ClearFiltersCommand => _clearFiltersCommand ??= new RelayCommand(ClearAllFilters);
    private ICommand? _createNewCommand;
    protected override ICommand? CreateNewCommand => _createNewCommand ??= new RelayCommand(NewProduct);

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
        _productService = App.Services.GetService(typeof(IProductService)) as IProductService
            ?? throw new InvalidOperationException("无法获取产品服务");

        RunInBackground(InitAsync(), "初始化产品列表失败");
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
        var result = await _productService.GetCategoriesAsync();
        if (result.Success && result.Data != null)
            Categories = new ObservableCollection<ProductCategoryDto>(result.Data);
    }

    protected override async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var request = new PagedRequest
            {
                PageIndex = PageIndex,
                PageSize = PageSize,
                Keyword = SearchKeyword
            };

            var result = await _productService.GetListAsync(request,
                categoryId: FilterCategoryId.HasValue && FilterCategoryId.Value > 0 ? FilterCategoryId : null,
                status: FilterStatus);

            if (result.Success && result.Data != null)
            {
                TotalCount = result.Data.TotalCount;
                Products = new ObservableCollection<ProductListItem>(result.Data.Items);
                UpdateEmptyState();
            }
        }
        catch (Exception ex)
        {
            ShowBusinessException(ex, "加载产品列表");
            ShowLoadFailedState();
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
