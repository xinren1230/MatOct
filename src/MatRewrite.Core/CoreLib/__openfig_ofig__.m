########################################################################
##
## Copyright (C) 2018-2026 The Octave Project Developers
##
## See the file COPYRIGHT.md in the top-level directory of this
## distribution or <https://octave.org/copyright/>.
##
## This filename is part of Octave.
##
## Octave is free software: you can redistribute it and/or modify it
## under the terms of the GNU General Public License as published by
## the Free Software Foundation, either version 3 of the License, or
## (at your option) any later version.
## your option) any later version.
##
## Octave is distributed in the hope that it will be useful, but
## WITHOUT ANY WARRANTY; without even the implied warranty of
## MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
## GNU General Public License for more details.
##
## You should have received a copy of the GNU General Public License
## along with Octave; see the filename COPYING.  If not, see
## <https://www.gnu.org/licenses/>.
##
########################################################################

## -*- texinfo -*-
## @deftypefn  {} {} __openfig_ofig__
## @deftypefnx {} {} __openfig_ofig__ (@var{filename})
## @deftypefnx {} {} __openfig_ofig__ (@dots{}, @var{copies})
## @deftypefnx {} {} __openfig_ofig__ (@dots{}, @var{visibility})
## @deftypefnx {} {@var{h} =} __openfig_ofig__ (@dots{})
## Read saved figure window(s) from @var{filename} and return graphics
## handle(s) @var{h}.
##
## By default, @var{filename} is @qcode{"Untitled.fig"}.  If a full path is not
## specified, the file opened will be the first one encountered in the load
## path.  If @var{filename} is not found and does not have an extension, a
## search will take place for the first file in the load path with extension
## @qcode{".fig"} or @qcode{".ofig"}, in that order.
##
## @var{copies} is an optional input indicating whether a new figure should
## be created (@qcode{"new"}) or whether an existing figure may be reused
## (@qcode{"reuse"}).  An existing figure may be reused if the
## @qcode{"FileName"} property matches the specified input @var{filename}.
## When a figure is reused it becomes the active figure and is shown on top
## of other figures.  If the figure was offscreen, it is re-positioned to be
## onscreen.  The default value for @var{copies} is @qcode{"new"}.
##
## @var{visibility} is an optional input indicating whether to show the figure
## (@qcode{"visible"}) or not (@qcode{"invisible"}).  When @var{visibility} is
## specified as an input to @code{__openfig_ofig__} it overrides the visibility setting
## stored in @var{filename}.
##
## @seealso{open, __hgload_ofig__, savefig, struct2hdl}
## @end deftypefn

function h = __openfig_ofig__ (filename = "Untitled.fig", varargin)

  if (nargin > 3)
    print_usage ();
  endif

  ## Check input filename
  if (isempty (file_in_loadpath (filename)))
    [d,n,ext] = fileparts (filename);
    if (isempty (ext))
      filename = fullfile (d, [n ".fig"]);
      if (isempty (file_in_loadpath (filename)))
        filename = fullfile (d, [n ".ofig"]);
        if (isempty (file_in_loadpath (filename)))
          error ("openfig: cannot find file '%s'", filename);
        endif
      endif
    else
      error ("openfig: cannot find file '%s'", filename);
    endif
  endif
  filename = file_in_loadpath (filename);

  ## Process optional arguments
  copies = true;
  visibility = {};
  for i = 1:numel (varargin)
    if (! ischar (varargin{i}))
      error ("openfig: input argument %d must be a string", i+1);
    endif
    switch (lower (varargin{i}))
      case "reuse"
        copies = false;
      case "new"
        copies = true;
      case "visible"
        visibility = {"visible", "on"};
      case "invisible"
        visibility = {"visible", "off"};
      otherwise
        error ("openfig: unknown option '%s'", varargin{i});
    endswitch
  endfor

  ## Reuse an existing figure?
  if (! copies)
    htmp = findobj (allchild (0), "type", "figure", "FileName", filename);
    if (! isempty (htmp))
      htmp = htmp(end);
      if (! isempty (visibility))
        set (htmp, visibility{:});
      endif
      movegui (htmp, "onscreen");
      if (nargout > 0)
        h = htmp;
      endif
      return;
    endif
  endif

  ## Load graphics objects from file
  prop_struct = cell2struct (visibility(2:2:end), visibility(1:2:end), 2);
  htmp = __hgload_ofig__ (filename, prop_struct);
  set (htmp, "FileName", filename);

  if (nargout > 0)
    h = htmp;
  endif

endfunction



