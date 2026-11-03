function [retval, status] = __makeinfo__ (text, output_type = "plain text", fsee_also)
  ## Convert Texinfo help text to plain text (or HTML) without an external
  ## makeinfo program.  MatOct implementation.
  if (nargin < 1)
    print_usage ();
  endif
  if (! ischar (text))
    error ("__makeinfo__: first input argument must be a string");
  endif
  if (! ischar (output_type))
    error ("__makeinfo__: second input argument must be a string");
  endif
  if (nargin < 3)
    fsee_also = @(T) ["\nSee also:", sprintf(" %s,", T{:})(1:end-1), "\n"];
  endif
  if (! is_function_handle (fsee_also))
    error ("__makeinfo__: third input argument must be a function handle");
  endif
  status = 0;
  if (strcmpi (output_type, "texinfo"))
    retval = text;
    return;
  endif
  t = strrep (text, "-*- texinfo -*-", "");
  t = regexprep (t, '@tex\n[\s\S]*?@end tex', '');
  t = regexprep (t, '@(iftex|ifhtml|html)\n[\s\S]*?@end \1', '');
  ## See also
  [s, e, te, m, tok] = regexp (t, '@seealso\{((?:[^}]|@\})*)\}');
  for k = numel (s):-1:1
    names = strtrim (strsplit (strrep (tok{k}{1}, "\n", " "), ","));
    names = regexprep (names, '@ref\{(?:[^}]*?),?(?:XREF)?([^,}]+)\}', '$1');
    t = [t(1:s(k)-1), fsee_also(names), t(e(k)+1:end)];
  endfor
  t = regexprep (t, '@ref\{(?:[^}]*?),?(?:XREF)?([^,}]+)\}', '$1');
  t = regexprep (t, '@xref\{(?:[^}]*?),?(?:XREF)?([^,}]+)\}', 'See $1');
  t = regexprep (t, '@pxref\{(?:[^}]*?),?(?:XREF)?([^,}]+)\}', 'see $1');
  t = regexprep (t, '^[ \t]*@deftypefnx?[ \t]*\{[^}\n]*\}[ \t]*([^\n]*)', ' -- $1', 'lineanchors');
  t = regexprep (t, '^[ \t]*@(end|example|group|noindent|table|itemize|enumerate|ifnottex|ifinfo|cindex|findex|multitable|sp|smallexample|display|format|verbatim|quotation|c|comment)\b[^\n]*\n?', '', 'lineanchors');
  t = regexprep (t, '@item(x)?[ \t]*', '* ');
  t = strrep (t, "@tab", "  ");
  for iter = 1:10
    old = t;
    ## @var{x} -> X
    [s, e, te, m, tok] = regexp (t, '@var\{([^{}]*)\}');
    for k = numel (s):-1:1
      t = [t(1:s(k)-1), upper(tok{k}{1}), t(e(k)+1:end)];
    endfor
    t = regexprep (t, '@(code|samp|file|command|env|option|kbd|key)\{([^{}]*)\}', '''$2''');
    t = regexprep (t, '@(qcode|emph|strong|dfn|cite|math|w|asis|url|uref|email|acronym|nospell|abbr|sc|t|r|b|i|indicateurl|footnote|var)\{([^{}]*)\}', '$2');
    t = regexprep (t, '@dots\{\}', '...');
    t = regexprep (t, '@(equiv|result)\{\}', '==');
    t = regexprep (t, '@(print|error|expansion|bullet|minus|copyright|TeX|LaTeX)\{\}', '');
    if (strcmp (t, old))
      break;
    endif
  endfor
  t = strrep (t, "@{", "{");
  t = strrep (t, "@}", "}");
  t = strrep (t, "@:", "");
  t = strrep (t, "@.", ".");
  t = strrep (t, "@*", "\n");
  t = strrep (t, "@@", "@");
  t = regexprep (t, '\n{3,}', "\n\n");
  if (strcmpi (output_type, "html"))
    t = ["<html><body><pre>\n", t, "\n</pre></body></html>\n"];
  endif
  t = regexprep (t, "^\\s+", "");
  retval = t;
endfunction
