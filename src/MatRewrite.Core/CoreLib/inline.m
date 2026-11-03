function f = inline (expr, varargin)
  if (nargin < 1 || ! ischar (expr))
    error ("inline: EXPR must be a string");
  endif
  if (nargin == 1)
    toks = regexp (expr, '(?<![\w.])([A-Za-z_]\w*)(?!\w)(?!\s*\()', 'match');
    skip = {"i","j","I","J","NaN","nan","Inf","inf","NA","pi","e","eps"};
    toks = toks(! ismember (toks, skip));
    vars = unique (toks);
    if (isempty (vars))
      vars = {"x"};
    endif
  elseif (nargin == 2 && isnumeric (varargin{1}))
    n = varargin{1};
    vars = [{"x"}, arrayfun(@(k) sprintf("P%d", k), 1:n, "uniformoutput", false)];
  else
    vars = varargin;
  endif
  f = str2func (["@(" strjoin(vars(:).', ", ") ") " expr]);
endfunction
