using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCOpti.Views;

/// <summary>Dark themed replacement for MessageBox. Buttons are passed in; the result is the clicked index.</summary>
public partial class DialogWindow : Window
{
    public int Result { get; private set; } = -1;

    public DialogWindow(string title, string message, string? warning, IReadOnlyList<string> buttons,
        int primaryIndex, bool danger, bool isError)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        if (!string.IsNullOrWhiteSpace(warning))
        {
            WarningText.Text = warning;
            WarningBox.Visibility = Visibility.Visible;
        }

        if (isError)
        {
            IconText.Text = "";
            IconText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
        }
        else if (danger || warning is not null)
        {
            IconText.Text = "";
            IconText.Foreground = (System.Windows.Media.Brush)FindResource("ModerateBrush");
        }

        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var isPrimary = i == primaryIndex;
            var b = new Button
            {
                Content = buttons[i],
                Margin = new Thickness(8, 0, 0, 0),
                MinWidth = 96,
                IsDefault = isPrimary && !danger, // dangerous actions must be clicked, not Enter-ed
                IsCancel = i == buttons.Count - 1 && buttons.Count > 1,
                Style = (Style)FindResource(isPrimary ? (danger ? "DangerButton" : "PrimaryButton") : "ButtonBase"),
            };
            b.Click += (_, _) => { Result = index; DialogResult = true; };
            ButtonPanel.Children.Add(b);
        }

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }
}
