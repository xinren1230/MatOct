// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterCellStruct()
    {
        Reg1("struct", a =>
        {
            if (a.Length == 1 && a[0] is ObjectValue sob) return ObjectToStruct(sob);
            if (a.Length == 0) return new StructValue();
            if (a.Length == 1 && a[0] is StructValue sv1) return sv1;
            if (a.Length == 1 && a[0] is MatrixValue em1 && em1.Numel == 0 && !em1.IsChar) return new StructValue(em1.R, em1.C, new List<string>());
            if (a.Length % 2 != 0) throw new OctaveException("struct: additional arguments must occur as \"field\", VALUE pairs");
            // cell values create struct arrays (non-1x1 cells); 1x1 cells are unwrapped
            int R = 1, C = 1;
            for (int k = 1; k < a.Length; k += 2)
                if (a[k] is CellValue c && c.Numel != 1) { R = c.R; C = c.C; break; }
            var fields = new List<string>();
            for (int k = 0; k < a.Length; k += 2) fields.Add(S(a[k], "struct"));
            var s = new StructValue(R, C, fields);
            for (int k = 0; k < a.Length; k += 2)
            {
                var f = fields[k / 2]; var v = a[k + 1];
                for (int e = 0; e < R * C; e++)
                {
                    if (v is CellValue c)
                    {
                        if (c.Numel == 1) s.Elems[e][f] = c.Data[0];
                        else if (c.R == R && c.C == C) s.Elems[e][f] = c.Data[e];
                        else throw new OctaveException("struct: dimensions of parameter 2 do not match those of parameter 4");
                    }
                    else s.Elems[e][f] = v;
                }
            }
            return s;
        });
        Reg1("fieldnames", a =>
        {
            if (a[0] is ObjectValue fob) { var pn = Interpreter.ObjectProps(fob); return new CellValue(pn.Count, 1, pn.Select(x => (Value)MatrixValue.Str(x)).ToArray()); }
            if (a[0] is not StructValue s) throw new OctaveException("fieldnames: Invalid input argument");
            return CellValue.ColOf(s.Fields.Select(Str));
        });
        Reg1("numfields", a => Num(a[0] is StructValue s ? s.Fields.Count : 0));
        Reg1("isfield", a =>
        {
            if (a[0] is not StructValue s) return a[1] is CellValue cc ? new MatrixValue(cc.R, cc.C, NumKind.Logical) : Bool(false);
            if (a[1] is CellValue c) { var r = new MatrixValue(c.R, c.C, NumKind.Logical); for (int k = 0; k < c.Numel; k++) r.Re[k] = c.Data[k] is MatrixValue m && m.IsChar && s.HasField(m.AsString()) ? 1 : 0; return r; }
            return Bool(IsString(a[1]) && s.HasField(S(a[1], "isfield")));
        });
        Reg1("rmfield", a =>
        {
            var s = ((a[0] as StructValue) ?? throw new OctaveException("rmfield: first argument must be a struct")).Copy();
            var names = a[1] is CellValue c ? c.Data.Select(v => S(v, "rmfield")).ToList() : new List<string> { S(a[1], "rmfield") };
            foreach (var n in names)
            {
                if (!s.HasField(n)) throw new OctaveException($"rmfield: structure does not contain remove field {n}");
                s.Fields.Remove(n); foreach (var e in s.Elems) e.Remove(n);
            }
            return s;
        });
        Reg1("setfield", a => { var s = ((StructValue)a[0]).Copy(); s.Set(S(a[1], "setfield"), a[2]); return s; });
        Reg1("getfield", a =>
        {
            var s = (StructValue)a[0]; var f = S(a[1], "getfield");
            if (!s.HasField(f)) throw new OctaveException($"invalid use of undefined value");
            return s.Get(f);
        });
        Reg1("orderfields", a =>
        {
            var s = ((StructValue)a[0]).Copy(); s.Fields = s.Fields.OrderBy(f => f, StringComparer.Ordinal).ToList(); return s;
        });
        Reg1("struct2cell", a =>
        {
            var s = (StructValue)a[0];
            if (s.Numel != 1)
            {
                int nf = s.Fields.Count, ne = s.Numel; var sd = s.Dims;
                var c = new CellValue(nf, ne);
                for (int e = 0; e < ne; e++) for (int f = 0; f < nf; f++) c.Data[f + e * nf] = s.Elems[e].TryGetValue(s.Fields[f], out var fv) ? fv : new MatrixValue(0, 0);
                var nd = new[] { nf }.Concat(sd).ToArray(); int L = nd.Length; while (L > 2 && nd[L - 1] == 1) L--;
                c.SetDims(nd.Take(L).ToArray());
                return c;
            }
            return CellValue.ColOf(s.Fields.Select(f => s.Elems[0][f]));
        });
        Reg1("cell2struct", a =>
        {
            var c = (CellValue)a[0]; var f = StrList(a[1], "cell2struct");
            int dim = a.Length > 2 ? I(a[2], "cell2struct") : 1;
            int n = dim == 1 ? c.C : c.R;
            var s = new StructValue(n, 1, f);
            for (int e = 0; e < n; e++)
                for (int k = 0; k < f.Count; k++) s.Elems[e][f[k]] = dim == 1 ? c.Data[k + e * c.R] : c.Data[e + k * c.R];
            return s;
        });
        Reg1("num2cell", a =>
        {
            var x = A(a[0], "num2cell");
            if (a.Length > 1)
            {
                var dm = M(a[1], "num2cell");
                var dl = dm.Re.Select(v => (int)v).ToArray();
                if (dl.Any(v => v < 1)) throw new OctaveException("num2cell: DIMS must be a valid array of integers between 1 and N");
                int nd = Math.Max(x.Dims.Length, dl.Length == 0 ? 2 : dl.Max());
                var sz = new int[nd]; for (int k = 0; k < nd; k++) sz[k] = k < x.Dims.Length ? x.Dims[k] : 1;
                var osz = (int[])sz.Clone(); var ssz = new int[nd];
                for (int k = 0; k < nd; k++) ssz[k] = 1;
                foreach (var d in dl) { osz[d - 1] = 1; ssz[d - 1] = sz[d - 1]; }
                int on = osz.Aggregate(1, (p2, q) => p2 * q), sn = ssz.Aggregate(1, (p2, q) => p2 * q);
                var str = new int[nd]; str[0] = 1; for (int k = 1; k < nd; k++) str[k] = str[k - 1] * sz[k - 1];
                var rc = new CellValue(osz[0], on / Math.Max(1, osz[0]));
                if (osz[0] == 0) rc = new CellValue(0, osz.Skip(1).Aggregate(1, (p2, q) => p2 * q));
                var oc = new int[nd];
                for (int o = 0; o < on; o++)
                {
                    var lin = new int[sn]; var sc = new int[nd];
                    for (int e = 0; e < sn; e++)
                    {
                        int li = 0; for (int k = 0; k < nd; k++) li += (oc[k] + sc[k]) * str[k];
                        lin[e] = li;
                        for (int k = 0; k < nd; k++) { if (++sc[k] < ssz[k]) break; sc[k] = 0; }
                    }
                    var g = x.Gather(lin, ssz[0], sn / Math.Max(1, ssz[0]));
                    if (nd > 2) g = NDArray.Reshape(g, ssz);
                    rc.Data[o] = g;
                    for (int k = 0; k < nd; k++) { if (++oc[k] < osz[k]) break; oc[k] = 0; }
                }
                if (nd > 2) rc.SetDims(osz);
                return rc;
            }
            var r = new CellValue(x.R, x.C);
            for (int k = 0; k < x.Numel; k++) r.Data[k] = x.Gather(new[] { k }, 1, 1);
            return r;
        });
        Reg1("cell2mat", a =>
        {
            var c = a[0] as CellValue ?? throw new OctaveException("cell2mat: C must be a cell array");
            if (c.Numel == 0) return new MatrixValue(0, 0);
            var rows = new List<Value>();
            for (int i = 0; i < c.R; i++) rows.Add(Concat.Horz(Enumerable.Range(0, c.C).Select(j => c.Data[i + j * c.R]).ToList()));
            return Concat.Vert(rows);
        });
        Reg("cellfun", (it, a, n) => CellFun(it, a, n, true));
        Reg("arrayfun", (it, a, n) => CellFun(it, a, n, false));
        Reg("structfun", (it, a, n) =>
        {
            var s = (StructValue)a[1];
            var cell = CellValue.ColOf(s.Fields.Select(f => s.Elems[0][f]));
            var args = new List<Value> { a[0], cell }; args.AddRange(a.Skip(2));
            return CellFun(it, args.ToArray(), n, true);
        });
        Reg("deal", (it, a, n) =>
        {
            int no = Math.Max(n, 1);
            if (a.Length == 1) return Enumerable.Repeat(a[0], no).ToArray();
            if (a.Length != no) throw new OctaveException("deal: nargin > 1 and nargin != nargout");
            return a;
        });
        Reg1("iscellstr", a => Bool(a[0] is CellValue c && c.Data.All(v => v is MatrixValue m && m.IsChar)));
    }

    static Value[] CellFun(Interpreter it, Value[] a, int nargout, bool isCell)
    {
        string fn = isCell ? "cellfun" : "arrayfun";
        if (a.Length < 2) throw new OctaveException($"Invalid call to {fn}");
        bool uniform = true; Value? errHandler = null;
        var inputs = new List<Value>();
        for (int k = 1; k < a.Length; k++)
        {
            if (IsString(a[k]) && k + 1 < a.Length && (S(a[k], fn).Equals("UniformOutput", StringComparison.OrdinalIgnoreCase) || S(a[k], fn).Equals("ErrorHandler", StringComparison.OrdinalIgnoreCase)))
            {
                if (S(a[k], fn).Equals("UniformOutput", StringComparison.OrdinalIgnoreCase)) uniform = Ops.IsTrue(a[k + 1]); else errHandler = a[k + 1];
                k++; continue;
            }
            inputs.Add(a[k]);
        }
        Value func = a[0];
        if (isCell && IsString(func))
        {
            string name = S(func, fn);
            var c0 = inputs[0] as CellValue ?? throw new OctaveException("cellfun: C must be a cell array");
            Func<Value, double>? quick = name switch
            {
                "isempty" => v => v.Numel == 0 ? 1 : 0,
                "length" => v => v.Numel == 0 ? 0 : Math.Max(v.Rows, v.Cols),
                "numel" or "prodofsize" => v => v.Numel,
                "ndims" => v => 2,
                "isreal" => v => v is MatrixValue m && !m.IsComplex ? 1 : 0,
                "islogical" => v => v is MatrixValue m && m.IsLogical ? 1 : 0,
                _ => null
            };
            if (name == "isclass" && inputs.Count == 2)
            {
                string cls = S(inputs[1], fn);
                var r = new MatrixValue(c0.R, c0.C, NumKind.Logical); if (c0.ND != null) r.SetDims(c0.Dims);
                for (int k = 0; k < c0.Numel; k++) r.Re[k] = S(it.CallFunction("class", new[] { c0.Data[k] }, 1)[0], fn) == cls ? 1 : 0;
                return Out(r);
            }
            if (name == "size" && inputs.Count == 2)
            {
                int d = (int)D(inputs[1], fn);
                if (d < 1) throw new OctaveException("cellfun: K must be a positive integer");
                var r = new MatrixValue(c0.R, c0.C); if (c0.ND != null) r.SetDims(c0.Dims);
                for (int k = 0; k < c0.Numel; k++) { var dd = c0.Data[k].Dims; r.Re[k] = d <= dd.Length ? dd[d - 1] : 1; }
                return Out(r);
            }
            if (inputs.Count > 1) quick = null;
            if (quick != null)
            {
                var r = new MatrixValue(c0.R, c0.C, name is "isempty" or "isreal" or "islogical" ? NumKind.Logical : NumKind.Double);
                if (c0.ND != null) r.SetDims(c0.Dims);
                for (int k = 0; k < c0.Numel; k++) r.Re[k] = quick(c0.Data[k]);
                return Out(r);
            }
            func = new FunctionHandleValue { Name = name };
        }
        if (func is MatrixValue fm && fm.IsChar) func = new FunctionHandleValue { Name = fm.AsString() };
        if (func is not FunctionHandleValue) throw new OctaveException($"{fn}: FCN must be a function handle");

        var arrs = inputs.Select(v => v as ArrayValue ?? throw new OctaveException($"{fn}: invalid argument")).ToList();
        if (isCell && arrs.Any(x => x is not CellValue)) throw new OctaveException("cellfun: C must be a cell array");
        var lead = arrs.FirstOrDefault(x => x.Numel != 1) ?? arrs[0];
        int R = lead.R, C = lead.C;
        var dims0 = lead.Dims;
        foreach (var x in arrs) if (x.Numel != 1 && !x.Dims.SequenceEqual(dims0)) throw new OctaveException($"{fn}: all the input arguments must have the same size and shape");
        int nout = Math.Max(nargout, 1);
        int n = lead.Numel;
        var results = new Value[nout][];
        for (int o = 0; o < nout; o++) results[o] = new Value[n];
        for (int k = 0; k < n; k++)
        {
            var args = arrs.Select(x => isCell ? ((CellValue)x).Data[x.Numel == 1 ? 0 : k] : x.Gather(new[] { x.Numel == 1 ? 0 : k }, 1, 1)).ToArray();
            Value[] r;
            try { r = it.CallCallable(func, args, nargout == 0 ? (uniform ? 1 : 1) : nout); }
            catch (OctaveException ex) when (errHandler != null && ex is not InterruptException)
            {
                var es = new StructValue(); es.Set("message", Str(ex.Message)); es.Set("identifier", Str(ex.Identifier)); es.Set("index", Num(k + 1));
                r = it.CallCallable(errHandler, new Value[] { es }.Concat(args).ToArray(), nout);
            }
            if (r.Length < nout && !(nargout == 0 && r.Length == 0)) throw new OctaveException($"{fn}: function returned fewer than nargout values");
            for (int o = 0; o < nout; o++) results[o][k] = o < r.Length ? r[o] : MatrixValue.Empty();
            if (nargout == 0 && r.Length == 0) return Array.Empty<Value>();
        }
        var outs = new Value[nout];
        for (int o = 0; o < nout; o++)
        {
            if (!uniform) { var cv = new CellValue(R, C, results[o]); if (dims0.Length > 2) cv.SetDims(dims0); outs[o] = cv; continue; }
            var m = new MatrixValue(R, C);
            bool allLogical = n > 0, anyComplex = false;
            for (int k = 0; k < n; k++)
            {
                if (results[o][k] is not MatrixValue rv || rv.Numel != 1)
                    throw new OctaveException($"{fn}: all values must be scalars when UniformOutput = true; use the 'UniformOutput', false options");
                if (!rv.IsLogical) allLogical = false;
                if (rv.IsComplex) { anyComplex = true; m.Im ??= new double[n]; m.Im[k] = rv.Im![0]; }
                m.Re[k] = rv.Re[0];
            }
            if (dims0.Length > 2) m.SetDims(dims0);
            if (allLogical) m.Kind = NumKind.Logical;
            else if (n > 0 && results[o].All(v => ((MatrixValue)v).Kind == ((MatrixValue)results[o][0]).Kind) && ((MatrixValue)results[o][0]).Kind != NumKind.Char) m.Kind = ((MatrixValue)results[o][0]).Kind;
            else if (n > 0 && results[o].All(v => ((MatrixValue)v).IsChar)) m.Kind = NumKind.Char;
            outs[o] = anyComplex ? m.Narrow() : m;
        }
        return outs;
    }
}
