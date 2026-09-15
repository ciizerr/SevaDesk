using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SevaDesk_App.Services;

public class Loc : DependencyObject
{
    public Loc() { }

    private enum LocTargetProperty
    {
        Key,
        HeaderKey,
        PlaceholderKey,
        TooltipKey,
        PrimaryButtonKey,
        SecondaryButtonKey,
        CloseButtonKey,
        MessageKey
    }

    private sealed class TrackedBinding
    {
        public WeakReference<DependencyObject> Target { get; }
        public LocTargetProperty PropertyType { get; }
        public string Key { get; }

        public TrackedBinding(DependencyObject target, LocTargetProperty propertyType, string key)
        {
            Target = new WeakReference<DependencyObject>(target);
            PropertyType = propertyType;
            Key = key;
        }
    }

    private static readonly List<TrackedBinding> _bindings = new();
    private static readonly object _syncLock = new();

    static Loc()
    {
        AppServices.Localization.LanguageChanged += OnLanguageChanged;
    }

    private static void OnLanguageChanged(object? sender, EventArgs e)
    {
        var dispatcher = MainWindow.Instance?.DispatcherQueue;
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(RefreshAll);
        }
        else
        {
            RefreshAll();
        }
    }

    private static void RefreshAll()
    {
        lock (_syncLock)
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                var binding = _bindings[i];
                if (binding.Target.TryGetTarget(out var element))
                {
                    ApplyValue(element, binding.PropertyType, binding.Key);
                }
                else
                {
                    _bindings.RemoveAt(i);
                }
            }
        }
    }

    private static void RegisterBinding(DependencyObject target, LocTargetProperty propertyType, string key)
    {
        lock (_syncLock)
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                if (_bindings[i].PropertyType == propertyType &&
                    _bindings[i].Target.TryGetTarget(out var existing) &&
                    ReferenceEquals(existing, target))
                {
                    _bindings.RemoveAt(i);
                    break;
                }
            }

            _bindings.Add(new TrackedBinding(target, propertyType, key));
        }
    }

    private static void ApplyValue(DependencyObject target, LocTargetProperty propertyType, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        var localized = AppServices.Localization.GetString(key);

        switch (propertyType)
        {
            case LocTargetProperty.Key:
                if (target is TextBlock tb)
                {
                    tb.Text = localized;
                }
                else if (target is NavigationViewItem nvi)
                {
                    nvi.Content = localized;
                }
                else if (target is TitleBar titleBar)
                {
                    titleBar.Title = localized;
                }
                else if (target is InfoBar ib)
                {
                    ib.Title = localized;
                }
                else if (target is MenuFlyoutItem mfi)
                {
                    mfi.Text = localized;
                }
                else if (target is SelectorBarItem sbi)
                {
                    sbi.Text = localized;
                }
                else if (target is ContentDialog cd)
                {
                    cd.Title = localized;
                }
                else if (target is ContentControl cc)
                {
                    if (cc.Content == null || cc.Content is string)
                    {
                        cc.Content = localized;
                    }
                }
                break;

            case LocTargetProperty.HeaderKey:
                if (target is TextBox tbH)
                {
                    tbH.Header = localized;
                }
                else if (target is ComboBox cbH)
                {
                    cbH.Header = localized;
                }
                else if (target is NumberBox nbH)
                {
                    nbH.Header = localized;
                }
                else if (target is PasswordBox pbH)
                {
                    pbH.Header = localized;
                }
                break;

            case LocTargetProperty.PlaceholderKey:
                if (target is TextBox tbP)
                {
                    tbP.PlaceholderText = localized;
                }
                else if (target is AutoSuggestBox asbP)
                {
                    asbP.PlaceholderText = localized;
                }
                else if (target is ComboBox cbP)
                {
                    cbP.PlaceholderText = localized;
                }
                break;

            case LocTargetProperty.TooltipKey:
                if (target is FrameworkElement fe)
                {
                    ToolTipService.SetToolTip(fe, localized);
                }
                break;

            case LocTargetProperty.PrimaryButtonKey:
                if (target is ContentDialog cdPrimary)
                {
                    cdPrimary.PrimaryButtonText = localized;
                }
                break;

            case LocTargetProperty.SecondaryButtonKey:
                if (target is ContentDialog cdSec)
                {
                    cdSec.SecondaryButtonText = localized;
                }
                break;

            case LocTargetProperty.CloseButtonKey:
                if (target is ContentDialog cdClose)
                {
                    cdClose.CloseButtonText = localized;
                }
                break;

            case LocTargetProperty.MessageKey:
                if (target is InfoBar ibMsg)
                {
                    ibMsg.Message = localized;
                }
                break;
        }
    }

    #region Key Attached Property
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.Key, key);
                    ApplyValue(d, LocTargetProperty.Key, key);
                }
            }));

    public static string GetKey(DependencyObject obj) => (string)obj.GetValue(KeyProperty);
    public static void SetKey(DependencyObject obj, string value) => obj.SetValue(KeyProperty, value);
    #endregion

    #region HeaderKey Attached Property
    public static readonly DependencyProperty HeaderKeyProperty =
        DependencyProperty.RegisterAttached("HeaderKey", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.HeaderKey, key);
                    ApplyValue(d, LocTargetProperty.HeaderKey, key);
                }
            }));

    public static string GetHeaderKey(DependencyObject obj) => (string)obj.GetValue(HeaderKeyProperty);
    public static void SetHeaderKey(DependencyObject obj, string value) => obj.SetValue(HeaderKeyProperty, value);
    #endregion

    #region PlaceholderKey Attached Property
    public static readonly DependencyProperty PlaceholderKeyProperty =
        DependencyProperty.RegisterAttached("PlaceholderKey", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.PlaceholderKey, key);
                    ApplyValue(d, LocTargetProperty.PlaceholderKey, key);
                }
            }));

    public static string GetPlaceholderKey(DependencyObject obj) => (string)obj.GetValue(PlaceholderKeyProperty);
    public static void SetPlaceholderKey(DependencyObject obj, string value) => obj.SetValue(PlaceholderKeyProperty, value);
    #endregion

    #region TooltipKey Attached Property
    public static readonly DependencyProperty TooltipKeyProperty =
        DependencyProperty.RegisterAttached("TooltipKey", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.TooltipKey, key);
                    ApplyValue(d, LocTargetProperty.TooltipKey, key);
                }
            }));

    public static string GetTooltipKey(DependencyObject obj) => (string)obj.GetValue(TooltipKeyProperty);
    public static void SetTooltipKey(DependencyObject obj, string value) => obj.SetValue(TooltipKeyProperty, value);
    #endregion

    #region PrimaryButtonKey Attached Property
    public static readonly DependencyProperty PrimaryButtonKeyProperty =
        DependencyProperty.RegisterAttached("PrimaryButtonKey", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.PrimaryButtonKey, key);
                    ApplyValue(d, LocTargetProperty.PrimaryButtonKey, key);
                }
            }));

    public static string GetPrimaryButtonKey(DependencyObject obj) => (string)obj.GetValue(PrimaryButtonKeyProperty);
    public static void SetPrimaryButtonKey(DependencyObject obj, string value) => obj.SetValue(PrimaryButtonKeyProperty, value);
    #endregion

    #region SecondaryButtonKey Attached Property
    public static readonly DependencyProperty SecondaryButtonKeyProperty =
        DependencyProperty.RegisterAttached("SecondaryButtonKey", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.SecondaryButtonKey, key);
                    ApplyValue(d, LocTargetProperty.SecondaryButtonKey, key);
                }
            }));

    public static string GetSecondaryButtonKey(DependencyObject obj) => (string)obj.GetValue(SecondaryButtonKeyProperty);
    public static void SetSecondaryButtonKey(DependencyObject obj, string value) => obj.SetValue(SecondaryButtonKeyProperty, value);
    #endregion

    #region CloseButtonKey Attached Property
    public static readonly DependencyProperty CloseButtonKeyProperty =
        DependencyProperty.RegisterAttached("CloseButtonKey", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.CloseButtonKey, key);
                    ApplyValue(d, LocTargetProperty.CloseButtonKey, key);
                }
            }));

    public static string GetCloseButtonKey(DependencyObject obj) => (string)obj.GetValue(CloseButtonKeyProperty);
    public static void SetCloseButtonKey(DependencyObject obj, string value) => obj.SetValue(CloseButtonKeyProperty, value);
    #endregion

    #region MessageKey Attached Property
    public static readonly DependencyProperty MessageKeyProperty =
        DependencyProperty.RegisterAttached("MessageKey", typeof(string), typeof(Loc),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is string key)
                {
                    RegisterBinding(d, LocTargetProperty.MessageKey, key);
                    ApplyValue(d, LocTargetProperty.MessageKey, key);
                }
            }));

    public static string GetMessageKey(DependencyObject obj) => (string)obj.GetValue(MessageKeyProperty);
    public static void SetMessageKey(DependencyObject obj, string value) => obj.SetValue(MessageKeyProperty, value);
    #endregion
}
