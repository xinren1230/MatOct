using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase5Tests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    // ---- syntax
    [Fact] public void DigitSeparators() => Assert.Equal("10000", Run("printf('%d', 10_000)"));
    [Fact] public void PostIncInCondition() => Assert.Equal("3", Run("it = 0; while it++ < 2, end; printf('%d', it)"));
    [Fact] public void ClassdefEndKeywords() => Assert.Equal("ok", Run("x = 1; if x, printf('ok'); endif"));

    // ---- new builtins
    [Fact] public void Sub2IndInd2Sub() => Assert.Equal("8 2 3", Run("i = sub2ind([3 4], 2, 3); [r, c] = ind2sub([3 4], i); printf('%d %d %d', i, r, c)"));
    [Fact] public void NthElement() => Assert.Equal("3 | 3 1 2 8 9 7", Run("printf('%d', nth_element([5 3 1 4], 2)); printf(' |'); printf(' %d', nth_element(magic(3), [1 3])')"));
    [Fact] public void FilterIir() => Assert.Equal("1.00 1.50 1.75", Run("printf('%.2f ', filter(1, [1 -0.5], [1 1 1]))").Trim());
    [Fact] public void FilterFinalState() => Assert.Equal("0.5", Run("[y, zf] = filter([1 1]/2, 1, [1 2 3]); printf('%g', zf - 1)"));
    [Fact] public void Pow2() => Assert.Equal("8 6", Run("printf('%g %g', pow2(3), pow2(1.5, 2))"));
    [Fact] public void Quadcc() => Assert.Equal("0.333333", Run("printf('%.6f', quadcc(@(x) x.^2, 0, 1))"));
    [Fact] public void Schur() => Assert.Equal("1", Run("A = [4 1; 2 3]; [U, T] = schur(A); printf('%d', norm(U*T*U' - A) < 1e-10)"));
    [Fact] public void Strptime() => Assert.Equal("2020 4", Run("[tm, n] = strptime('2020-05-17', '%Y-%m-%d'); printf('%d %d', tm.year + 1900, tm.mon)"));
    [Fact] public void IsnaNd() => Assert.Equal("1 1 1 2 2", Run("a = isna(zeros(1,1,1,2)); printf('%d ', size(a)); printf('%d', numel(a))"));
    [Fact] public void EmptyShortCircuitIsFalse() => Assert.Equal("no", Run("x = []; if x >= 0 && true, printf('yes'); else printf('no'); end"));

    // ---- MFileFirst set
    [Fact] public void MFileFirstContainsTunedNames()
    {
        Assert.Contains("interp1", Interpreter.MFileFirst);
        Assert.Contains("var", Interpreter.MFileFirst);
        Assert.DoesNotContain("figure", Interpreter.MFileFirst);
    }

    // ---- in-place assignment fast path must keep value semantics
    [Fact] public void InPlaceAssignCopySemantics() =>
        Assert.Equal("1 0 0|1 5 0|1 5 7|10 20 30", Run(
            "v = zeros(1,3); v(1) = 1; w = v; v(2) = 5; c = {}; c{1} = v; v(3) = 7; f = @() v; v(1) = 9;" +
            "printf('%d ', w); printf('|'); printf('%d ', c{1}); printf('|'); printf('%d ', f()); printf('|');" +
            "for k = 1:3, v(k) = k*10; end; printf('%d ', v)").Replace(" |", "|").Trim());
    [Fact] public void InPlaceCellAppendCopySemantics() =>
        Assert.Equal("1 2 5", Run("c = {}; c{1} = 1; d = c; c{2} = 2; c{1} = 5; printf('%d %d %d', numel(d), numel(c), c{1})"));
    [Fact] public void FunctionParamNotAliased() =>
        Assert.Equal("0 0", Run("function r = g(p)\n p(1) = 42; r = p;\nend\nv = zeros(1,2); v(2) = 0; q = g(v); printf('%d %d', v)"));
}
