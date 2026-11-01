using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase10Tests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    [Fact] public void RowAppend() => Assert.Equal("1 1000 500500", Run("v=[]; for k=1:1000, v(end+1)=k; end; printf('%d ', size(v), sum(v)); ").Trim());
    [Fact] public void ColAppend() => Assert.Equal("4 1", Run("v=[1;2]; for k=1:2, v(end+1)=k; end; printf('%d ', size(v))").Trim());
    [Fact] public void AppendAliasSafe() => Assert.Equal("3 4", Run("v=[]; for k=1:3, v(end+1)=k; end; w=v; w(end+1)=9; printf('%d ', numel(v), numel(w))").Trim());
    [Fact] public void AppendThenWrite() => Assert.Equal("1 7 3 4", Run("v=[]; for k=1:3, v(end+1)=k; end; v(2)=7; v(end+1)=4; printf('%d ', v)").Trim());
    [Fact] public void CellAppend() => Assert.Equal("1 500 500", Run("c={}; for k=1:500, c{end+1}=sprintf('%d',k); end; printf('%d ', size(c)); printf('%s', c{end})"));
    [Fact] public void CellAppendAlias() => Assert.Equal("2 3", Run("c={}; c{end+1}=1; c{end+1}=2; d=c; d{end+1}=3; printf('%d ', numel(c), numel(d))").Trim());

    // ---- phase 10 round 2
    [Fact] public void SparseKron() => Assert.Equal("2 4", Run("printf('%d ', size(kron(sparse(eye(2)),[1 2])))").Trim());
    [Fact] public void Meshgrid3D() => Assert.Equal("3 2 4", Run("[x,y,z]=meshgrid(1:2,1:3,1:4); printf('%d ', size(z))").Trim());
    [Fact] public void Stream2Count() => Assert.Equal("1", Run("[x,y]=meshgrid(1:5); xy=stream2(x,y,ones(5),zeros(5),1,1); printf('%d', numel(xy))"));
    [Fact] public void Gradient() => Assert.Equal("3 4 6 7", Run("printf('%g ', gradient([1 4 9 16]))").Trim());
    [Fact] public void ExpmComplex() => Assert.Equal("0.5403 0.8415", Run("e=expm([0 1i;1i 0]); printf('%.4f ', real(e(1)), imag(e(2)))").Trim());
    [Fact] public void StructCsListAssign() => Assert.Equal("5 6", Run("s=struct('a',{1,2}); [s.a]=deal(5,6); printf('%d ', s.a)").Trim());
    [Fact] public void EigComplexVectors() => Assert.Equal("0.7071", Run("[v,d]=eig([0 1;-1 0]); printf('%.4f', imag(v(2,1)))"));
    [Fact] public void Condeig() => Assert.Equal("1.0150 1.0150", Run("printf('%.4f ', condeig([1 2;3 4]))").Trim());
    [Fact] public void EigGeneralized() => Assert.Equal("0.5 2", Run("printf('%g ', eig([2 0;0 1],[1 0;0 2]))").Trim());
}
