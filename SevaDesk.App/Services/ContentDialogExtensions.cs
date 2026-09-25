using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace SevaDesk_App.Services;

/// <summary>
/// Extension methods for WinUI 3 ContentDialog to support light dismiss (dismiss on outside click).
/// </summary>
public static class ContentDialogExtensions
{
    public static void EnableLightDismiss(this ContentDialog dialog, Func<bool>? canDismissPredicate = null)
    {
        dialog.Loaded += (s, e) =>
        {
            AttachLightDismiss(dialog, canDismissPredicate);
        };
    }

    private static void AttachLightDismiss(ContentDialog dialog, Func<bool>? canDismissPredicate)
    {
        try
        {
            // 1. Locate the SmokeLayerBackground part (standard WinUI 3 ContentDialog overlay)
            var smoke = FindVisualChildByName<FrameworkElement>(dialog, "SmokeLayerBackground");
            if (smoke != null)
            {
                smoke.PointerPressed += (sender, args) =>
                {
                    if (canDismissPredicate == null || canDismissPredicate())
                    {
                        args.Handled = true;
                        dialog.Hide();
                    }
                };
            }

            // 2. Also attach a handled pointer press listener to the root layout container as fallback
            var layoutRoot = FindVisualChildByName<FrameworkElement>(dialog, "LayoutRoot")
                             ?? (VisualTreeHelper.GetChildrenCount(dialog) > 0
                                 ? VisualTreeHelper.GetChild(dialog, 0) as FrameworkElement
                                 : null);

            if (layoutRoot != null)
            {
                layoutRoot.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((sender, args) =>
                {
                    var bgElement = FindVisualChildByName<FrameworkElement>(dialog, "BackgroundElement");
                    if (bgElement != null)
                    {
                        var pt = args.GetCurrentPoint(bgElement).Position;
                        if (pt.X < 0 || pt.Y < 0 || pt.X > bgElement.ActualWidth || pt.Y > bgElement.ActualHeight)
                        {
                            if (canDismissPredicate == null || canDismissPredicate())
                            {
                                args.Handled = true;
                                dialog.Hide();
                            }
                        }
                    }
                }), true);
            }
        }
        catch
        {
            // Graceful fallback: modal behavior remains active if visual tree cannot be hooked
        }
    }

    private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && string.Equals(fe.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return fe;
            }

            var found = FindVisualChildByName<T>(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
