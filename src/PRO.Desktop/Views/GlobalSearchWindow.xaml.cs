using System.Windows;
using System.Windows.Input;
using PRO.Desktop.Services;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class GlobalSearchWindow : Window
{
    private readonly GlobalSearchViewModel _viewModel;

    public GlobalSearchWindow(object viewModel)
    {
        InitializeComponent();
        _viewModel = (GlobalSearchViewModel)viewModel;
        DataContext = _viewModel;

        // 选中结果后导航并关闭
        _viewModel.SearchItemSelected += OnSearchItemSelected;

        Loaded += (s, e) =>
        {
            SearchInput.Focus();
            Keyboard.Focus(SearchInput);
        };

        // Esc 关闭, Enter 选中
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
            else if (e.Key == Key.Enter && _viewModel.SelectedItem != null)
            {
                _viewModel.SelectItemCommand.Execute(_viewModel.SelectedItem);
            }
        };
    }

    private void OnSearchItemSelected(SearchResult item)
    {
        DialogResult = true;
        Close();

        NavigateByResult(item);
    }

    private void ResultItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedItem != null)
        {
            _viewModel.SelectItemCommand.Execute(_viewModel.SelectedItem);
        }
    }

    private void SearchAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SearchAction action, Tag: SearchResult item })
        {
            return;
        }

        e.Handled = true;
        CloseWithSuccess();
        ExecuteSearchAction(item, action.Action);
    }

    private void ExecuteSearchAction(SearchResult item, string action)
    {
        switch (action)
        {
            case "OpenArchive":
                OpenCustomerArchive(item.Id, item.Title);
                break;
            case "EditCustomer":
                OpenCustomerEditor(item.Id);
                break;
            case "ViewOrders":
                NavigateToRoute("order");
                break;
            case "ViewOrder":
            case "EditOrder":
                OpenOrderEditor(item.Id);
                break;
            case "ViewProduct":
            case "EditProduct":
                OpenProductEditor(item.Id);
                break;
            case "ViewDeliveryPerson":
                OpenDeliveryPersonEditor(item.Id);
                break;
            default:
                NavigateByResult(item);
                break;
        }
    }

    private void NavigateByResult(SearchResult item)
    {
        // 根据 Type 确定导航目标
        var routeMap = new Dictionary<string, string>
        {
            ["Customer"] = "customer",
            ["Order"] = "order",
            ["Product"] = "product",
            ["DeliveryPerson"] = "logistics",
            ["Opportunity"] = "visit_opportunity"
        };

        if (routeMap.TryGetValue(item.Type, out var route))
        {
            NavigateToRoute(route);
        }
    }

    private void NavigateToRoute(string route)
    {
        var mainVm = System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel;
        if (mainVm == null) return;

        var navItem = mainVm.NavigationItems
            .SelectMany<NavigationItem, NavigationItem>(n => n.Children.Any() ? n.Children : [n])
            .FirstOrDefault(n => n.Id == route);

        if (navItem != null)
        {
            mainVm.NavigateToTabCommand.Execute(navItem);
        }
    }

    private static void OpenCustomerArchive(int customerId, string customerName)
    {
        if (App.Services.GetService(typeof(CustomerArchiveViewModel)) is not CustomerArchiveViewModel archiveVm)
        {
            return;
        }

        archiveVm.LoadCustomer(customerId);
        var window = new Window
        {
            Title = $"客户档案 - {customerName}",
            Content = new CustomerArchiveView(archiveVm),
            Width = 1000,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.Show();
    }

    private static void OpenCustomerEditor(int customerId)
    {
        if (App.Services.GetService(typeof(CustomerEditViewModel)) is not CustomerEditViewModel editVm)
        {
            return;
        }

        editVm.LoadCustomer(customerId);
        new CustomerEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
    }

    private static void OpenOrderEditor(int orderId)
    {
        if (App.Services.GetService(typeof(OrderEditViewModel)) is not OrderEditViewModel editVm)
        {
            return;
        }

        editVm.LoadOrder(orderId);
        new OrderEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
    }

    private static void OpenProductEditor(int productId)
    {
        if (App.Services.GetService(typeof(ProductEditViewModel)) is not ProductEditViewModel editVm)
        {
            return;
        }

        editVm.LoadProduct(productId);
        new ProductEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
    }

    private static void OpenDeliveryPersonEditor(int deliveryPersonId)
    {
        if (App.Services.GetService(typeof(DeliveryPersonEditViewModel)) is not DeliveryPersonEditViewModel editVm)
        {
            return;
        }

        editVm.LoadDeliveryPerson(deliveryPersonId);
        new DeliveryPersonEditWindow(editVm) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
    }

    private void CloseWithSuccess()
    {
        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            // The search window is normally shown as a dialog, but keep action buttons usable if this changes.
        }

        Close();
    }
}
