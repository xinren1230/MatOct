// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterDebug()
    {
        // dbstop name line [line...] | dbstop in name at line | dbstop("name", lines)
        Reg("dbstop", (it, a, n) =>
        {
            var words = new List<string>();
            foreach (var v in a)
            {
                if (v is MatrixValue m && !m.IsChar) words.AddRange(m.Re.Select(x => ((int)x).ToString()));
                else words.AddRange(S(v, "dbstop").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            }
            words.RemoveAll(w => w is "in" or "at");
            if (words.Count == 0) throw new OctaveException("dbstop: function name required");
            var name = words[0];
            var lines = words.Skip(1).Select(w => int.TryParse(w, out var l) ? l : throw new OctaveException($"dbstop: invalid line '{w}'")).ToList();
            if (lines.Count == 0)
            {
                // break at first executable line: find the file
                var f = it.FindFile(Interpreter.BreakKey(name)) ?? throw new OctaveException($"dbstop: unable to find function '{name}'");
                var fn = it.LoadFile(f, Interpreter.BreakKey(name));
                var first = fn.Body.FirstOrDefault(s => s.Line > 0);
                lines.Add(first?.Line ?? 1);
            }
            foreach (var l in lines) it.SetBreakpoint(name, l);
            return n > 0 ? Out(new MatrixValue(1, lines.Count, lines.Select(x => (double)x).ToArray())) : Array.Empty<Value>();
        });
        Reg0("dbclear", (it, a) =>
        {
            if (a.Length == 0) throw new OctaveException("dbclear: function name required");
            var first = S(a[0], "dbclear");
            if (first == "all") { it.Breakpoints.Clear(); return; }
            var words = new List<string>();
            foreach (var v in a)
            {
                if (v is MatrixValue m && !m.IsChar) words.AddRange(m.Re.Select(x => ((int)x).ToString()));
                else words.AddRange(S(v, "dbclear").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            }
            words.RemoveAll(w => w is "in" or "at");
            if (words.Count == 1) it.ClearBreakpoint(words[0]);
            else foreach (var w in words.Skip(1)) it.ClearBreakpoint(words[0], int.Parse(w));
        });
        Reg("dbstatus", (it, a, n) =>
        {
            if (n == 0)
            {
                foreach (var (k, s) in it.Breakpoints) it.Out.WriteLine($"breakpoint in {k} at line{(s.Count > 1 ? "s" : "")} {string.Join(", ", s)}");
                return Array.Empty<Value>();
            }
            var names = it.Breakpoints.SelectMany(kv => kv.Value.Select(l => (kv.Key, l))).ToList();
            var st = new StructValue(names.Count, 1, new List<string> { "name", "line" });
            for (int i = 0; i < names.Count; i++) { st.Set("name", Str(names[i].Key), i); st.Set("line", Num(names[i].l), i); }
            return Out(st);
        });
        Reg0("dbcont", (it, a) => it.DbContinue());
        Reg0("dbquit", (it, a) => it.DbQuit());
        Reg0("dbstep", (it, a) =>
        {
            var mode = a.Length > 0 ? S(a[0], "dbstep").ToLowerInvariant() : "";
            it.DbStep(mode switch { "in" => StepMode.In, "out" => StepMode.Out, _ => StepMode.Over });
        });
        Reg0("dbup", (it, a) => throw new OctaveException("dbup: not supported yet"));
        Reg("dbstack", (it, a, n) =>
        {
            var frames = it.CallStack.AsEnumerable().Reverse().ToList();
            if (n == 0)
            {
                if (frames.Count > 0) it.Out.WriteLine("stopped in:\n");
                foreach (var f in frames) it.Out.WriteLine($"  --> {f.FrameName} at line {f.Line}");
                return Array.Empty<Value>();
            }
            var st = new StructValue(frames.Count, 1, new List<string> { "file", "name", "line" });
            for (int i = 0; i < frames.Count; i++)
            {
                st.Set("file", Str(frames[i].Func?.File ?? ""), i); st.Set("name", Str(frames[i].FrameName), i); st.Set("line", Num(frames[i].Line), i);
            }
            return Out(st);
        });
        Reg1("isdebugmode", a => Bool(false));
        cur!.Builtins["isdebugmode"] = (it, a, n) => Out(Bool(it.IsPaused));
    }
}
