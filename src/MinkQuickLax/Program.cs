using MinkQuickLax.Tray;

namespace MinkQuickLax;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new App();
        app.InitializeComponent();

        using var tray = new TrayController(app);
        return app.Run();
    }
}
