// MatRewrite - a C# reimplementation of the GNU Octave core.
// Copyright (C) 2026 Xinren Chen. Licensed under GPL-3.0-or-later.
using System.Numerics;
using MatRewrite.Core.Parsing;

namespace MatRewrite.Core.Runtime;

/// <summary>Error raised by Octave code (error(), runtime failures).</summary>
public class OctaveException : Exception
{
    public string Identifier { get; }
    public OctaveException(string message, string identifier = "") : base(message) { Identifier = identifier; }
}

public abstract class Value
{
    public abstract int Rows { get; }
    public abstract int Cols { get; }
    public int Numel => Rows * Cols;
    public bool IsEmpty => Numel == 0;
    public abstract string ClassName { get; }
    public string DimString => $"{Rows}x{Cols}";
}

/// <summary>The magic colon used inside an index expression, e.g. A(:, 1).</summary>
public sealed class MagicColon : Value
{
    public static readonly MagicColon Instance = new();
    public override int Rows => 1;
    public override int Cols => 1;
    public override string ClassName => "magic-colon";
}

/// <summary>Column-major 2-D array base class (matrix, cell, struct array).</summary>
public abstract class ArrayValue : Value
{
    public int R, C;
    public override int Rows => R;
    public override int Cols => C;
    /// <summary>Build a new array of the same type from elements at linear indices.</summary>
    public abstract ArrayValue Gather(int[] lin, int r, int c);
    /// <summary>Copy with new size; existing elements kept at their (row,col).</summary>
    public abstract ArrayValue Resized(int r, int c);
    /// <summary>Set element dst from src[srcIdx] (src has a compatible type).</summary>
    public abstract void SetFrom(int dst, ArrayValue src, int srcIdx);
    public abstract ArrayValue EmptyLike(int r, int c);
}

public enum NumKind { Double, Logical, Char }

public sealed class MatrixValue : ArrayValue
{
    public double[] Re;
    public double[]? Im;
    public NumKind Kind;
    public bool DoubleQuoted; // display only

    public MatrixValue(int r, int c, NumKind kind = NumKind.Double, bool complex = false)
    {
        R = r; C = c; Kind = kind; Re = new double[r * c];
        if (complex) Im = new double[r * c];
    }
    public MatrixValue(int r, int c, double[] re, double[]? im = null, NumKind kind = NumKind.Double)
    { R = r; C = c; Re = re; Im = im; Kind = kind; }

    public bool IsComplex => Im != null;
    public bool IsChar => Kind == NumKind.Char;
    public bool IsLogical => Kind == NumKind.Logical;
    public bool IsScalar => R == 1 && C == 1;
    public bool IsVector => (R == 1 || C == 1) && R * C >= 1;

    public override string ClassName => Kind switch { NumKind.Logical => "logical", NumKind.Char => "char", _ => "double" };

    public static MatrixValue Scalar(double d) => new(1, 1, new[] { d });
    public static MatrixValue Cplx(double re, double im) => new MatrixValue(1, 1, new[] { re }, new[] { im }).Narrow();
    public static MatrixValue Cplx(Complex z) => Cplx(z.Real, z.Imaginary);
    public static MatrixValue Bool(bool b) => new(1, 1, new[] { b ? 1.0 : 0.0 }, null, NumKind.Logical);
    public static MatrixValue Empty() => new(0, 0);
    public static MatrixValue Str(string s, bool dq = false)
    {
        var m = new MatrixValue(s.Length == 0 ? 0 : 1, s.Length, NumKind.Char) { DoubleQuoted = dq };
        for (int i = 0; i < s.Length; i++) m.Re[i] = s[i];
        return m;
    }
    public static MatrixValue Row(IEnumerable<double> xs) { var a = xs.ToArray(); return new MatrixValue(1, a.Length, a); }
    public static MatrixValue Col(IEnumerable<double> xs) { var a = xs.ToArray(); return new MatrixValue(a.Length, 1, a); }

    public Complex At(int k) => new(Re[k], Im == null ? 0 : Im[k]);
    public double this[int k] => Re[k];
    public double this[int i, int j] => Re[i + j * R];

    public string AsString()
    {
        if (R <= 1) return new string(Re.Select(d => (char)(int)d).ToArray());
        // row-major concatenation of rows
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < R; i++) for (int j = 0; j < C; j++) sb.Append((char)(int)Re[i + j * R]);
        return sb.ToString();
    }
    public string RowString(int i)
    {
        var ch = new char[C];
        for (int j = 0; j < C; j++) ch[j] = (char)(int)Re[i + j * R];
        return new string(ch);
    }

    public double ScalarDouble(string ctx = "value")
    {
        if (Numel < 1) throw new OctaveException($"{ctx}: expected a scalar value but got an empty matrix");
        return Re[0];
    }

    /// <summary>Drop the imaginary part if it is all zero.</summary>
    public MatrixValue Narrow()
    {
        if (Im != null && Im.All(v => v == 0)) Im = null;
        return this;
    }

    public MatrixValue AsDouble()
    {
        if (Kind == NumKind.Double) return this;
        return new MatrixValue(R, C, Re, Im, NumKind.Double);
    }

    public MatrixValue Copy() => new(R, C, (double[])Re.Clone(), (double[]?)Im?.Clone(), Kind) { DoubleQuoted = DoubleQuoted };

    public override ArrayValue Gather(int[] lin, int r, int c)
    {
        var m = new MatrixValue(r, c, Kind, Im != null) { DoubleQuoted = DoubleQuoted };
        for (int k = 0; k < lin.Length; k++)
        {
            m.Re[k] = Re[lin[k]];
            if (Im != null) m.Im![k] = Im[lin[k]];
        }
        return m;
    }
    public override ArrayValue Resized(int r, int c)
    {
        var m = new MatrixValue(r, c, Kind, Im != null) { DoubleQuoted = DoubleQuoted };
        for (int j = 0; j < Math.Min(c, C); j++)
            for (int i = 0; i < Math.Min(r, R); i++)
            {
                m.Re[i + j * r] = Re[i + j * R];
                if (Im != null) m.Im![i + j * r] = Im[i + j * R];
            }
        return m;
    }
    public override void SetFrom(int dst, ArrayValue src, int srcIdx)
    {
        var s = (MatrixValue)src;
        Re[dst] = s.Re[srcIdx];
        if (s.Im != null) { Im ??= new double[Re.Length]; Im[dst] = s.Im[srcIdx]; }
        else if (Im != null) Im[dst] = 0;
    }
    public override ArrayValue EmptyLike(int r, int c) => new MatrixValue(r, c, Kind, false);
}

public sealed class CellValue : ArrayValue
{
    public Value[] Data;
    public CellValue(int r, int c)
    {
        R = r; C = c; Data = new Value[r * c];
        for (int i = 0; i < Data.Length; i++) Data[i] = MatrixValue.Empty();
    }
    public CellValue(int r, int c, Value[] data) { R = r; C = c; Data = data; }
    public static CellValue RowOf(IEnumerable<Value> vs) { var a = vs.ToArray(); return new CellValue(1, a.Length, a); }
    public static CellValue ColOf(IEnumerable<Value> vs) { var a = vs.ToArray(); return new CellValue(a.Length, 1, a); }
    public override string ClassName => "cell";
    public override ArrayValue Gather(int[] lin, int r, int c) => new CellValue(r, c, lin.Select(k => Data[k]).ToArray());
    public override ArrayValue Resized(int r, int c)
    {
        var m = new CellValue(r, c);
        for (int j = 0; j < Math.Min(c, C); j++)
            for (int i = 0; i < Math.Min(r, R); i++) m.Data[i + j * r] = Data[i + j * R];
        return m;
    }
    public override void SetFrom(int dst, ArrayValue src, int srcIdx) => Data[dst] = ((CellValue)src).Data[srcIdx];
    public override ArrayValue EmptyLike(int r, int c) => new CellValue(r, c);
    public CellValue Copy() => new(R, C, (Value[])Data.Clone());
}

/// <summary>Struct array: every element has the same ordered field list.</summary>
public sealed class StructValue : ArrayValue
{
    public List<string> Fields = new();
    public Dictionary<string, Value>[] Elems;
    public StructValue() { R = 1; C = 1; Elems = new[] { new Dictionary<string, Value>() }; }
    public StructValue(int r, int c, List<string> fields)
    {
        R = r; C = c; Fields = new List<string>(fields);
        Elems = new Dictionary<string, Value>[r * c];
        for (int k = 0; k < Elems.Length; k++) Elems[k] = NewElem();
    }
    Dictionary<string, Value> NewElem()
    {
        var d = new Dictionary<string, Value>();
        foreach (var f in Fields) d[f] = MatrixValue.Empty();
        return d;
    }
    public override string ClassName => "struct";
    public Value Get(string f, int k = 0)
    {
        if (!Elems[k].TryGetValue(f, out var v)) throw new OctaveException($"invalid use of undefined value");
        return v;
    }
    public bool HasField(string f) => Fields.Contains(f);
    public void AddField(string f)
    {
        if (Fields.Contains(f)) return;
        Fields.Add(f);
        foreach (var e in Elems) e[f] = MatrixValue.Empty();
    }
    public void Set(string f, Value v, int k = 0) { AddField(f); Elems[k][f] = v; }
    public StructValue Copy()
    {
        var s = new StructValue(R, C, Fields);
        for (int k = 0; k < Elems.Length; k++) s.Elems[k] = new Dictionary<string, Value>(Elems[k]);
        return s;
    }
    public override ArrayValue Gather(int[] lin, int r, int c)
    {
        var s = new StructValue(r, c, Fields);
        for (int k = 0; k < lin.Length; k++) s.Elems[k] = new Dictionary<string, Value>(Elems[lin[k]]);
        return s;
    }
    public override ArrayValue Resized(int r, int c)
    {
        var s = new StructValue(r, c, Fields);
        for (int j = 0; j < Math.Min(c, C); j++)
            for (int i = 0; i < Math.Min(r, R); i++) s.Elems[i + j * r] = Elems[i + j * R];
        return s;
    }
    public override void SetFrom(int dst, ArrayValue src, int srcIdx)
    {
        var s = (StructValue)src;
        foreach (var f in s.Fields) AddField(f);
        var e = new Dictionary<string, Value>();
        foreach (var f in Fields) e[f] = s.Elems[srcIdx].TryGetValue(f, out var v) ? v : MatrixValue.Empty();
        Elems[dst] = e;
    }
    public override ArrayValue EmptyLike(int r, int c) => new StructValue(r, c, Fields);
}

public sealed class FunctionHandleValue : Value
{
    public string? Name;                       // @name
    public List<string>? Params;               // @(params) body
    public Expr? Body;
    public Dictionary<string, Value>? Captured;
    public UserFunction? Context;              // file context for subfunction lookup
    public override int Rows => 1;
    public override int Cols => 1;
    public override string ClassName => "function_handle";
    public bool IsAnonymous => Body != null;
    public override string ToString() =>
        IsAnonymous ? "@(" + string.Join(", ", Params!) + ") " + Unparser.Print(Body!) : "@" + Name;
}
