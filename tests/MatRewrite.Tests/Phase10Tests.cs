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
}
