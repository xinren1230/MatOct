// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Sparse;

/// <summary>Bridges dense-only builtins: sparse args are densified and, for functions whose
/// Octave result is sparse, the first output is converted back.</summary>
public static class SparseBridge
{
    /// <summary>Builtins that handle SparseValue arguments themselves.</summary>
    public static readonly HashSet<string> Aware = new()
    {
        "sparse", "sortrows", "full", "issparse", "nnz", "nzmax", "nonzeros", "find", "size", "numel", "rows", "columns", "length",
        "ndims", "isempty", "class", "isnumeric", "isreal", "islogical", "isfloat", "isa", "speye", "spones", "spfun",
        "spalloc", "sprand", "sprandn", "sprandsym", "spdiags", "spzeros", "eigs", "svds", "isscalar", "isvector",
        "ismatrix", "issquare", "isrow", "iscolumn", "disp", "display", "spfun", "spy",
    };

    /// <summary>Functions whose result keeps sparsity in Octave.</summary>
    static readonly HashSet<string> KeepSparse = new()
    {
        "abs", "sqrt", "real", "imag", "conj", "sign", "floor", "ceil", "round", "fix", "sin", "tan", "asin", "atan",
        "sinh", "tanh", "asinh", "atanh", "expm1", "log1p", "sum", "prod", "cumsum", "max", "min", "diag", "tril", "triu",
        "reshape", "repmat", "kron", "fliplr", "flipud", "circshift", "logical", "double", "not", "and", "or", "xor",
        "uminus", "uplus", "inv", "power", "rdivide", "spfun", "cat", "horzcat", "vertcat", "permute", "rot90", "any", "all",
        "transpose", "ctranspose", "sort", "nth_element", "ipermute", "squeeze", "flip", "cummax", "cummin", "cumprod", "hsv2rgb", "rgb2hsv", "eq", "ne", "lt", "le", "gt", "ge", "isnan", "isinf",
    };

    public static Value[] Call(string name, Value[] args, Func<Value[], Value[]> invoke)
    {
        var dargs = args.Select(a => a is SparseValue s ? s.ToDense() : a).ToArray();
        var outs = invoke(dargs);
        if (KeepSparse.Contains(name) && outs.Length > 0 && outs[0] is MatrixValue m && m.ND == null && !m.IsChar && m.Kind is NumKind.Double or NumKind.Logical)
        {
            outs = (Value[])outs.Clone();
            outs[0] = SparseValue.FromDense(m);
        }
        return outs;
    }
}
