// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Runtime;

/// <summary>[a, b] and [a; b] concatenation with Octave type rules.</summary>
public static class Concat
{
    public static Value Horz(List<Value> items) => Join(items, true);
    public static Value Vert(List<Value> items) => Join(items, false);

    static Value Join(List<Value> items, bool horiz)
    {
        if (items.Count == 1) return items[0];
        if (items.Any(v => v is FunctionHandleValue))
            throw new OctaveException("concatenation of function handles is not allowed; use a cell array");
        bool anyCell = items.Any(v => v is CellValue);
        bool anyStruct = items.Any(v => v is StructValue);
        var arrs = items.Cast<ArrayValue>().ToList();
        if (anyCell)
        {
            // [c, x]: non-cell items are wrapped (Octave behaviour for [c1, c2])
            arrs = arrs.Select(a => a is CellValue ? a : (a.Numel == 0 ? new CellValue(0, 0) : new CellValue(1, 1, new Value[] { a }))).ToList();
        }
        else if (anyStruct && arrs.Any(a => a is not StructValue && a.Numel > 0))
            throw new OctaveException("concatenation operator not implemented for 'struct' by 'matrix' operations");

        NumKind kind = NumKind.Double; bool cplx = false;
        if (!anyCell && !anyStruct)
        {
            var ms = arrs.Cast<MatrixValue>().ToList();
            bool anyChar = ms.Any(m => m.IsChar);
            bool allLogical = ms.Where(m => m.Numel > 0 || ms.All(x => x.Numel == 0)).All(m => m.IsLogical);
            kind = anyChar ? NumKind.Char : allLogical ? NumKind.Logical : NumKind.Double;
            cplx = ms.Any(m => m.IsComplex);
        }

        var nonEmpty = arrs.Where(a => !(a.R == 0 && a.C == 0) && !(a.Numel == 0 && (horiz ? a.R != FirstR(arrs) : a.C != FirstC(arrs)))).ToList();
        if (nonEmpty.Count == 0)
        {
            var e0 = arrs[0];
            if (anyCell) return new CellValue(0, 0);
            if (e0 is MatrixValue m0) return new MatrixValue(horiz ? e0.R : arrs.Sum(a => a.R), horiz ? arrs.Sum(a => a.C) : e0.C, kind);
            return e0;
        }
        int R, C;
        if (horiz)
        {
            R = nonEmpty[0].R;
            foreach (var a in nonEmpty)
                if (a.R != R) throw new OctaveException($"horizontal dimensions mismatch ({nonEmpty[0].R}x{nonEmpty[0].C} vs {a.R}x{a.C})");
            C = nonEmpty.Sum(a => a.C);
        }
        else
        {
            C = nonEmpty[0].C;
            foreach (var a in nonEmpty)
                if (a.C != C) throw new OctaveException($"vertical dimensions mismatch ({nonEmpty[0].R}x{nonEmpty[0].C} vs {a.R}x{a.C})");
            R = nonEmpty.Sum(a => a.R);
        }
        ArrayValue target = anyCell ? new CellValue(R, C)
            : anyStruct ? new StructValue(R, C, ((StructValue)nonEmpty[0]).Fields)
            : new MatrixValue(R, C, kind, cplx);
        int off = 0;
        foreach (var a in nonEmpty)
        {
            for (int j = 0; j < a.C; j++)
                for (int i = 0; i < a.R; i++)
                {
                    int dst = horiz ? i + (j + off) * R : (i + off) + j * R;
                    target.SetFrom(dst, a, i + j * a.R);
                }
            off += horiz ? a.C : a.R;
        }
        if (target is MatrixValue tm) { tm.Kind = kind; tm.Narrow(); }
        return target;
    }

    static int FirstR(List<ArrayValue> a) => a.FirstOrDefault(x => x.Numel > 0)?.R ?? 0;
    static int FirstC(List<ArrayValue> a) => a.FirstOrDefault(x => x.Numel > 0)?.C ?? 0;
}
