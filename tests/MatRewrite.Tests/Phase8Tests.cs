using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase8Tests
{
    static string Run(string code)
    {
        var o = new StringWriter();
        new Interpreter(o).Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    // ---- interpolation
    [Fact] public void Interp1Linear() => Assert.Equal("1.5", Run("printf('%g', interp1([1 2], [1 2], 1.5))"));
    [Fact] public void Interp1OutsideIsNaN() => Assert.Equal("1", Run("printf('%g', isnan(interp1([1 2], [1 2], 3)))"));
    [Fact] public void Interp1Extrap() => Assert.Equal("3", Run("printf('%g', interp1([1 2], [1 2], 3, 'linear', 'extrap'))"));
    [Fact] public void Interp1Spline() => Assert.Equal("9", Run("printf('%.6g', interp1(1:4, (1:4).^2, 3, 'spline'))"));
    [Fact] public void Interp1Nearest() => Assert.Equal("20", Run("printf('%g', interp1([1 2 3], [10 20 30], 2.4, 'nearest'))"));
    [Fact] public void Interp2Linear() => Assert.Equal("2.5", Run("[x, y] = meshgrid(1:2); z = x + y; printf('%g', interp2(z, 1.5, 1))"));
    [Fact] public void Interp2ComplexExtrap() => Assert.Equal("0 1", Run("v = interp2([1 2; 3 4], 5, 5, 'linear', 1i); printf('%g %g', real(v), imag(v))"));

    // ---- gallery / orth
    [Fact] public void GalleryMinij() => Assert.Equal("1 1 1 2", Run("printf('%d ', gallery('minij', 2))"));
    [Fact] public void GalleryTridiagIsSparse() => Assert.Equal("1", Run("printf('%d', issparse(gallery('tridiag', 4)))"));
    [Fact] public void GalleryUnknown() => Assert.Contains("unknown matrix", Assert.Throws<OctaveException>(() => Run("gallery('foobar')")).Message);
    [Fact] public void OrthIsOrthonormal() => Assert.Equal("1", Run("q = orth([1 1; 1 1; 0 0]); printf('%d', abs(norm(q) - 1) < 1e-12 && columns(q) == 1)"));

    // ---- ode helpers
    [Fact] public void OdesetPrefixAmbiguous() => Assert.Contains("matches more than one", Assert.Throws<OctaveException>(() => Run("odeset('Re', 1)")).Message);
    [Fact] public void Decic() => Assert.Equal("-0.04 0.04", Run(
        "f = @(t, y, yp) [-(yp(1) + 0.04*y(1) - 1e4*y(2)*y(3)); -(yp(2) - 0.04*y(1) + 1e4*y(2)*y(3) + 3e7*y(2)^2); y(1) + y(2) + y(3) - 1];" +
        "[y, yp] = decic(f, 0, [1;0;0], [1;1;0], [23;110;0], [0;0;1]); printf('%.6g %.6g', yp(1), yp(2))"));
    [Fact] public void NthargoutCell() => Assert.Equal("3 4", Run("c = nthargout(1:2, @size, ones(3, 4)); printf('%d %d', c{1}, c{2})"));
    [Fact] public void FormatQuery() => Assert.Equal("short", Run("format short; f = format(); printf('%s', f)"));

    // ---- polynomials / linalg / special / time
    [Fact] public void Polyfit() => Assert.Equal("2 1", Run("printf('%.6g ', polyfit([0 1 2], [1 3 5], 1))"));
    [Fact] public void PpvalDescending() => Assert.Equal("2", Run("pp = mkpp([0 1], [1 1]); printf('%g', ppval(pp, 1))"));
    [Fact] public void PchipComplex() => Assert.Equal("1", Run("v = pchip(1:3, [1 2 3] + 1i*[1 2 3], 2); printf('%d', v == 2 + 2i)"));
    [Fact] public void QrEconomy() => Assert.Equal("3 2", Run("[q, r] = qr(rand(3, 2), 0); printf('%d %d', size(q))"));
    [Fact] public void GammaincEdge() => Assert.Equal("1", Run("printf('%g', gammainc(Inf, 2))"));
    [Fact] public void EpsSingle() => Assert.Equal("single", Run("printf('%s', class(eps('single')))"));
    [Fact] public void Magic4() => Assert.Equal("34", Run("printf('%d', sum(magic(4)(:, 1)))"));
    [Fact] public void Datenum() => Assert.Equal("738887", Run("printf('%d', datenum(2023, 1, 1))"));
    [Fact] public void Datestr() => Assert.Equal("01-Jan-2023", Run("printf('%s', datestr(738887, 1))"));
    [Fact] public void Weekday() => Assert.Equal("1", Run("printf('%d', weekday(738887))"));
    [Fact] public void LeapYear() => Assert.Equal("1 0", Run("printf('%d ', is_leap_year([2024 2023]))"));
}
