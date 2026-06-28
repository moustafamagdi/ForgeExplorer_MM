using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ForgeExplorer.Core;

namespace ForgeExplorer.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ExplorerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DiagnosticLogger.Initialize();
            DiagnosticLogger.Write("ExplorerCommand.Execute started.");
            Views.MainWindow mainWindow = new Views.MainWindow();
            mainWindow.ShowDialog();
            DiagnosticLogger.Write("ExplorerCommand.Execute completed.");

            return Result.Succeeded;
        }
    }
}
