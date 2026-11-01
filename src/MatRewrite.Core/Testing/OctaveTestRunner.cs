// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Testing;

/// <summary>One %! block of a GNU Octave source file.</summary>
public sealed record TestBlock(string Kind, string Header, string Code, int Line);

public sealed record TestFailure(int Line, string Kind, string Message);

public sealed class FileTestResult
{
    public string File = "";
    public int Pass, Fail, Skip;
    public List<TestFailure> Failures = new();
    public int Total => Pass + Fail;
}

/// <summary>
/// Runs the built-in self tests of GNU Octave source files (%!test, %!assert, %!error, %!warning,
/// %!shared, %!function ... %!endfunction) against this interpreter. %!xtest, %!testif and %!demo blocks are skipped.
/// </summary>
public static class OctaveTestRunner
{
    static readonly Regex Kw = new(@"^(test|assert|error|warning|fail|shared|function|endfunction|xtest|testif|demo|eval|shared|ok|end)\b");

    static readonly Regex BugTag = new(@"^\s*<\*?\d+(,\s*\*?\d+)*>");
    public static List<TestBlock> ParseBlocks(string src)
    {
        var blocks = new List<TestBlock>();
        var lines = src.Replace("\r\n", "\n").Split('\n');
        string? kind = null, header = ""; var code = new StringBuilder(); int start = 0;
        void Flush()
        {
            if (kind != null) blocks.Add(new TestBlock(kind, header ?? "", code.ToString(), start));
            kind = null; code.Clear();
        }
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (!l.StartsWith("%!")) { Flush(); continue; }
            var body = l[2..];
            var m = Kw.Match(body);
            if (m.Success && body.Length > 0 && !char.IsWhiteSpace(body[0]))
            {
                Flush();
                kind = m.Value; start = i + 1;
                var rest = body[m.Length..];
                if (kind is "test" or "assert" or "xtest" or "fail") rest = BugTag.Replace(rest, "");
                if (kind is "test" or "xtest" or "demo" or "shared" or "function" or "endfunction")
                {
                    // header text on the same line is code for test/demo; for shared it is the variable list
                    header = rest.Trim();
                    if (kind is "test" or "xtest" or "demo") { code.Append(rest).Append('\n'); header = ""; }
                    if (kind == "function") { header = ""; code.Append("function").Append(rest).Append('\n'); }
                }
                else { header = ""; code.Append(rest).Append('\n'); }
                if (kind == "endfunction") Flush();
            }
            else if (kind != null) code.Append(body).Append('\n');
        }
        Flush();
        // merge function bodies: function block code already contains the body until endfunction
        return blocks;
    }

    /// <summary>Run all test blocks of one file.</summary>
    public static FileTestResult RunFile(string path, int blockTimeoutMs = 10000, IEnumerable<string>? searchPath = null, Action<Interpreter>? configure = null)
    {
        var res = new FileTestResult { File = path };
        List<TestBlock> blocks;
        try { blocks = ParseBlocks(File.ReadAllText(path)); }
        catch (Exception ex) { res.Fail++; res.Failures.Add(new TestFailure(0, "read", ex.Message)); return res; }
        var sink = new StringWriter();
        var it = new Interpreter(sink);
        if (searchPath != null) { foreach (var p in searchPath) it.SearchPath.Add(p); it.PreferBuiltins = true; }
        configure?.Invoke(it);
        var shared = new List<string>();

        (List<string>, List<string>)? warnBase = null;
        foreach (var b in blocks)
        {
            switch (b.Kind)
            {
                case "demo": case "endfunction": continue;
                case "xtest": case "testif": res.Skip++; continue;
                case "function":
                    try { it.Eval(b.Code + "\nendfunction\n"); }
                    catch (Exception ex) { res.Fail++; res.Failures.Add(new TestFailure(b.Line, "function", Msg(ex))); }
                    continue;
                case "shared":
                {
                    shared = b.Header.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
                    it.Base.Vars.Clear();
                    foreach (var v in shared) it.Base.Vars[v] = MatrixValue.Empty();
                    if (b.Code.Trim().Length > 0)
                    {
                        var (ok, err) = Exec(it, b.Code, blockTimeoutMs);
                        if (!ok) { res.Fail++; res.Failures.Add(new TestFailure(b.Line, "shared", err!)); }
                    }
                    continue;
                }
            }
            // test blocks run as functions in Octave: undo "local" warning state changes of earlier blocks
            if (warnBase == null) warnBase = (it.DisabledWarnings.ToList(), it.EnabledWarnings.ToList());
            else { it.DisabledWarnings.Clear(); foreach (var w in warnBase.Value.Item1) it.DisabledWarnings.Add(w); it.EnabledWarnings.Clear(); foreach (var w in warnBase.Value.Item2) it.EnabledWarnings.Add(w); }
            // fresh workspace containing only the shared variables
            foreach (var k in it.Base.Vars.Keys.Where(k => !shared.Contains(k)).ToList()) it.Base.Vars.Remove(k);
            string code = b.Code;
            // Octave runs each block as a function: shared variables are written back only when the block completes normally
            var snap = shared.Where(it.Base.Vars.ContainsKey).ToDictionary(k => k, k => it.Base.Vars[k]);
            bool pass; string? msg = null;
            switch (b.Kind)
            {
                case "test":
                case "eval":
                {
                    var (ok, err) = Exec(it, code, blockTimeoutMs);
                    pass = ok; msg = err;
                    break;
                }
                case "assert":
                case "fail":
                {
                    var (ok, err) = Exec(it, b.Kind + " " + code.Trim() + "\n", blockTimeoutMs);
                    if (!ok && code.TrimStart().StartsWith("(")) { pass = false; msg = err; break; }
                    pass = ok; msg = err;
                    break;
                }
                case "error":
                case "warning":
                {
                    var (pattern, id, rest) = SplitErrorHeader(code);
                    it.LastWarning = ""; it.LastWarningId = "";
                    var (ok, err, errId) = ExecWithId(it, rest, blockTimeoutMs);
                    if (b.Kind == "error")
                    {
                        if (ok) { pass = false; msg = "expected an error but none was raised"; }
                        else if (err == "timeout") { pass = false; msg = err; }
                        else if (id != null) { pass = errId == id; if (!pass) msg = $"expected error id '{id}', got '{errId}': {err}"; }
                        else if (pattern != null) { pass = SafeMatch(err!, pattern); if (!pass) msg = $"error '{err}' does not match <{pattern}>"; }
                        else pass = true;
                    }
                    else
                    {
                        if (!ok) { pass = false; msg = err; }
                        else if (it.LastWarning == "") { pass = false; msg = "expected a warning but none was issued"; }
                        else if (id != null) { pass = it.LastWarningId == id; if (!pass) msg = $"expected warning id '{id}', got '{it.LastWarningId}'"; }
                        else if (pattern != null) { pass = SafeMatch(it.LastWarning, pattern); if (!pass) msg = $"warning '{it.LastWarning}' does not match <{pattern}>"; }
                        else pass = true;
                    }
                    break;
                }
                default: res.Skip++; continue;
            }
            if (b.Kind is "error" or "warning" || !pass)
                foreach (var kv in snap) it.Base.Vars[kv.Key] = kv.Value;
            if (pass) res.Pass++;
            else
            {
                res.Fail++;
                res.Failures.Add(new TestFailure(b.Line, b.Kind, (msg ?? "failed").Replace("\n", " ").Trim()));
                if (msg == "timeout") break;   // interpreter may be wedged
            }
        }
        return res;
    }

    static bool SafeMatch(string text, string pattern)
    {
        try { return Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)); }
        catch { return text.Contains(pattern); }
    }

    /// <summary>Split "%!error &lt;pattern&gt; code" / "%!error id=X:y code".</summary>
    static (string? pattern, string? id, string code) SplitErrorHeader(string code)
    {
        string? pattern = null, id = null;
        var c = code.TrimStart();
        for (int guard = 0; guard < 3; guard++)
        {
            if (c.StartsWith("<"))
            {
                int close = c.IndexOf('>');
                // allow '>' inside a pattern when followed by more pattern text: take the '>' that is followed by whitespace
                var m = Regex.Match(c, @"^<(.*?)>(\s|$)", RegexOptions.Singleline);
                if (m.Success) { pattern = m.Groups[1].Value; c = c[m.Length..].TrimStart(); }
                else if (close > 0) { pattern = c[1..close]; c = c[(close + 1)..].TrimStart(); }
                else break;
            }
            else if (c.StartsWith("id="))
            {
                var m = Regex.Match(c, @"^id=(\S+)");
                id = m.Groups[1].Value; c = c[m.Length..].TrimStart();
            }
            else break;
        }
        return (pattern, id, c);
    }

    static string Msg(Exception ex) => Interpreter.ErrorInfo(ex).msg;

    static (bool ok, string? err) Exec(Interpreter it, string code, int timeoutMs)
    {
        var (ok, err, _) = ExecWithId(it, code, timeoutMs);
        return (ok, err);
    }

    static (bool ok, string? err, string? id) ExecWithId(Interpreter it, string code, int timeoutMs)
    {
        bool ok = false; string? err = null, id = null;
        var t = new Thread(() =>
        {
            try { it.Eval(code); ok = true; }
            catch (Exception ex)
            {
                var info = Interpreter.ErrorInfo(ex);
                err = info.msg; id = info.id;
                if (ex is StackOverflowException or InsufficientExecutionStackException) err = "stack overflow";
            }
        }, 64 * 1024 * 1024) { IsBackground = true };
        t.Start();
        if (!t.Join(timeoutMs))
        {
            it.RequestInterrupt();
            if (!t.Join(2000)) return (false, "timeout", null);
            return (false, "timeout", null);
        }
        return (ok, err, id);
    }

    // ------------------------------------------------------------------ reports
    public sealed record AreaSummary(string Area, int Files, int Pass, int Fail, int Skip)
    {
        public double Rate => Pass + Fail == 0 ? 0 : 100.0 * Pass / (Pass + Fail);
    }

    public static List<AreaSummary> Summarize(IEnumerable<(string area, FileTestResult r)> results) =>
        results.GroupBy(x => x.area).Select(g => new AreaSummary(g.Key, g.Count(), g.Sum(x => x.r.Pass), g.Sum(x => x.r.Fail), g.Sum(x => x.r.Skip)))
               .OrderBy(a => a.Area, StringComparer.Ordinal).ToList();

    /// <summary>Machine-readable one-line result used between the driver and per-file child processes.</summary>
    public static string Serialize(FileTestResult r)
    {
        var sb = new StringBuilder();
        sb.Append($"@@RESULT {r.Pass} {r.Fail} {r.Skip}\n");
        foreach (var f in r.Failures) sb.Append($"@@FAIL {f.Line}\t{f.Kind}\t{f.Message.Replace("\t", " ")}\n");
        return sb.ToString();
    }

    public static FileTestResult? Deserialize(string file, string output)
    {
        FileTestResult? r = null;
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("@@RESULT "))
            {
                var p = line[9..].Trim().Split(' ');
                r = new FileTestResult { File = file, Pass = int.Parse(p[0]), Fail = int.Parse(p[1]), Skip = int.Parse(p[2]) };
            }
            else if (line.StartsWith("@@FAIL ") && r != null)
            {
                var p = line[7..].Split('\t', 3);
                if (p.Length == 3) r.Failures.Add(new TestFailure(int.TryParse(p[0], out var ln) ? ln : 0, p[1], p[2].TrimEnd('\r')));
            }
        }
        return r;
    }
}
