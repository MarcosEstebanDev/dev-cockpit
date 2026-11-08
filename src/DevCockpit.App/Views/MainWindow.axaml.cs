using Avalonia.Controls;
using Avalonia.Threading;

namespace DevCockpit.App.Views;

public partial class MainWindow : Window
{
    /// <summary>Solo "Salir" del menú de la bandeja cierra de verdad; la X oculta la ventana.</summary>
    public bool AllowClose { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            // Hide() dentro del propio cierre se ignora: se oculta apenas termina de procesarse.
            Dispatcher.UIThread.Post(Hide);
        }
        base.OnClosing(e);
    }
}
