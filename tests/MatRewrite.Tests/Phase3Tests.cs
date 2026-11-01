using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase3Tests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "mrp3_" + Guid.NewGuid().ToString("N"));
    public Phase3Tests() { Directory.CreateDirectory(dir); }
    public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

    void File(string name, string text) => System.IO.File.WriteAllText(Path.Combine(dir, name), text);

    (Interpreter it, StringWriter o) Make(Queue<string>? input = null)
    {
        var o = new StringWriter();
        var it = new Interpreter(o);
        it.SearchPath.Add(dir);
        if (input != null) it.InputReader = p => { o.Write(p); return input.Count > 0 ? input.Dequeue() : null; };
        return (it, o);
    }

    string Run(string code, Queue<string>? input = null)
    {
        var (it, o) = Make(input);
        it.Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    // ---- ODE
    [Fact] public void Ode45Decay() => Assert.Equal("1", Run("[t,y]=ode45(@(t,y) -y,[0 1],1); disp(abs(y(end)-exp(-1))<1e-6)"));
    [Fact] public void Ode23Oscillator() => Assert.Equal("1", Run("[t,y]=ode23(@(t,y)[y(2);-y(1)],[0 pi],[0;1]); disp(abs(y(end,2)+1)<1e-2)"));
    [Fact] public void Ode45Tspan() => Assert.Equal("5 1", Run("o=odeset('RelTol',1e-8,'AbsTol',1e-10); [t,y]=ode45(@(t,y) -2*t*y, linspace(0,2,5),1,o); printf('%d %d', numel(t), max(abs(y-exp(-t.^2)))<1e-6)"));
    [Fact] public void Ode45Struct() => Assert.Equal("ode45", Run("s=ode45(@(t,y) y,[0 1],1); disp(s.solver)"));
    [Fact] public void OdeGet() => Assert.Equal("0.001", Run("o=odeset('reltol',1e-3); printf('%g', odeget(o,'RelTol'))"));

    // ---- sparse
    [Fact] public void SparseDisplay() => Assert.Contains("Compressed Column Sparse (rows = 3, cols = 3, nnz = 2", Run("S = sparse([1 2],[1 3],[5 6],3,3)"));
    [Fact] public void SparseAccumulates() => Assert.Equal("3", Run("S = sparse([1 1],[1 1],[1 2]); disp(full(S))"));
    [Fact] public void SparsePredicates() => Assert.Equal("1 0 1 3", Run("S=speye(3); printf('%d %d %d %d', issparse(S), issparse(full(S)), issparse(S+S), nnz(S))"));

    // ---- classdef
    const string Point = @"classdef Point
  properties
    x = 0;
    y = 0;
  end
  properties (Constant)
    K = 42;
  end
  methods
    function obj = Point(x, y)
      if nargin > 0, obj.x = x; obj.y = y; end
    end
    function d = len(obj)
      d = sqrt(obj.x^2 + obj.y^2);
    end
    function obj = move(obj, dx)
      obj.x = obj.x + dx;
    end
    function r = plus(a, b)
      r = Point(a.x + b.x, a.y + b.y);
    end
  end
  methods (Static)
    function p = unit()
      p = Point(1, 0);
    end
  end
end
";
    const string Counter = @"classdef Counter < handle
  properties
    n = 0
  end
  methods
    function inc(obj)
      obj.n = obj.n + 1;
    end
  end
end
";
    [Fact] public void ClassValueSemantics() { File("Point.m", Point); Assert.Equal("5 5 3 4", Run("p=Point(3,4); q=p.move(1); printf('%g %g %g %g', len(p), p.len(), p.x, q.x)")); }
    [Fact] public void ClassOperatorAndStatic() { File("Point.m", Point); Assert.Equal("7 1 42", Run("p=Point(3,4)+Point(4,0); u=Point.unit(); printf('%g %g %g', p.x, u.x, Point.K)")); }
    [Fact] public void ClassHandleSemantics() { File("Counter.m", Counter); Assert.Equal("2 1 1", Run("c=Counter(); d=c; c.inc(); inc(d); printf('%d %d %d', c.n, c==d, isa(c,'handle'))")); }
    [Fact] public void ClassInheritance()
    {
        File("Counter.m", Counter);
        File("Named.m", "classdef Named < Counter\n properties\n label = 'a'\n end\n methods\n function obj = Named(l)\n obj = obj@Counter();\n obj.label = l;\n end\n end\nend\n");
        Assert.Equal("foo 1 1 Named", Run("n=Named('foo'); n.inc(); printf('%s %d %d %s', n.label, n.n, isa(n,'Counter'), class(n))"));
    }
    [Fact] public void ClassUnknownProperty() { File("Point.m", Point); Assert.Contains("unknown property", Run("p=Point(); try, p.z=1; catch e, disp(e.message); end")); }
    [Fact] public void ClassDisplay() { File("Point.m", Point); Assert.Contains("Point object with properties", Run("p = Point(1,2)")); }

    // ---- input / keyboard / debugger
    [Fact] public void InputNumeric() => Assert.Equal("x? 6", Run("v = input('x? '); printf('%d', v*2)", new Queue<string>(new[] { "3" })));
    [Fact] public void InputString() => Assert.Equal("n? hi!", Run("v = input('n? ', 's'); printf('%s!', v)", new Queue<string>(new[] { "hi" })));
    [Fact] public void KeyboardInspectsScope()
    {
        var r = Run("a = 7; keyboard; disp(a)", new Queue<string>(new[] { "disp(a*2)", "a = 1;", "dbcont" }));
        Assert.Contains("14", r);
        Assert.EndsWith("1", r);
    }
    [Fact] public void BreakpointAndStep()
    {
        File("bpf.m", "function r = bpf(x)\n  y = x + 1;\n  z = y * 2;\n  r = z;\nend\n");
        var (it, o) = Make(new Queue<string>(new[] { "disp(y)", "dbstep", "disp(z)", "dbcont" }));
        var lines = new List<int>();
        it.Paused += info => lines.Add(info.Line);
        it.Eval("dbstop bpf 3\nr = bpf(4); disp(r)");
        var s = o.ToString();
        Assert.Equal(new[] { 3, 4 }, lines);
        Assert.Contains("5", s);
        Assert.Contains("10", s);
    }
    [Fact] public void DbQuitAbortsExecution()
    {
        var (it, o) = Make(new Queue<string>(new[] { "dbquit" }));
        Assert.Throws<InterruptException>(() => it.Eval("keyboard; disp('after')"));
        Assert.DoesNotContain("after", o.ToString());
    }
    [Fact] public void InterruptStopsLoop()
    {
        var (it, o) = Make();
        var th = new Thread(() => { Thread.Sleep(200); it.RequestInterrupt(); });
        th.Start();
        Assert.Throws<InterruptException>(() => it.Eval("k = 0; while true, k = k + 1; end"));
        th.Join();
        it.Eval("disp(k > 0)");
        Assert.EndsWith("1", o.ToString().Trim());
    }
    [Fact] public void TryDoesNotCatchInterrupt()
    {
        var (it, _) = Make();
        new Thread(() => { Thread.Sleep(150); it.RequestInterrupt(); }).Start();
        Assert.Throws<InterruptException>(() => it.Eval("try, while true, end, catch, disp('caught'), end"));
    }
}
