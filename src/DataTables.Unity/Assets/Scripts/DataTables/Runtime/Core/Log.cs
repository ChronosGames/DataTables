using System;

namespace DataTables
{
    public static class Log
    {
        public enum Level { Trace, Debug, Info, Warning, Error, Critical }

        static Action<Level, string, Exception?> s_Sink = DefaultSink;

        public static void Configure(Action<Level, string, Exception?> sink)
        {
            s_Sink = sink ?? DefaultSink;
        }

        public static void Trace(string message) => s_Sink(Level.Trace, message, null);
        public static void Debug(string message) => s_Sink(Level.Debug, message, null);
        public static void Info(string message) => s_Sink(Level.Info, message, null);
        public static void Warning(string message) => s_Sink(Level.Warning, message, null);
        public static void Error(string message, Exception? ex = null) => s_Sink(Level.Error, message, ex);
        public static void Critical(string message, Exception? ex = null) => s_Sink(Level.Critical, message, ex);

        static void DefaultSink(Level level, string message, Exception? ex)
        {
#if UNITY_5_3_OR_NEWER
            var full = ex == null ? message : message + "\n" + ex;
            switch (level)
            {
                case Level.Warning:
                    UnityEngine.Debug.LogWarning(full);
                    break;
                case Level.Error:
                case Level.Critical:
                    UnityEngine.Debug.LogError(full);
                    break;
                default:
                    UnityEngine.Debug.Log(full);
                    break;
            }
#else
            var full = ex == null ? message : message + Environment.NewLine + ex;
            switch (level)
            {
                case Level.Warning:
                    Console.WriteLine("[WARN] " + full);
                    break;
                case Level.Error:
                case Level.Critical:
                    Console.Error.WriteLine("[ERROR] " + full);
                    break;
                default:
                    Console.WriteLine(full);
                    break;
            }
#endif
        }
    }
}

