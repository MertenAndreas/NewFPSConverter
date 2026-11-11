using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace NewFPSConverter;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            FreeConsole();
        }
        else
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        if (args.Any(a => a.Equals("--list-gpus", StringComparison.OrdinalIgnoreCase)
                       || a.Equals("-list-gpus", StringComparison.OrdinalIgnoreCase)))
        {
            GpuDetector.PrintAvailableGpus();
            return (int)ErrorCodes.Ok;
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
}
