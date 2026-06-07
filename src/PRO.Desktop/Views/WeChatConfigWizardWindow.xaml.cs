using System.Windows;
using System.Windows.Input;
using PRO.Application.DTOs;
using PRO.Desktop.ViewModels;

namespace PRO.Desktop.Views;

public partial class WeChatConfigWizardWindow : Window
{
    private readonly WeChatConfigWizardViewModel _viewModel;

    public WeChatConfigWizardWindow(WeChatConfigWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void Step_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WeChatWizardStepDto step)
        {
            _viewModel.GoToStepCommand.Execute(step.Step);
        }
    }

    private void CorpSecret_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            _viewModel.CorpSecret = passwordBox.Password;
        }
    }
}
