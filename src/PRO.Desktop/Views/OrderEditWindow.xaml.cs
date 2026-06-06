using System.Windows;
using System.Windows.Controls;
using PRO.Application.DTOs;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class OrderEditWindow : Window
{
    public OrderEditWindow(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CustomerSearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && DataContext is OrderEditViewModel vm)
        {
            var customer = e.AddedItems[0] as CustomerListItem;
            if (customer != null)
            {
                vm.SelectCustomerFromSearchCommand.Execute(customer);
            }
        }
    }

    private void ProductGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (DataContext is OrderEditViewModel vm)
        {
            Dispatcher.BeginInvoke(() => vm.RecalcItemsCommand.Execute(null));
        }
    }

    private void DiscountButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not OrderItemDto item) return;
        if (DataContext is not OrderEditViewModel vm) return;

        var menu = new ContextMenu();

        var discountItem = new MenuItem { Header = "打折" };
        discountItem.Click += (s, args) =>
        {
            var inputDialog = CreateDiscountInputDialog("输入折扣率（如 0.85 表示85折）：", item, "Discount");
            inputDialog.ShowDialog();
            vm.RecalcItemsCommand.Execute(null);
        };
        menu.Items.Add(discountItem);

        var priceChangeItem = new MenuItem { Header = "改价" };
        priceChangeItem.Click += (s, args) =>
        {
            var inputDialog = CreateDiscountInputDialog("输入改后单价：", item, "PriceChange");
            inputDialog.ShowDialog();
            vm.RecalcItemsCommand.Execute(null);
        };
        menu.Items.Add(priceChangeItem);

        menu.PlacementTarget = btn;
        menu.IsOpen = true;
    }

    private Window CreateDiscountInputDialog(string prompt, OrderItemDto item, string discountType)
    {
        var inputBox = new TextBox
        {
            Width = 200,
            Margin = new Thickness(10),
            Style = (Style)FindResource("ModernTextBoxStyle")
        };

        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(inputBox);

        var okButton = new Button
        {
            Content = "确定",
            Width = 80,
            Margin = new Thickness(10, 0, 10, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = (Style)FindResource("PrimaryButtonStyle")
        };

        var dialog = new Window
        {
            Title = discountType == "Discount" ? "打折" : "改价",
            Content = new StackPanel(),
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        ((StackPanel)dialog.Content).Children.Add(panel);
        ((StackPanel)dialog.Content).Children.Add(okButton);

        okButton.Click += (s, e) =>
        {
            if (decimal.TryParse(inputBox.Text.Trim(), out var value) && value > 0)
            {
                item.DiscountType = discountType;
                item.DiscountValue = value;

                if (discountType == "Discount" && value <= 1)
                {
                    item.Amount = Math.Round(item.Quantity * item.UnitPrice * value, 2);
                }
                else if (discountType == "PriceChange")
                {
                    item.UnitPrice = value;
                    item.Amount = item.Quantity * value;
                }

                dialog.DialogResult = true;
            }
            else
            {
                MessageBox.Show("请输入有效的数字", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        return dialog;
    }
}
