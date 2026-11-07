using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DevCockpit.App.Views;
using DevCockpit.Core.Ports;

namespace DevCockpit.App.Services;

/// <summary>Muestra el texto exacto que se va a mandar a la nube y espera la decisión del usuario.</summary>
public sealed class DialogCloudSendConfirmation(Func<Window?> owner) : ICloudSendConfirmation
{
    public Task<ConfirmationResult> ConfirmAsync(string providerName, string payload, IReadOnlyList<string> repoNames)
        => Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new ConfirmSendDialog(providerName, payload, repoNames);
            return owner() is { IsVisible: true } window
                ? await dialog.ShowDialog<ConfirmationResult?>(window) ?? new ConfirmationResult(false, false)
                : new ConfirmationResult(false, false); // sin ventana visible no se manda nada
        });
}
