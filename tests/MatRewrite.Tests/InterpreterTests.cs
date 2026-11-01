using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class InterpreterTests
{
    static (Interpreter it, StringWriter o) New()
    {
        var o = new StringWriter();
        return (new Interpreter(o), o);
    }

    static string Run(string code)
    {
        var (it, o) = New();
        it.Eval(code);
        return o.ToString().Replace("\r\n", "\n");
    }

    static double Num(string code, string var = "r")
    {
        var (it, _) = New();
        it.Eval(code);
        return Convert.ToDouble(Run1(it, var));
    }

    static string Run1(Interpreter it, string var)
    {
        var o = new StringWriter();
        it.Out = o;
        it.Eval($"printf('%.15g', {var});");
        return o.ToString();
    }

    [Fact] public void MatrixTransposeProduct() =>
        Assert.Equal("B =\n\n    5   11\n   11   25\n\n", Run("A=[1 2;3 4]; B=A*A'"));

    [Fact] public void ScalarDisplay() => Assert.Equal("x = 3.5000\n", Run("x = 3.5"));
    [Fact] public void StringDisplay() => Assert.Equal("s = hi\n", Run("s = 'hi'"));
    [Fact] public void ForLoop() => Assert.Equal(55, Num("r=0; for k=1:10, r=r+k; end"));
    [Fact] public void WhileBreak() => Assert.Equal(5, Num("r=0; while true, r=r+1; if r>=5, break; end, end"));
    [Fact] public void IfElse() => Assert.Equal(2, Num("x=3; if x<2, r=1; elseif x<4, r=2; else r=3; end"));
    [Fact] public void Switch() => Assert.Equal(2, Num("s='b'; switch s, case 'a', r=1; case {'b','c'}, r=2; otherwise, r=3; end"));
    [Fact] public void TryCatch() => Assert.Equal("bad 5", Run("try, error('bad %d',5); catch e, printf('%s', e.message); end"));
    [Fact] public void AnonFunction() => Assert.Equal(10, Num("f=@(x) x.^2+1; r=f(3)"));
    [Fact] public void Closure() => Assert.Equal(7, Num("a=4; f=@(x) x+a; a=100; r=f(3)"));
    [Fact] public void IndexingAndGrow() => Assert.Equal(3, Num("x=[]; x(3)=1; r=numel(x)"));
    [Fact] public void LogicalIndex() => Assert.Equal(7, Num("v=1:5; r=sum(v(v>2 & v~=5))"));
    [Fact] public void EndInIndex() => Assert.Equal(4, Num("v=[1 2 3 4]; r=v(end)"));
    [Fact] public void Deletion() => Assert.Equal(2, Num("v=[1 2 3]; v(2)=[]; r=numel(v)"));
    [Fact] public void ColonRange() => Assert.Equal(4, Num("r=numel(0:0.5:1.5)"));
    [Fact] public void Cells() => Assert.Equal(2, Num("c={1,'ab',[1 2]}; r=numel(c{3})"));
    [Fact] public void Structs() => Assert.Equal(3, Num("s.a.b=3; r=s.a.b"));
    [Fact] public void StructArray() => Assert.Equal(2, Num("s(2).x=5; r=numel(s)"));
    [Fact] public void DynamicField() => Assert.Equal(1, Num("f='q'; s.(f)=1; r=s.q"));
    [Fact] public void Det() => Assert.Equal(-2, Num("r=det([1 2;3 4])"), 10);
    [Fact] public void Inv() => Assert.Equal(-2, Num("m=inv([1 2;3 4]); r=m(1,1)"), 10);
    [Fact] public void Backslash() => Assert.Equal(1, Num("A=[2 1;1 3]; x=A\\[3;4]; r=x(1)"), 10);
    [Fact] public void Eig() => Assert.Equal(3, Num("r=max(eig([2 1;1 2]))"), 10);
    [Fact] public void Complex() => Assert.Equal(5, Num("z=3+4i; r=abs(z)"));
    [Fact] public void Sprintf() => Assert.Equal("1-2-3-", Run("printf('%d-', [1 2 3])"));
    [Fact] public void Num2Str() => Assert.Equal("3.1416", Run("printf('%s', num2str(pi))"));
    [Fact] public void Strings() => Assert.Equal("A,B", Run("printf('%s', strjoin(upper(strsplit('a,b', ',')), ','))"));
    [Fact] public void Regexprep() => Assert.Equal("x1x", Run("printf('%s', regexprep('a1b', '[a-z]', 'x'))"));
    [Fact] public void Cellfun() => Assert.Equal(6, Num("r=sum(cellfun(@numel, {'a','bb','ccc'}))"));
    [Fact] public void CellfunNonUniform() => Assert.Equal(2, Num("c=cellfun(@(x) x*2, {1,2}, 'UniformOutput', false); r=c{1}"));
    [Fact] public void Sort() => Assert.Equal(1, Num("[s,i]=sort([3 1 2]); r=i(3)+0*s(1)"));
    [Fact] public void MaxWithIndex() => Assert.Equal(2, Num("[m,i]=max([1 9 3]); r=i"));
    [Fact] public void Find() => Assert.Equal(4, Num("r=sum(find([0 1 0 1 0]) - 1)"));
    [Fact] public void Unique() => Assert.Equal(3, Num("r=numel(unique([3 1 3 2 1]))"));
    [Fact] public void Reshape() => Assert.Equal(3, Num("m=reshape(1:6,2,3); r=m(1,2)"));
    [Fact] public void Magic() => Assert.Equal(34, Num("r=sum(magic(4)(:,1))"));
    [Fact] public void Polyfit() => Assert.Equal(2, Num("p=polyfit([1 2 3],[2 4 6],1); r=round(p(1))"));
    [Fact] public void Roots() => Assert.Equal(5, Num("r=sum(roots([1 -5 6]))"), 8);
    [Fact] public void Isequal() => Assert.Equal(1, Num("r=isequal([1 2],[1 2])"));
    [Fact] public void Eval() => Assert.Equal(7, Num("eval('r=7;')"));
    [Fact] public void Feval() => Assert.Equal(3, Num("r=feval('max',[1 3 2])"));
    [Fact] public void Nested() => Assert.Equal(120, Num("r=prod(1:5)"));
    [Fact] public void CommandLineFunction() =>
        Assert.Equal(9, Num("function y=sq(x)\n y=x*x;\nend\nr=sq(3);"));

    [Fact]
    public void UserFunctionFileOnPath()
    {
        var dir = Directory.CreateTempSubdirectory("mrw").FullName;
        File.WriteAllText(Path.Combine(dir, "fib.m"),
            "function r = fib(n)\n if n < 2\n r = n;\n else\n r = fib(n-1)+fib(n-2);\n end\nend\n");
        var (it, _) = New();
        it.SearchPath.Add(dir);
        it.Eval("r = fib(10);");
        Assert.Equal("55", Run1(it, "r"));
    }

    [Fact]
    public void UndefinedRaises()
    {
        var (it, _) = New();
        Assert.ThrowsAny<Exception>(() => it.Eval("nope_xyz(1)"));
    }

    [Fact]
    public void DimensionMismatch()
    {
        var (it, _) = New();
        var ex = Assert.ThrowsAny<Exception>(() => it.Eval("[1 2; 3]"));
        Assert.Contains("vertical dimensions mismatch", ex.Message);
    }

    [Fact] public void CellDisplay() =>
        Assert.Equal("c =\n{\n  [1,1] = 1\n  [1,2] = ab\n}\n\n", Run("c = {1, 'ab'}"));
}
