using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase6Tests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    // ---- containers.Map
    [Fact] public void MapCountUint64() => Assert.Equal("uint64 2", Run("m = containers.Map({'a','b'}, {1, 2}); printf('%s %d', class(m.Count), m.Count)"));
    [Fact] public void MapConcat() => Assert.Equal("3", Run("a = containers.Map({'a','b'}, {1, 2}); b = containers.Map({'c'}, {3}); m = [a; b]; printf('%d', m.Count)"));
    [Fact] public void MapBraceError() => Assert.Contains("only '()' indexing", Assert.Throws<OctaveException>(() => Run("m = containers.Map(); m{1}")).Message);
    [Fact] public void MapIsKeyWrongType() => Assert.Equal("0", Run("m = containers.Map({'a'}, {1}); printf('%d', isKey(m, 42))"));
    [Fact] public void MapValuesShape() => Assert.Equal("2 2", Run("m = containers.Map({'a','b','c','d'}, {1,2,3,4}); v = values(m, {'a','b';'c','d'}); printf('%d ', size(v))"));

    // ---- language / builtins
    [Fact] public void ForKeyValueOverStruct() => Assert.Equal("a=1 b=2", Run("s.a = 1; s.b = 2; r = {}; for [v, k] = s, r{end+1} = sprintf('%s=%d', k, v); end; printf('%s', strjoin(r, ' '))"));
    [Fact] public void CellNoArgsIsEmpty() => Assert.Equal("0 0", Run("printf('%d ', size(cell()))"));
    [Fact] public void StructEmptyArray() => Assert.Equal("0 0", Run("printf('%d ', size(struct([])))"));
    [Fact] public void NormRows() => Assert.Equal("5 1", Run("printf('%g ', norm([3 4; 1 0], 2, 'rows'))"));

    // ---- closure compiler keeps semantics
    [Fact] public void CompiledScalarLoop() => Assert.Equal("110", Run("s = 0; for k = 1:10, s = s + k * 2; end; printf('%d', s)"));
    [Fact] public void CompiledShortCircuit() => Assert.Equal("1 0", Run("a = 1; b = []; printf('%d %d', a || b, 0 && error('x'))"));
    [Fact] public void CompiledNotNaNErrors() => Assert.Throws<OctaveException>(() => Run("x = !NaN;"));
    [Fact] public void CompiledIntArith() => Assert.Equal("int8 127", Run("x = int8(100) + 50; printf('%s %d', class(x), x)"));
    [Fact] public void CellAssignInPlaceKeepsValueSemantics() => Assert.Equal("1 9", Run("c = cell(1, 2); c{1} = 1; d = c; for k = 1:1, c{k} = 9; end; printf('%d %d', d{1}, c{1})"));
}
