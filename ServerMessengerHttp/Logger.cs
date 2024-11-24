using System.Collections.Concurrent;
using System.Diagnostics;

namespace ServerMessengerHttp
{
    internal static class Logger
    {
        private static readonly ConcurrentQueue<(string, ConsoleColor)> _loggingQueue;
        private const string _pathToLoggingFile = @"C:\Users\Crist\source\repos\ServerMessengerHttp\ServerMessengerHttp\NeededFiles\LoggingFile.txt";

        static Logger()
        {
            _loggingQueue = new ConcurrentQueue<(string, ConsoleColor)>();
            if (File.Exists(_pathToLoggingFile))
            {
                File.WriteAllText(_pathToLoggingFile, "");
            }
            else
            {
                throw new Exception("The logging file doesn´t exist.");
            }
        }

        internal static async Task LogAsync<T>(params T[] logs) where T : notnull, IConvertible
        {
            foreach (T log in logs)
            {
                var formatedLog = $"[{DateTime.Now}]: {log}";
                Console.WriteLine(formatedLog);
                _loggingQueue.Enqueue((formatedLog, ConsoleColor.White));
            }
            //Creating a empty line for better readability
            Console.ResetColor();
            Console.WriteLine("");
            _loggingQueue.Enqueue(("", ConsoleColor.White));

            try
            {
                using StreamWriter streamWriter = new(path: _pathToLoggingFile, append: true);
                foreach ((var content, ConsoleColor color) in _loggingQueue)
                {
                    await streamWriter.WriteLineAsync(content);
                }
                _loggingQueue.Clear();
            }
            catch (Exception) { }
        }

        internal static async Task LogAsync<T>(ConsoleColor color, params T[] logs) where T : notnull, IConvertible
        {
            Console.ForegroundColor = color;
            foreach (T log in logs)
            {
                var formatedLog = $"[{DateTime.Now}]: {log}";
                Console.WriteLine(formatedLog);
                _loggingQueue.Enqueue((formatedLog, color));
            }
            Console.ResetColor(); 

            //Creating a empty line for better readability
            Console.WriteLine("");
            _loggingQueue.Enqueue(("", color));

            try
            {
                using StreamWriter streamWriter = new(path: _pathToLoggingFile, append: true);
                foreach ((var content, ConsoleColor colorInQueue) in _loggingQueue)
                {
                    await streamWriter.WriteLineAsync(content);
                }
                _loggingQueue.Clear();
            }
            catch (Exception) { }
        }

        #region Exceptions

        internal static void LogException(Exception ex)
        {
            StackTrace stackTrace = new(ex, true);
            StackFrame? stackFrame = null;
            foreach (StackFrame item in stackTrace.GetFrames())
            {
                //Looking for the frame contains the infos about the error
                if (item.GetMethod()?.Name != null && item.GetFileName() != null)
                {
                    stackFrame = item;
                    break;
                }
            }

            Console.Beep();
            Console.Beep();
            if (stackFrame == null)
            {
                _ = LogAsync(ConsoleColor.Red, $"ERROR: {ex.Message}");
                return;
            }

            var methodName = stackFrame?.GetMethod()?.Name + "()";
            var filename = stackFrame?.GetFileName() ?? "missing filename";
            var lineNum = stackFrame?.GetFileLineNumber();
            var columnNum = stackFrame?.GetFileColumnNumber();

            //The var filename is the whole path to the file. This shortens it down to the filename 
            var index = filename.LastIndexOf(@"\") + 1;
            filename = filename.Remove(0, index);

            var errorInfos = $"ERROR in file {filename}, in {methodName}, at line: {lineNum}, at column: {columnNum}";
            var errorMessage = $"ERROR: {ex.Message}";

            _ = LogAsync(ConsoleColor.Red, [errorInfos, errorMessage]);
        }

        #endregion
    }
}
