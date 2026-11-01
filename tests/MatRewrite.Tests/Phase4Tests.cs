using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase4Tests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "mrp4_" + Guid.NewGuid().ToString("N"));
    public Phase4Tests()
    {
        Directory.CreateDirectory(dir);
        File(@"@Acc/Acc.m", @"classdef Acc < handle
  properties
    total = 0
  end
end
");
        File(@"@Acc/add.m", @"function add(a, v)
  a.total = a.total + v;
end
");
        File(@"@Poly/Poly.m", @"function p = Poly(c)
  s.coef = c;
  p = class(s, 'Poly');
end
");
        File(@"@Poly/polyval2.m", @"function y = polyval2(p, x)
  y = polyval(p.coef, x);
end
");
        File(@"Color.m", @"classdef Color
  properties
    R, G, B
  end
  methods
    function c = Color(r, g, b)
      c.R = r; c.G = g; c.B = b;
    end
  end
  enumeration
    Red (1, 0, 0)
    Green (0, 1, 0)
    Blue (0, 0, 1)
  end
end
");
        File(@"Pt.m", @"classdef Pt
  properties
    x = 0
    y = 0
  end
  methods
    function p = Pt(x, y)
      if nargin > 0, p.x = x; p.y = y; end
    end
    function p = set.x(p, v)
      p.x = abs(v);
    end
    function d = norms(ps)
      d = arrayfun(@(k) hypot(ps(k).x, ps(k).y), 1:numel(ps));
    end
  end
end
");
        File(@"Shape.m", @"classdef Shape
  properties
    name = 'shape'
  end
  methods
    function s = Shape(n)
      if nargin > 0, s.name = n; end
    end
    function a = area(s)
      a = 0;
    end
  end
end
");
        File(@"Sq.m", @"classdef Sq < Shape
  properties
    side = 1
  end
  methods
    function s = Sq(a)
      s@Shape('square');
      s.side = a;
    end
    function a = area(s)
      a = s.side^2;
    end
  end
end
");
        File(@"Temp.m", @"classdef Temp < handle
  properties
    C = 0
  end
  properties (Dependent)
    F
  end
  properties (SetAccess = private)
    Log = 0
  end
  properties (Access = private)
    secret = 42
  end
  events
    Changed
  end
  methods
    function obj = Temp(c)
      if nargin > 0, obj.C = c; end
    end
    function v = get.F(obj)
      v = obj.C * 9/5 + 32;
    end
    function set.F(obj, v)
      obj.C = (v - 32) * 5/9;
    end
    function set.C(obj, v)
      if v < -273.15, error('below absolute zero'); end
      obj.C = v;
      obj.Log = obj.Log + 1;
      notify(obj, 'Changed');
    end
    function s = peek(obj)
      s = obj.secret + hidden_helper(obj);
    end
  end
  methods (Access = private)
    function r = hidden_helper(obj)
      r = 1;
    end
  end
  methods (Static)
    function t = boiling()
      t = Temp(100);
    end
  end
end
");
    }
    public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

    void File(string name, string text)
    {
        var p = Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        System.IO.File.WriteAllText(p, text);
    }

    string Run(string code)
    {
        var o = new StringWriter();
        var it = new Interpreter(o);
        it.SearchPath.Add(dir);
        it.Eval(code);
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    [Fact] public void SparseSolve() => Assert.Equal(@"1".Replace("\r\n", "\n").Trim(), Run(@"A=sparse([4 1 0;1 4 1;0 1 4]); b=[1;2;3]; x=A\b; disp(norm(full(A)*x-b)<1e-12)"));
    [Fact] public void SparseSpdiagsSolve() => Assert.Equal(@"1".Replace("\r\n", "\n").Trim(), Run(@"n=50; e=ones(n,1); K=spdiags([-e 2*e -e],-1:1,n,n); x=K\e; disp(norm(K*x-e)<1e-9 && issparse(K))"));
    [Fact] public void SparseFind() => Assert.Equal(@"2 3 7".Replace("\r\n", "\n").Trim(), Run(@"S=sparse([2],[3],[7],4,4); [i,j,v]=find(S); printf('%d %d %d',i,j,v)"));
    [Fact] public void SparseIndexAssign() => Assert.Equal(@"1 5 3".Replace("\r\n", "\n").Trim(), Run(@"S=speye(4); S(2,3)=5; printf('%d %g %d', issparse(S), S(2,3), nnz(S(1:2,:)))"));
    [Fact] public void SparseOpsKeep() => Assert.Equal(@"1 1 0".Replace("\r\n", "\n").Trim(), Run(@"S=speye(3); printf('%d %d %d', issparse(S*2), issparse(S'), issparse(S+1))"));
    [Fact] public void SparseEigs() => Assert.Equal(@"1".Replace("\r\n", "\n").Trim(), Run(@"A=spdiags((1:20)',0,20,20); d=eigs(A,3); disp(norm(sort(d)-[18;19;20])<1e-8)"));
    [Fact] public void SparseSvds() => Assert.Equal(@"1".Replace("\r\n", "\n").Trim(), Run(@"A=sparse(diag([5 3 1])); s=svds(A,2); disp(norm(s-[5;3])<1e-10)"));
    [Fact] public void SparseNnzSprand() => Assert.Equal(@"1".Replace("\r\n", "\n").Trim(), Run(@"S=sprand(100,100,0.05); disp(nnz(S)>0 && nnz(S)<=600 && issparse(S))"));
    [Fact] public void SparseConcat() => Assert.Equal(@"1 6".Replace("\r\n", "\n").Trim(), Run(@"S=[speye(2) speye(2)]; printf('%d %d', issparse(S), numel(S)-nnz(S)+2)"));
    [Fact] public void SparseSum() => Assert.Equal(@"3".Replace("\r\n", "\n").Trim(), Run(@"disp(full(sum(sum(speye(3)))))"));
    [Fact] public void ClassGetSetDependent() => Assert.Equal(@"86
100".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(30); disp(t.F); t.F=212; disp(t.C)"));
    [Fact] public void ClassSetValidation() => Assert.Equal(@"below absolute zero".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); try, t.C=-300; catch e, disp(e.message), end"));
    [Fact] public void ClassSetAccess() => Assert.Equal(@"Temp: property 'Log' has private access and cannot be set from this context".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); try, t.Log=5; catch e, disp(e.message), end"));
    [Fact] public void ClassPrivateProp() => Assert.Equal(@"Temp: property 'secret' has private access and cannot be read from this context".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); try, t.secret; catch e, disp(e.message), end"));
    [Fact] public void ClassPrivateMethod() => Assert.Equal(@"43".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); try, t.hidden_helper(); catch e, end; disp(t.peek())"));
    [Fact] public void ClassStatic() => Assert.Equal(@"212".Replace("\r\n", "\n").Trim(), Run(@"b=Temp.boiling(); disp(b.F)"));
    [Fact] public void ClassEvents() => Assert.Equal(@"hit 5
hit 6".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); l=addlistener(t,'Changed',@(s,e) printf('hit %g\n', s.C)); t.C=5; t.C=6; delete(l); t.C=7;"));
    [Fact] public void ClassEventName() => Assert.Equal(@"Changed".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); addlistener(t,'Changed',@(s,e) disp(e.EventName)); t.C=1;"));
    [Fact] public void ClassBadEvent() => Assert.Equal(@"notify: 'Nope' is not an event of class 'Temp'".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); try, notify(t,'Nope'); catch e, disp(e.message), end"));
    [Fact] public void ClassHandleDelete() => Assert.Equal(@"0".Replace("\r\n", "\n").Trim(), Run(@"t=Temp(); delete(t); disp(isvalid(t))"));
    [Fact] public void ClassValueSetter() => Assert.Equal(@"5".Replace("\r\n", "\n").Trim(), Run(@"p=Pt(1,2); p.x=-5; disp(p.x)"));
    [Fact] public void ClassValueSemantics() => Assert.Equal(@"1 2".Replace("\r\n", "\n").Trim(), Run(@"p=Pt(1,2); q=p; q.x=2; printf('%d %d', p.x, q.x)"));
    [Fact] public void ClassObjectArray() => Assert.Equal(@"1 3 Pt".Replace("\r\n", "\n").Trim(), Run(@"ps=[Pt(3,4) Pt(6,8) Pt(1,0)]; s=size(ps); printf('%d %d %s', s(1), s(2), class(ps))"));
    [Fact] public void ClassObjectArrayProps() => Assert.Equal(@"3 0 1".Replace("\r\n", "\n").Trim(), Run(@"ps=[Pt(3,4) Pt(6,8) Pt(1,0)]; ps(2).x=0; printf('%d ', [ps.x])"));
    [Fact] public void ClassObjectArrayMethod() => Assert.Equal(@"5 10 1".Replace("\r\n", "\n").Trim(), Run(@"ps=[Pt(3,4) Pt(6,8) Pt(1,0)]; printf('%g ', ps.norms())"));
    [Fact] public void ClassObjectArrayGrow() => Assert.Equal(@"5 0".Replace("\r\n", "\n").Trim(), Run(@"ps=[Pt(3,4) Pt(6,8)]; ps(5)=Pt(9,9); printf('%d %d', numel(ps), ps(4).x)"));
    [Fact] public void ClassEnum() => Assert.Equal(@"c = Red".Replace("\r\n", "\n").Trim(), Run(@"c = Color.Red"));
    [Fact] public void ClassEnumOps() => Assert.Equal(@"1 0 Red 1".Replace("\r\n", "\n").Trim(), Run(@"c=Color.Red; printf('%d %d %s %d', c==Color.Red, c==Color.Blue, char(c), c.R)"));
    [Fact] public void ClassEnumeration() => Assert.Equal(@"Red Green Blue".Replace("\r\n", "\n").Trim(), Run(@"[m,n]=enumeration('Color'); printf('%s ', n{:})"));
    [Fact] public void ClassOldStyleFolder() => Assert.Equal(@"11 Poly".Replace("\r\n", "\n").Trim(), Run(@"q=Poly([1 2 3]); printf('%d %s', polyval2(q,2), class(q))"));
    [Fact] public void ClassdefFolderMethods() => Assert.Equal(@"11".Replace("\r\n", "\n").Trim(), Run(@"a=Acc(); add(a,5); add(a,6); disp(a.total)"));
    [Fact] public void ClassInheritSuperCtor() => Assert.Equal(@"9 square 1".Replace("\r\n", "\n").Trim(), Run(@"s=Sq(3); printf('%d %s %d', area(s), s.name, isa(s,'Shape'))"));
    [Fact] public void ClassPolymorphism() => Assert.Equal(@"0 4".Replace("\r\n", "\n").Trim(), Run(@"c={Shape(), Sq(2)}; printf('%d ', cellfun(@area, c))"));
}
