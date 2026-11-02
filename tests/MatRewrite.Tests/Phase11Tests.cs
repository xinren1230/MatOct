using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase11Tests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    [Fact] public void VersionCurrent() => Assert.Equal("0.18.0", Run("printf('%s', version())"));
    [Fact] public void SchurReal() => Assert.Equal("1", Run("A=[1 2;3 4]; [u,s]=schur(A); printf('%d', norm(u*s*u'-A)<1e-10 && abs(s(2,1))<1e-12)"));
    [Fact] public void Qz() => Assert.Equal("1", Run("A=[1 2;3 4]; B=[2 0;1 1]; [AA,BB,Q,Z]=qz(A,B); printf('%d', norm(Q*A*Z-AA)<1e-10 && norm(Q*B*Z-BB)<1e-10)"));
    [Fact] public void SumAll() => Assert.Equal("10", Run("printf('%d', sum([1 2;3 4],'all'))"));
    [Fact] public void SumVecdim() => Assert.Equal("1 1 2", Run("printf('%d ', size(sum(ones(2,3,2),[1 2])))").Trim());
    [Fact] public void Integral() => Assert.Equal("0.333333", Run("printf('%.6f', integral(@(x) x.^2, 0, 1))"));
    [Fact] public void Quadgk() => Assert.Equal("2.000000", Run("printf('%.6f', quadgk(@sin, 0, pi))"));
    [Fact] public void Integral2() => Assert.Equal("1.000000", Run("printf('%.6f', integral2(@(x,y) x.*y*4, 0, 1, 0, 1))"));
    [Fact] public void RngRepeat() => Assert.Equal("1", Run("rng(3); a=rand(1,3); rng(3); b=rand(1,3); printf('%d', isequal(a,b))"));
    [Fact] public void Delaunay() => Assert.Equal("2 3", Run("t=delaunay([0 1 0 1],[0 0 1 1]); printf('%d ', size(t))").Trim());
    [Fact] public void ConvhullArea() => Assert.Equal("1", Run("[h,a]=convhull([0 1 1 0 .5],[0 0 1 1 .5]); printf('%g', a)"));
    [Fact] public void Inpolygon() => Assert.Equal("1 0", Run("printf('%d ', inpolygon([.5 2],[.5 2],[0 1 1 0],[0 0 1 1]))").Trim());
    [Fact] public void GriddataLinear() => Assert.Equal("1.5", Run("[x,y]=meshgrid(0:2); z=x+y; printf('%g', griddata(x(:),y(:),z(:),0.5,1))"));
    [Fact] public void DiffDim() => Assert.Equal("1 3", Run("printf('%d ', size(diff([1 2 3;4 5 6],1,1)))").Trim());
    [Fact] public void NullSpace() => Assert.Equal("1", Run("n=null([1 1]); printf('%d', abs(n'*[1;1])<1e-12 && numel(n)==2)"));
    [Fact] public void EyeClass() => Assert.Equal("single", Run("printf('%s', class(eye(2,'single')))"));
    [Fact] public void SvdSingle() => Assert.Equal("single", Run("printf('%s', class(svd(single([1 2;3 4]))))"));
    [Fact] public void AnonInCell() => Assert.Equal("9", Run("c={@(x) x.^2, 3}; printf('%d', c{1}(c{2}))"));
    [Fact] public void Rotz() => Assert.Equal("0", Run("printf('%g', round(rotz(90)(1,1)*1e12)/1e12)"));
}
