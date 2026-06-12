using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace PRO.Desktop.Controls;

public partial class ToastNotification : UserControl
{
    private readonly Storyboard _showStory;
    private readonly Storyboard _hideStory;
    private bool _isVisible;

    public ToastNotification()
    {
        InitializeComponent();
        var duration = new Duration(TimeSpan.FromSeconds(0.25));

        _showStory = new Storyboard();
        var showAnim = new DoubleAnimation(0, 1, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(showAnim, ToastBorder);
        Storyboard.SetTargetProperty(showAnim, new PropertyPath("Opacity"));
        _showStory.Children.Add(showAnim);

        _hideStory = new Storyboard();
        var hideAnim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.3))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }, BeginTime = TimeSpan.FromSeconds(2.5) };
        Storyboard.SetTarget(hideAnim, ToastBorder);
        Storyboard.SetTargetProperty(hideAnim, new PropertyPath("Opacity"));
        _hideStory.Children.Add(hideAnim);
        _hideStory.Completed += (s, e) => { _isVisible = false; Visibility = Visibility.Collapsed; };
    }

    public void Show(string message, string? title = null, bool isSuccess = true)
    {
        if (_isVisible) return;
        _isVisible = true;

        TitleText.Text = title ?? (isSuccess ? "成功" : "提示");
        MessageText.Text = message;
        if (!isSuccess) { ToastBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 30, 30)); }
        else { ToastBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 30)); }

        Visibility = Visibility.Visible;
        _showStory.Begin();
        _hideStory.Begin();
    }
}
