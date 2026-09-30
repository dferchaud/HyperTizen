using System;
using System.Collections.Concurrent;
using System.Linq;

namespace HyperTizen
{
    // Recent log lines kept in memory so they can be read over HTTP when dlog is unavailable.
    public static class Diag
    {
        private const int MaxLines = 400;
        private static readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();

        public static void Log(string message)
        {
            try { Tizen.Log.Debug("HyperTizen", message); } catch { }
            _lines.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff") + " " + message);
            while (_lines.Count > MaxLines && _lines.TryDequeue(out _)) { }
        }

        public static string Dump()
        {
            return string.Join("\n", _lines.ToArray());
        }
    }
}
