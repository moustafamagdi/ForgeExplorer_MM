using Autodesk.Revit.UI;
using ForgeExplorer.Core;
using System;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace ForgeExplorer
{
    public class Application : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            DiagnosticLogger.Write("OnShutdown called.");
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            DiagnosticLogger.Initialize();
            DiagnosticLogger.Write("OnStartup started.");
            RibbonPanel ribbonPanel = application.CreateRibbonPanel("Forge Explorer");
            string assembly = Assembly.GetExecutingAssembly().Location;

            PushButton pushButton = ribbonPanel.AddItem(new PushButtonData(
                "ForgeExplorer",
                "Forge Explorer",
                assembly,
                typeof(Commands.ExplorerCommand).FullName)) as PushButton;

            if (pushButton != null)
            {
                pushButton.LargeImage = new BitmapImage(new Uri("/ForgeExplorer;component/Resources/open-folder_32x32.png", UriKind.RelativeOrAbsolute));
                pushButton.Image = new BitmapImage(new Uri("/ForgeExplorer;component/Resources/open-folder_16x16.png", UriKind.RelativeOrAbsolute));
                pushButton.ToolTip = "Explore Autodesk Docs, BIM 360, Fusion Team, and A360 Personal data from Revit 2024.";
            }

            DiagnosticLogger.Write($"OnStartup completed. Log file: {DiagnosticLogger.CurrentLogPath}");
            return Result.Succeeded;
        }
    }
}
