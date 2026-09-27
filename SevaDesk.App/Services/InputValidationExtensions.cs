using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace SevaDesk_App.Services;

/// <summary>
/// Extension methods for standardizing TextBox input constraints and validation in SevaDesk.
/// </summary>
public static class InputValidationExtensions
{
    /// <summary>
    /// Configures a TextBox to only accept numeric digits (0-9) up to 10 characters maximum.
    /// Non-digit characters are blocked on keystroke and paste.
    /// Calls <paramref name="onValidationChanged"/> when text changes, indicating whether
    /// the input is empty or a complete 10-digit number.
    /// </summary>
    public static void ConfigureNumericMobileInput(this TextBox textBox, Action<bool>? onValidationChanged = null)
    {
        textBox.MaxLength = 10;
        
        var scope = new InputScope();
        scope.Names.Add(new InputScopeName(InputScopeNameValue.TelephoneNumber));
        textBox.InputScope = scope;

        textBox.BeforeTextChanging += (sender, args) =>
        {
            // Reject non-ASCII digits or strings exceeding 10 characters
            args.Cancel = args.NewText.Any(c => !char.IsAsciiDigit(c)) || args.NewText.Length > 10;
        };

        if (onValidationChanged != null)
        {
            textBox.TextChanged += (sender, args) =>
            {
                var text = textBox.Text?.Trim() ?? string.Empty;
                bool isValid = text.Length == 0 || text.Length == 10;
                onValidationChanged(isValid);
            };
        }
    }
}
