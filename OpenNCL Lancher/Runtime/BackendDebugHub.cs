using System;
using System.Collections.ObjectModel;
using System.Threading;

namespace OpenNCL_Lancher.Runtime
{
    internal enum BackendDebugLevel
    {
        Info,
        Warn,
        Error,
        Trace,
    }

    internal sealed record BackendDebugEvent(
        DateTime Timestamp,
        BackendDebugLevel Level,
        string Category,
        string Message
    );

    internal static class BackendDebugHub
    {
        private static readonly object Gate = new();
        private static long _seq;

        public static event Action<BackendDebugEvent>? Event;

        public static long NextSeq() => Interlocked.Increment(ref _seq);

        public static void Info(string category, string message) => Emit(BackendDebugLevel.Info, category, message);
        public static void Warn(string category, string message) => Emit(BackendDebugLevel.Warn, category, message);
        public static void Error(string category, string message) => Emit(BackendDebugLevel.Error, category, message);
        public static void Trace(string category, string message) => Emit(BackendDebugLevel.Trace, category, message);

        private static void Emit(BackendDebugLevel level, string category, string message)
        {
            Action<BackendDebugEvent>? handler;
            lock (Gate) handler = Event;
            handler?.Invoke(new BackendDebugEvent(DateTime.Now, level, category, message));
        }
    }
}

