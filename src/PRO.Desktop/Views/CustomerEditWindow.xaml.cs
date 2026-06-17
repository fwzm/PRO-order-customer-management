using System.Windows;
using PRO.Desktop.ViewModels;
using PRO.Domain.Enums;

namespace PRO.Desktop.Views;

public partial class CustomerEditWindow : Window
{
    private readonly CustomerEditViewModel _viewModel;

    public CustomerEditWindow(object viewModel)
    {
        InitializeComponent();
        _viewModel = (CustomerEditViewModel)viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.StopAutoSave();

        if (_viewModel.IsDirty && DialogResult != true)
        {
            var result = MessageBox.Show(
                "有未保存的修改，是否保存？\n\n选择\"取消\"返回编辑",
                "确认关闭",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    _viewModel.SaveCommand.Execute(null);
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateMajorCustomerVisibility();
        _viewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(CustomerEditViewModel.CustomerType))
                UpdateMajorCustomerVisibility();
        };
    }

    private void UpdateMajorCustomerVisibility()
    {
        MajorCustomerSection.Visibility = _viewModel.CustomerType == CustomerType.Sub
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MapPickerButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mapPickerVm = App.Services.GetService(typeof(MapPickerViewModel)) as MapPickerViewModel;
            if (mapPickerVm == null) return;

            var window = new MapPickerWindow(mapPickerVm);
            window.Owner = this;

            if (window.ShowDialog() == true)
            {
                var selected = mapPickerVm.SelectedResult;
                if (selected != null)
                {
                    if (string.IsNullOrWhiteSpace(_viewModel.Name))
                        _viewModel.Name = selected.Title;
                    _viewModel.Address = selected.Address;
                    _viewModel.Longitude = selected.Longitude;
                    _viewModel.Latitude = selected.Latitude;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"地图选点失败: {ex.Message}\n请手动输入地址和经纬度。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
