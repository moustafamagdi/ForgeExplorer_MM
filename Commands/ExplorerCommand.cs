using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ForgeExplorer.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ExplorerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Views.MainWindow mainWindow = new Views.MainWindow();
            mainWindow.ShowDialog();

            return Result.Succeeded;
        }
    }
}
