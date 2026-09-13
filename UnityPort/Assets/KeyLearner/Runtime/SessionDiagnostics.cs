using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace KeyLearner.Unity
{
    // Explicitly supplied aggregate records only. Never subscribes to arbitrary logs,
    // serializes KeyEvent/KeySnapshot, or writes from a native keyboard callback.
    public sealed class SessionDiagnostics : IDisposable
    {
        public const long MaximumBytes = 2 * 1024 * 1024;
        StreamWriter writer;
        public string DirectoryPath { get; }
        public string FilePath { get; private set; } = "";
        public bool Available => writer != null;
        public string Failure { get; private set; } = "";
        public SessionDiagnostics(string profileRoot)
        {
            DirectoryPath = Path.Combine(profileRoot, "diagnostics");
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                // Retain at most five previous owned sessions; never touch other files.
                foreach (var old in new DirectoryInfo(DirectoryPath).GetFiles("input-session-*.jsonl").Where(f => System.Text.RegularExpressions.Regex.IsMatch(f.Name, @"^input-session-\d{8}-\d{6}-[a-f0-9]{8}\.jsonl$")).OrderByDescending(f => f.LastWriteTimeUtc).Skip(5))
                    try { if ((old.Attributes & FileAttributes.ReparsePoint) == 0) old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                FilePath = Path.Combine(DirectoryPath, "input-session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".jsonl");
                writer = new StreamWriter(new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { Failure = e.GetType().Name; }
        }
        public void Write(string kind, object data)
        {
            if (writer == null) return;
            try
            {
                string line = JsonSerializer.Serialize(new { schema = 1, utc = DateTime.UtcNow.ToString("O"), kind, data });
                if (writer.BaseStream.Position + Encoding.UTF8.GetByteCount(line) + 2 > MaximumBytes) { Dispose(); return; }
                writer.WriteLine(line);
            }
            catch (Exception e)
            {
                // A failed diagnostic disk must never stop gameplay or input cleanup.
                Failure = e.GetType().Name;
                Dispose();
            }
        }
        public void Dispose()
        {
            var owned = writer; writer = null;
            try { owned?.Dispose(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // A fixed, mouse-independent introduction after Unity's own splash. The timer
    // starts on the first visible repaint, so initialization cannot consume it.
    public sealed class StartupIntroduction
    {
        double firstPaint = double.NaN;
        public bool Completed { get; private set; }
        public bool Active => !Completed;
        public double Progress(double now) => double.IsNaN(firstPaint) ? 0 : Math.Min(1, Math.Max(0, (now - firstPaint) / 4));
        public void Painted(double now, bool unityFinished) { if (unityFinished && double.IsNaN(firstPaint)) firstPaint = now; }
        public void Tick(double now) { if (Progress(now) >= 1) Completed = true; }
    }
}
