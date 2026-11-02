using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase9Tests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }
    static string Err(string code) => Assert.Throws<OctaveException>(() => Run(code)).Message;

    // ---- linear algebra
    [Fact] public void Sqrtm() => Assert.Equal("1", Run("printf('%d', norm(sqrtm([4 0;0 9]) - [2 0;0 3]) < 1e-12)"));
    [Fact] public void Logm() => Assert.Equal("1", Run("printf('%d', norm(logm(expm([1 2;0 3])) - [1 2;0 3]) < 1e-10)"));
    [Fact] public void Cross() => Assert.Equal("0 0 1", Run("printf('%g ', cross([1 0 0],[0 1 0])); "));
    [Fact] public void CondInfNorm() => Assert.Equal("15", Run("printf('%g', norm([7 2 3;1 3 4;6 4 5], Inf))"));
    [Fact] public void CondNaN() => Assert.Contains("must not contain Inf or NaN", Err("cond([1 2; NaN 4])"));
    [Fact] public void TraceVector() => Assert.Equal("3", Run("printf('%g', trace(3:10))"));
    [Fact] public void TraceND() => Assert.Contains("only valid on 2-D objects", Err("trace(ones(1,3,3))"));
    [Fact] public void Rref() => Assert.Equal("1 0 0 1", Run("printf('%g ', rref([2 4; 1 3]))"));

    // ---- colormaps / image
    [Fact] public void ViridisSize() => Assert.Equal("64 3", Run("printf('%d ', size(viridis(64)))"));
    [Fact] public void Hsv2RgbRed() => Assert.Equal("1 0 0", Run("printf('%g ', hsv2rgb([0 1 1]))"));
    [Fact] public void Rgb2Gray() => Assert.Equal("0.2989", Run("printf('%.4f', rgb2gray([1 0 0]))"));

    // ---- graphics property validation
    [Fact] public void RadioProp() => Assert.Contains("invalid value for radio property \"xscale\" (value = foo)", Err("figure('visible','off'); set(gca,'xscale','foo')"));
    [Fact] public void DoubleProp() => Assert.Contains("invalid value for double property \"linewidth\"", Err("figure('visible','off'); h=line(1:2,1:2); set(h,'linewidth','x')"));
    [Fact] public void ColorProp() => Assert.Contains("invalid value for color property \"color\"", Err("figure('visible','off'); h=line(1:2,1:2); set(h,'color','purple')"));
    [Fact] public void ReadOnlyType() => Assert.Contains("\"type\" is read-only", Err("figure('visible','off'); h=line(1:2,1:2); set(h,'type','x')"));
    [Fact] public void AxesSelectsCurrent() => Assert.Equal("1", Run("figure('visible','off'); a=axes(); b=axes(); axes(a); printf('%d', gca==a)"));
    [Fact] public void SphereSize() => Assert.Equal("4 4", Run("[x,y,z]=sphere(3); printf('%d ', size(z))"));
    [Fact] public void SphereBadN() => Assert.Contains("N must be a real scalar > 0", Err("sphere(-1)"));
    [Fact] public void StairsOutputs() => Assert.Equal("1 2 2 3 3", Run("[x,y]=stairs([1 2 3]); printf('%g ', x)"));

    // ---- misc
    [Fact] public void FftShift() => Assert.Equal("4 5 1 2 3", Run("printf('%g ', fftshift(1:5))"));
    [Fact] public void IfftShift() => Assert.Equal("3 4 5 1 2", Run("printf('%g ', ifftshift(1:5))"));
    [Fact] public void Hanning() => Assert.Equal("0 1 0", Run("printf('%g ', hanning(3))"));
    [Fact] public void Beta() => Assert.Equal("0.0833333", Run("printf('%g', beta(2,3))"));
    [Fact] public void Bincoeff() => Assert.Equal("10", Run("printf('%g', bincoeff(5,2))"));
    [Fact] public void Base2Dec() => Assert.Equal("255", Run("printf('%g', base2dec('ff',16))"));
    [Fact] public void Fzero() => Assert.Equal("1.5708", Run("printf('%.4f', fzero(@cos,[0 3]))"));
    [Fact] public void Fminbnd() => Assert.Equal("1.0000", Run("printf('%.4f', fminbnd(@(x)(x-1).^2,0,3))"));
    [Fact] public void Inpolygon() => Assert.Equal("1 0", Run("printf('%d ', inpolygon([0.5 2],[0.5 2],[0 1 1 0],[0 0 1 1]))"));
    [Fact] public void Rectint() => Assert.Equal("1", Run("printf('%g', rectint([0 0 2 2],[1 1 2 2]))"));
    [Fact] public void Strtrunc() => Assert.Equal("abc", Run("printf('%s', strtrunc('abcdef',3))"));
}
