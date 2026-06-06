using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PRO.Desktop.Controls;

/// <summary>
/// 全局加载覆盖层控件 - 显示旋转加载动画和可选文本
/// </summary>
public partial class LoadingOverlay : UserControl
{
    private readonly DispatcherTimer _animationTimer;
    private double _currentAngle;

    public LoadingOverlay()
    {
        InitializeComponent();
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _animationTimer.Tick += (s, e) =>
        {
            _currentAngle = (_currentAngle + 6) % 360;
            SpinnerRotation.Angle = _currentAngle;
        };
    }

    /// <summary>
    /// 显示加载覆盖层
    /// </summary>
    public void Show(string? text = null)
    {
        LoadingText.Text = text ?? "加载中...";
        OverlayBorder.Visibility = Visibility.Visible;
        _animationTimer.Start();
    }

    /// <summary>
    /// 隐藏加载覆盖层
    /// </summary>
    public void Hide()
    {
        _animationTimer.Stop();
        OverlayBorder.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 当前是否正在显示
    /// </summary>
    public bool IsShowing => OverlayBorder.Visibility == Visibility.Visible;
}
