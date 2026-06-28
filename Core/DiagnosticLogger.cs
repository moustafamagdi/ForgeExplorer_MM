using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ForgeExplorer.Core
{
    internal static class DiagnosticLogger
    {
        private static readonly object SyncRoot = new object();
        private static readonly string LogPath = CreateLogPath();
        private static string _fallbackLogPath;
        private static bool _initialized;

        public static string CurrentLogPath => LogPath;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Write("Diagnostic logging initialized.");
            Write($"Assembly: {Assembly.GetExecutingAssembly().FullName}");
            Write($"Assembly location: {Assembly.GetExecutingAssembly().Location}");
            Write($"Revit/process: {AppDomain.CurrentDomain.FriendlyName}");
            Write($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
            Write($"Machine: {Environment.MachineName}");
            Write($"User: {Environment.UserName}");
            Write($"OS: {Environment.OSVersion}");
            Write($"CLR: {Environment.Version}");

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskSchedulerHook.Initialize();
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        }

        public static void Write(string message, [CallerMemberName] string caller = null)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{caller}] {message}";
            lock (SyncRoot)
            {
                try
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    if (string.IsNullOrWhiteSpace(_fallbackLogPath))
                    {
                        _fallbackLogPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(LogPath));
                    }

                    File.AppendAllText(_fallbackLogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
        }

        public static void WriteException(Exception exception, string context, [CallerMemberName] string caller = null)
        {
            if (exception == null)
            {
                Write($"{context}: <null exception>", caller);
                return;
            }

            Write($"{context}: {FlattenException(exception)}", caller);
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            Write($"AssemblyResolve requested: {args.Name}; requesting assembly: {args.RequestingAssembly?.FullName ?? "<unknown>"}");
            return null;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            WriteException(args.ExceptionObject as Exception, $"Unhandled exception. IsTerminating={args.IsTerminating}");
        }

        private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is TypeInitializationException || args.Exception?.GetType().FullName?.StartsWith("Autodesk.", StringComparison.OrdinalIgnoreCase) == true)
            {
                WriteException(args.Exception, "First chance exception");
            }
        }

        private static string CreateLogPath()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
            {
                desktop = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
            {
                desktop = Path.GetTempPath();
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            return Path.Combine(desktop, $"{timestamp}-log.txt");
        }

        private static string FlattenException(Exception exception)
        {
            StringBuilder builder = new StringBuilder();
            int depth = 0;
            Exception current = exception;
            while (current != null)
            {
                builder.AppendLine();
                builder.AppendLine($"  [{depth}] {current.GetType().FullName}: {current.Message}");
                builder.AppendLine(current.StackTrace ?? "  <no stack trace>");
                current = current.InnerException;
                depth++;
            }

            if (exception is ReflectionTypeLoadException reflectionTypeLoadException)
            {
                for (int i = 0; i < reflectionTypeLoadException.LoaderExceptions.Length; i++)
                {
                    builder.AppendLine($"  LoaderException[{i}]: {reflectionTypeLoadException.LoaderExceptions[i]}");
                }
            }

            return builder.ToString();
        }
        private static class TaskSchedulerHook
        {
            private static bool _initialized;

            public static void Initialize()
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;
                TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    WriteException(args.Exception, "Unobserved task exception");
                };
            }
        }
    }
}
