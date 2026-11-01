using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase7Tests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    [Fact] public void Atan2d() => Assert.Equal("45", Run("printf('%g', atan2d(1, 1))"));
    [Fact] public void Nthroot() => Assert.Equal("-3", Run("printf('%g', nthroot(-27, 3))"));
    [Fact] public void Nextpow2() => Assert.Equal("10", Run("printf('%d', nextpow2(1000))"));
    [Fact] public void Blkdiag() => Assert.Equal("1 0 0 2", Run("printf('%d ', blkdiag(1, 2))"));
    [Fact] public void Cumtrapz() => Assert.Equal("0 1.5 4", Run("printf('%g ', cumtrapz([1 2 3]))"));
    [Fact] public void Bitset() => Assert.Equal("5", Run("printf('%d', bitset(1, 3))"));
    [Fact] public void Toeplitz() => Assert.Equal("1 2 2 1", Run("printf('%d ', toeplitz([1 2]))"));
    [Fact] public void Hadamard4RowSums() => Assert.Equal("4 0 0 0", Run("printf('%d ', sum(hadamard(4), 2))"));
    [Fact] public void SortrowsDescendingColumn() => Assert.Equal("3 2 1", Run("printf('%d ', sortrows([1;3;2], -1))"));
    [Fact] public void SortrowsSparseStaysSparse() => Assert.Equal("1", Run("printf('%d', issparse(sortrows(sparse([2;1]))))"));
    [Fact] public void Perms() => Assert.Equal("6 3", Run("printf('%d ', size(perms(1:3)))"));
    [Fact] public void UnionSorted() => Assert.Equal("1 2 3 4", Run("printf('%d ', union([3 1], [2 4 1]))"));
    [Fact] public void Setxor() => Assert.Equal("1 4", Run("printf('%d ', setxor([1 2 3], [2 3 4]))"));
    [Fact] public void IsmemberIndex() => Assert.Equal("1 2", Run("[tf, loc] = ismember(5, [4 5]); printf('%d %d', tf, loc)"));
    [Fact] public void MedianEven() => Assert.Equal("2.5", Run("printf('%g', median([1 2 3 4]))"));
    [Fact] public void StdSample() => Assert.Equal("1", Run("printf('%g', std([1 2 3]))"));
    [Fact] public void Movmad() => Assert.Equal("3", Run("printf('%d', numel(movmad([1 2 3], 3)))"));
    [Fact] public void Erase() => Assert.Equal("hlo", Run("printf('%s', erase('hello', 'el'))"));
    [Fact] public void Validatestring() => Assert.Equal("apple", Run("printf('%s', validatestring('app', {'apple', 'banana'}))"));
    [Fact] public void CompareVersions() => Assert.Equal("1", Run("printf('%d', compare_versions('1.10.0', '1.9.0', '>'))"));
}
