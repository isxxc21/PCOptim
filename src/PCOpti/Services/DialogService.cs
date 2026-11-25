using System.Windows;
using PCOpti.Views;

namespace PCOpti.Services;

public interface IDialogService
{
    /// <summary>Yes/No style confirmation. Returns true only if the user clicks the confirm button.</summary>
    bool Confirm(string title, string message, string confirmText = "Continue", bool danger = false, string? warning = null);
    /// <summary>Shows several choices; returns the index clicked, or -1 if closed.</summary>
    int Choose(string title, string message, params string[] buttons);
    void Info(string title, string message, bool isError = false);
}

/// <summary>Shows <see cref="DialogWindow"/> on the UI thread, whichever thread calls it.</summary>
public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message, string confirmText = "Continue", bool danger = false, string? warning = null) =>
        Show(title, message, warning, new[] { confirmText, "Cancel" }, 0, danger, false) == 0;

    public int Choose(string title, string message, params string[] buttons) =>
        Show(title, message, null, buttons, 0, false, false);

    public void Info(string title, string message, bool isError = false) =>
        Show(title, message, null, new[] { "OK" }, 0, false, isError);

    private static int Show(string title, string message, string? warning, string[] buttons, int primary, bool danger, bool isError)
    {
        return Application.Current.Dispatcher.Invoke(() =>
        {
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dlg = new DialogWindow(title, message, warning, buttons, primary, danger, isError);
            if (owner is not null && owner != dlg && owner.IsLoaded) dlg.Owner = owner;
            else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dlg.ShowDialog();
            return dlg.Result;
        });
    }
}
