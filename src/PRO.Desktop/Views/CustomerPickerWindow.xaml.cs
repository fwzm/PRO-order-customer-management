using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PRO.Application.DTOs;
using PRO.Domain.Enums;
using PRO.Infrastructure.Persistence;
using PRO.Desktop.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace PRO.Desktop.Views;

public partial class CustomerPickerWindow : Window
{
    private readonly ObservableCollection<CustomerListItem> _allCustomers;

    public CustomerListItem? SelectedCustomer { get; private set; }

    public CustomerPickerWindow(ObservableCollection<CustomerListItem> customers, CustomerListItem? selectedCustomer)
    {
        InitializeComponent();
        _allCustomers = customers;
        CustomerGrid.ItemsSource = customers;
        SelectedCustomer = selectedCustomer;

        if (selectedCustomer != null)
        {
            CustomerGrid.SelectedItem = selectedCustomer;
            CustomerGrid.ScrollIntoView(selectedCustomer);
        }
    }

    /// <summary>
    /// 自加载模式：从数据库加载客户列表并显示选择器
    /// </summary>
    public static async Task<CustomerListItem?> ShowAsync(Window? owner = null, CustomerListItem? preselected = null, int? filterBranchId = null)
    {
        var db = App.Services.GetService(typeof(ProDbContext)) as ProDbContext
            ?? throw new InvalidOperationException("无法获取数据库上下文");

        var branchId = filterBranchId ?? CurrentSession.CurrentBranchId;

        var query = db.Customers
            .Include(c => c.Branch)
            .Include(c => c.CustomerManager)
            .AsQueryable();

        if (!CurrentSession.Current.IsHeadquartersAdmin)
            query = query.Where(c => c.BranchId == branchId);

        query = query.Where(c => c.Status == CustomerStatus.Active);

        var customers = await query.OrderBy(c => c.Name)
            .Take(500)
            .Select(c => new CustomerListItem
            {
                Id = c.Id,
                Name = c.Name,
                CustomerNo = c.CustomerNo,
                CustomerType = c.CustomerType,
                CustomerTypeName = c.CustomerType == CustomerType.Major ? "大客户" : "细分客户",
                Phone = c.Phone,
                Address = c.Address,
                BranchId = c.BranchId,
                BranchName = c.Branch != null ? c.Branch.Name : "",
                Status = c.Status,
                CustomerManagerName = c.CustomerManager != null ? c.CustomerManager.Name : null,
            })
            .ToListAsync();

        var items = new ObservableCollection<CustomerListItem>(customers);
        var picker = new CustomerPickerWindow(items, preselected) { Owner = owner ?? System.Windows.Application.Current.MainWindow };

        if (picker.ShowDialog() == true)
            return picker.SelectedCustomer;

        return null;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var keyword = SearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            CustomerGrid.ItemsSource = _allCustomers;
        }
        else
        {
            CustomerGrid.ItemsSource = _allCustomers
                .Where(c => c.Name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase) ||
                           (c.Phone != null && c.Phone.Contains(keyword)))
                .ToList();
        }
    }

    private void CustomerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = CustomerGrid.SelectedItem != null;
    }

    private void CustomerGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (CustomerGrid.SelectedItem is CustomerListItem customer)
        {
            SelectedCustomer = customer;
            DialogResult = true;
            Close();
        }
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (CustomerGrid.SelectedItem is CustomerListItem customer)
        {
            SelectedCustomer = customer;
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
