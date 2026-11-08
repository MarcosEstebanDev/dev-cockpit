using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DevCockpit.Core.Ports;

namespace DevCockpit.App.Views;

public partial class ConfirmSendDialog : Window
{
    // Constructor sin parámetros: lo exige el cargador de XAML de Avalonia.
    public ConfirmSendDialog() => InitializeComponent();

    public ConfirmSendDialog(string providerName, string payload, IReadOnlyList<string> repoNames) : this()
    {
        Heading.Text = $"Se va a enviar este texto a {providerName}. Revisalo antes de confirmar:";
        Payload.Text = payload;
        if (repoNames.Count > 0)
            DontAskAgain.Content = "No volver a preguntar para: " + string.Join(", ", repoNames);
        else
            DontAskAgain.IsVisible = false;
    }

    private void OnSend(object? sender, RoutedEventArgs e)
        => Close(new ConfirmationResult(true, DontAskAgain.IsChecked == true));

    private void OnCancel(object? sender, RoutedEventArgs e)
        => Close(new ConfirmationResult(false, false));
}
