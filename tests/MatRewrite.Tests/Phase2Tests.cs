using MatRewrite.Core.Runtime;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MatRewrite.Tests;

public class Phase2Tests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "mrtest_" + Guid.NewGuid().ToString("N"));
    public Phase2Tests() { Directory.CreateDirectory(dir); }
    public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

    string Run(string code)
    {
        var o = new StringWriter();
        var it = new Interpreter(o);
        var old = Environment.CurrentDirectory;
        Environment.CurrentDirectory = dir;
        try { it.Eval(code); it.CloseAllFiles(); }
        finally { Environment.CurrentDirectory = old; }
        return o.ToString().Replace("\r\n", "\n");
    }

    string P(string expr)
    {
        int k = expr.LastIndexOf("; ");
        return k < 0 ? Run($"printf('%s', mat2str({expr}));") : Run(expr[..(k + 2)] + $"printf('%s', mat2str({expr[(k + 2)..]}));");
    }

    string RunFile(string name, string body)
    {
        var p = Path.Combine(dir, name);
        File.WriteAllText(p, body);
        var o = new StringWriter();
        var it = new Interpreter(o);
        try { it.RunFile(p); }
        catch (OctaveException e) { o.Write("ERR:" + e.Message + "|" + Interpreter.FormatStack(e)); }
        return o.ToString().Replace("\r\n", "\n");
    }

    // ---- N-D arrays
    [Fact] public void NdSize() => Assert.Equal("[2 3 4]", P("size(zeros(2,3,4))"));
    [Fact] public void NdSumDim3() => Assert.Equal("[6 10;8 12]", P("sum(reshape(1:8,2,2,2),3)"));
    [Fact] public void NdCat() => Assert.Equal("[2 2 2]", P("size(cat(3,[1 2;3 4],[5 6;7 8]))"));
    [Fact] public void NdPermute() => Assert.Equal("[4 2 3]", P("size(permute(zeros(2,3,4),[3 1 2]))"));
    [Fact] public void NdSqueeze() => Assert.Equal("[3 4]", P("size(squeeze(zeros(1,3,4)))"));
    [Fact] public void NdIndexAssignGrow() => Assert.Equal("[2 2 3]", P("A=zeros(2,2); A(:,:,3)=1; size(A)"));
    [Fact] public void NdIndexRead() => Assert.Equal("7", P("A=reshape(1:8,2,2,2); A(1,2,2)"));
    [Fact] public void NdDisplayPages() => Assert.Contains("ans(:,:,2) =", Run("A=reshape(1:8,2,2,2)"));
    [Fact] public void NdBroadcast() => Assert.Equal("[2 2 2]", P("size(ones(2,2,2) + [1 2])"));

    // ---- integer classes
    [Fact] public void IntSaturate() => Assert.Equal("127", Run("x = int8(100) + int8(100); printf('%d', x);"));
    [Fact] public void IntClassKept() => Assert.Equal("int8", Run("printf('%s', class(int8(5) * 2.6));"));
    [Fact] public void IntRounds() => Assert.Equal("13", Run("printf('%d', int8(5) * 2.6);"));
    [Fact] public void UintFloor() => Assert.Equal("0", Run("printf('%d', uint8(5) - 10);"));
    [Fact] public void IntDivRounds() => Assert.Equal("3", Run("printf('%d', int8(5) / int8(2));"));
    [Fact] public void IntMax() => Assert.Equal("32767", Run("printf('%d', intmax('int16'));"));
    [Fact] public void IntMixedError() => Assert.Contains("binary operator '+' not implemented for 'int8 scalar' by 'int16 scalar' operations",
        Run("try, int8(1)+int16(1); catch e, disp(e.message); end"));
    [Fact] public void SingleWins() => Assert.Equal("single", Run("printf('%s', class(single(1) + 2));"));
    [Fact] public void IdivideFix() => Assert.Equal("3", Run("printf('%d', idivide(int32(7), int32(2)));"));
    [Fact] public void ConcatInt() => Assert.Equal("int8", Run("printf('%s', class([int8(1) 2.7]));"));

    // ---- scoping & errors
    [Fact] public void Subfunction() => Assert.Equal("15\n", RunFile("main1.m", "function main1\n  disp(helper(5))\nend\nfunction r = helper(x)\n  r = 3*x;\nend\n"));
    [Fact] public void NestedSharesScope() => Assert.Equal("15\n",
        RunFile("outer.m", "function outer\n  a = 10;\n  function inner(x)\n    a = a + x;\n  end\n  inner(5);\n  disp(a)\nend\n"));
    [Fact] public void Persistent() => Assert.Equal("3\n",
        RunFile("pmain.m", "function pmain\n  for k=1:3, c = counter(); end\n  disp(c)\nend\nfunction n = counter()\n  persistent k\n  if isempty(k), k = 0; end\n  k = k + 1; n = k;\nend\n"));
    [Fact] public void Global() => Assert.Equal("42\n",
        RunFile("gmain.m", "function gmain\n  global G\n  G = 42;\n  show();\nend\nfunction show()\n  global G\n  disp(G)\nend\n"));
    [Fact] public void ErrorStack()
    {
        var o = RunFile("emain.m", "function emain\n  try\n    bad();\n  catch err\n    printf('%s %d', err.stack(1).name, err.stack(1).line);\n  end\nend\nfunction bad()\n  x = 1;\n  error('boom');\nend\n");
        Assert.Equal("bad 10", o);
    }
    [Fact] public void ErrorTrace() => Assert.Contains("called from", RunFile("tmain.m", "function tmain\n  f2();\nend\nfunction f2()\n  error('x');\nend\n"));

    // ---- file I/O
    [Fact] public void FprintfFgetl() => Assert.Equal("a=1|b=2|-1",
        Run("f=fopen('t.txt','w'); fprintf(f,'a=%d\\n',1); fprintf(f,'b=%d\\n',2); fclose(f); f=fopen('t.txt','r'); x=fgetl(f); y=fgetl(f); z=fgetl(f); fclose(f); printf('%s|%s|%d', x, y, z);"));
    [Fact] public void FeofLoop() => Assert.Equal("3", Run("f=fopen('t.txt','w'); fprintf(f,'1\\n2\\n3\\n'); fclose(f); f=fopen('t.txt'); n=0; while ~feof(f), fgetl(f); n=n+1; end; fclose(f); printf('%d',n);"));
    [Fact] public void FreadFwrite() => Assert.Equal("[1;2;300]", P("f=fopen('b.bin','w'); fwrite(f,[1 2 300],'uint16'); fclose(f); f=fopen('b.bin'); d=fread(f,Inf,'uint16'); fclose(f); d"));
    [Fact] public void FscanfMatrix() => Assert.Equal("[1 3;2 4]", P("f=fopen('t.txt','w'); fprintf(f,'1 2\\n3 4\\n'); fclose(f); f=fopen('t.txt'); A=fscanf(f,'%d',[2 Inf]); fclose(f); A"));
    [Fact] public void Sscanf() => Assert.Equal("[1.5;2.5]", P("sscanf('1.5,2.5','%f,%f')"));
    [Fact] public void Textscan() => Assert.Equal("b 2", Run("C=textscan(sprintf('a,1\\nb,2'),'%s %f','Delimiter',','); printf('%s %d', C{1}{2}, C{2}(2));"));
    [Fact] public void CsvRoundTrip() => Assert.Equal("1", Run("M=magic(4); csvwrite('m.csv',M); printf('%d', isequal(csvread('m.csv'),M));"));
    [Fact] public void DlmreadOffset() => Assert.Equal("[5 7;9 2]", P("dlmwrite('m.txt',magic(3)); dlmread('m.txt',',',1,1)"));
    [Fact] public void IsFileExist() => Assert.Equal("1 1 2", Run("fclose(fopen('e.txt','w')); printf('%d %d %d', isfile('e.txt'), isfolder('.'), exist('e.txt','file'));"));
    [Fact] public void SaveLoadText() => Assert.Equal("1",
        Run("a=[1 2;3 4]; b='hi'; c={1,'x'}; s.f=int16(3); s.g=[1+2i 3]; n=reshape(1:8,2,2,2); t=struct('q',{1,'z'}); s0=s; save('v.txt'); clear a b c s t n; load('v.txt'); " +
            "printf('%d', isequal(a,[1 2;3 4]) && strcmp(b,'hi') && isequal(c,{1,'x'}) && isa(s.f,'int16') && isequal(s.g,[1+2i 3]) && isequal(size(n),[2 2 2]) && strcmp(t(2).q,'z'));"));
    [Fact] public void SaveLoadMat() => Assert.Equal("1",
        Run("a=[1 2;3 4]; b='hi'; c={1,'x'}; s.f=int16(3); s.l=true; t=struct('q',{1,'z'}); save('-v6','v.mat','a','b','c','s','t'); S=load('v.mat'); " +
            "printf('%d', isequal(S.a,a) && strcmp(S.b,'hi') && isequal(S.c,c) && isa(S.s.f,'int16') && islogical(S.s.l) && strcmp(S.t(2).q,'z'));"));
    [Fact] public void SaveAscii() => Assert.Equal("[1 2;3 4]", P("x=[1 2;3 4]; save('-ascii','q.txt','x'); y=load('q.txt'); y"));

    // ---- containers.Map
    [Fact] public void MapBasic() => Assert.Equal("2 3 1 0", Run("m=containers.Map(); m('a')=1; m('b')=[2 3]; v=m('b'); printf('%d %d %d %d', m.Count, v(2), isKey(m,'a'), m.isKey('z'));"));
    [Fact] public void MapCtorKeysSorted() => Assert.Equal("x,y 20", Run("m=containers.Map({'y','x'},{20,10}); k=keys(m); printf('%s,%s %d', k{1}, k{2}, m('y'));"));
    [Fact] public void MapNumericRemove() => Assert.Equal("1 three", Run("m=containers.Map('KeyType','double','ValueType','any'); m(3)='three'; m(1)='one'; remove(m,1); printf('%d %s', m.Count, m(3));"));
    [Fact] public void MapHandleSemantics() => Assert.Equal("99", Run("m=containers.Map(); m('a')=1; g=m; g('a')=99; printf('%d', m('a'));"));
    [Fact] public void MapMissingKey() => Assert.Contains("not present", Run("m=containers.Map(); try, m('q'); catch e, disp(e.message); end"));
}
