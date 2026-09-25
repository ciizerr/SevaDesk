using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Controls;

public sealed partial class ToastNotificationControl : UserControl
{
    private DispatcherTimer? _autoDismissTimer;
    private bool _isVisible;

    public ToastNotificationControl()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;

        HideAnimation.Completed += (s, e) =>
        {
            Visibility = Visibility.Collapsed;
            _isVisible = false;
        };

        ToastService.Instance.ToastRequested += OnToastRequested;
        ToastService.Instance.ToastWithActionRequested += OnToastWithActionRequested;
        Unloaded += (s, e) =>
        {
            ToastService.Instance.ToastRequested -= OnToastRequested;
            ToastService.Instance.ToastWithActionRequested -= OnToastWithActionRequested;
        };
    }

    private Action? _currentAction;

    private void OnToastWithActionRequested(string message, ToastSeverity severity, int durationMs, string? actionLabel, Action? onAction)
    {
        _currentAction = onAction;
        if (!string.IsNullOrWhiteSpace(actionLabel) && onAction != null)
        {
            ToastActionButton.Content = actionLabel;
            ToastActionButton.Visibility = Visibility.Visible;
        }
        else
        {
            ToastActionButton.Visibility = Visibility.Collapsed;
        }
        ShowToastInternal(message, severity, durationMs);
    }

    private void OnToastRequested(string message, ToastSeverity severity, int durationMs)
    {
        _currentAction = null;
        ToastActionButton.Visibility = Visibility.Collapsed;
        ShowToastInternal(message, severity, durationMs);
    }

    private void ShowToastInternal(string message, ToastSeverity severity, int durationMs)
    {
        // Stop any pending dismiss
        _autoDismissTimer?.Stop();
        if (_isVisible)
        {
            HideAnimation.Stop();
        }

        // Apply severity styling
        ApplySeverityStyle(severity);

        // Set message
        MessageText.Text = message;

        // Show
        Visibility = Visibility.Visible;
        _isVisible = true;
        ShowAnimation.Begin();

        // Set up auto-dismiss timer
        if (durationMs > 0)
        {
            _autoDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            _autoDismissTimer.Tick += (s, e) =>
            {
                _autoDismissTimer.Stop();
                DismissToast();
            };
            _autoDismissTimer.Start();
        }
    }

    private void ToastActionButton_Click(object sender, RoutedEventArgs e)
    {
        var action = _currentAction;
        _currentAction = null;
        DismissToast();
        action?.Invoke();
    }

    private void ApplySeverityStyle(ToastSeverity severity)
    {
        string glyph;
        string dotBackground;
        string borderColor;

        switch (severity)
        {
            case ToastSeverity.Success:
                glyph = "\uE73E"; // Checkmark
                dotBackground = "#10B981";
                borderColor = "#10B981";
                break;
            case ToastSeverity.Error:
                glyph = "\uEA39"; // Error circle
                dotBackground = "#EF4444";
                borderColor = "#EF4444";
                break;
            case ToastSeverity.Warning:
                glyph = "\uE7BA"; // Warning
                dotBackground = "#F59E0B";
                borderColor = "#F59E0B";
                break;
            case ToastSeverity.Info:
            default:
                glyph = "\uE946"; // Info
                dotBackground = "#3B82F6";
                borderColor = "#3B82F6";
                break;
        }

        SeverityIcon.Glyph = glyph;
        SeverityIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);

        var dotColor = ParseHexColor(dotBackground);
        SeverityDot.Background = new SolidColorBrush(dotColor);

        var borderBrushColor = ParseHexColor(borderColor);
        // Use a semi-transparent version for the border
        borderBrushColor.A = 80;
        ToastBorder.BorderBrush = new SolidColorBrush(borderBrushColor);
    }

    private static Windows.UI.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    private void DismissToast()
    {
        if (!_isVisible) return;
        HideAnimation.Begin();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _autoDismissTimer?.Stop();
        DismissToast();
    }
}
