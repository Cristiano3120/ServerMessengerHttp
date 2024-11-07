using System.Collections.Concurrent;
using System.Diagnostics;

namespace ServerMessengerHttp
{
    internal static class Logger
    {
        private static readonly ConcurrentQueue<string> _loggingQueue;
        private const string _pathToLoggingFile = @"C:\Users\Crist\source\repos\ServerMessengerHttp\ServerMessengerHttp\NeededFiles\LoggingFile.txt";


        static Logger()
        {
            _loggingQueue = new ConcurrentQueue<string>();
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
                _loggingQueue.Enqueue(formatedLog);
            }
            try
            {
                using StreamWriter streamWriter = new(path: _pathToLoggingFile, append: true);

                foreach (var item in _loggingQueue)
                {
                    await streamWriter.WriteLineAsync(item);
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

            if (stackFrame != null)
            {
                var methodName = stackFrame?.GetMethod()?.Name + "()";
                var filename = stackFrame?.GetFileName() ?? "missing filename";
                var lineNum = stackFrame?.GetFileLineNumber();
                var columnNum = stackFrame?.GetFileColumnNumber();

                //The var filename is the whole path to the file. This shortens it down to the filename 
                var index = filename.LastIndexOf(@"\") + 1;
                filename = filename.Remove(0, index);

                var errorInfos = $"ERROR in file {filename}, in {methodName}, at line: {lineNum}, at column: {columnNum}";
                var errorMessage = $"ERROR: {ex.Message}";

                _ = LogAsync([errorInfos, errorMessage]);
            }
            else
            {
                _ = LogAsync($"ERROR: {ex.Message}");
            }
        }

        #endregion
    }
}
