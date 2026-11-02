using System.IO;
using MatRewrite.Core.Runtime;
using MatRewrite.Core.Testing;
using Xunit;

namespace MatRewrite.Tests;

public class CompatTests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    // ---- Octave syntax extensions
    [Fact] public void DefaultParams() => Assert.Equal("3 11", Run("function r = f(a, k = 1)\n r = a + k;\nend\nprintf('%d %d', f(2), f(1, 10))"));
    [Fact] public void ChainedAssign() => Assert.Equal("5 5", Run("a = b = 5; printf('%d %d', a, b)"));
    [Fact] public void ParenAssign() => Assert.Equal("4 4", Run("y = (x = 4); printf('%d %d', x, y)"));
    [Fact] public void CompoundOps() => Assert.Equal("6 1", Run("a = [1 2 3]; a .*= 2; b = false; b |= true; printf('%d %d', a(3), b)"));
    [Fact] public void PrefixIncrement() => Assert.Equal("2 3 2", Run("v = {1,2,3}; k = 0; a = v{++k}; b = v{++k}; c = v{++k}; printf('%d %d %d', b, c, k-1)"));

    // ---- warnings
    [Fact] public void LastWarn() => Assert.EndsWith("boom|my:id", Run("warning('my:id', 'boom'); [m, id] = lastwarn(); printf('%s|%s', m, id)"));
    [Fact] public void WarningOff() => Assert.Equal("", Run("warning('off', 'my:id'); warning('my:id', 'silent');"));

    // ---- compat builtins
    [Fact] public void CumMax() => Assert.Equal("1 3 3 5 | 1 2 2 4", Run("[m,i] = cummax([1 3 2 5]); printf('%d ', m); printf('| '); printf('%d ', i)"));
    [Fact] public void CumMin() => Assert.Equal("3 1 1", Run("printf('%d ', cummin([3 1 2]))"));
    [Fact] public void MergeIfelse() => Assert.Equal("1 9", Run("printf('%d ', merge([true false], [1 2], [8 9]))"));
    [Fact] public void Lookup() => Assert.Equal("0 1 2 3", Run("printf('%d ', lookup([1 2 3], [0 1.5 2 9]))"));
    [Fact] public void Signbit() => Assert.Equal("1 0", Run("printf('%d ', signbit([-1 1]))"));
    [Fact] public void IsaCell() => Assert.Equal("0 1", Run("printf('%d ', isa(1, {'char','double'}))"));
    [Fact] public void RootColorOrder() => Assert.Equal("8 3", Run("printf('%d ', size(get(0, 'defaultaxescolororder')))"));
    [Fact] public void SortRowsIdx() => Assert.Equal("2 3 1", Run("printf('%d ', __sort_rows_idx__([3 1; 1 2; 2 0], 'ascend'))"));

    // ---- test runner
    [Fact]
    public void ParseBlocks()
    {
        var b = OctaveTestRunner.ParseBlocks("function x\nend\n%!assert (1, 1)\n%!test <*12345>\n%! a = 1;\n%!error <oops> error ('oops')\n%!xtest\n%! fail\n");
        Assert.Equal(new[] { "assert", "test", "error", "xtest" }, b.Select(x => x.Kind).ToArray());
    }

    [Fact]
    public void RunFile()
    {
        var p = Path.Combine(Path.GetTempPath(), "mrc_" + Guid.NewGuid().ToString("N") + ".m");
        File.WriteAllText(p, "%!assert (1+1, 2)\n%!assert (1, 2)\n%!error <bad> error ('so bad')\n%!shared z\n%! z = 3;\n%!assert (z, 3)\n");
        try
        {
            var r = OctaveTestRunner.RunFile(p);
            Assert.Equal(3, r.Pass);
            Assert.Equal(1, r.Fail);
        }
        finally { File.Delete(p); }
    }
}
