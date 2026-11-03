########################################################################
##
## Copyright (C) 2014-2026 The Octave Project Developers
##
## See the file COPYRIGHT.md in the top-level directory of this
## distribution or <https://octave.org/copyright/>.
##
## This file is part of Octave.
##
## Octave is free software: you can redistribute it and/or modify it
## under the terms of the GNU General Public License as published by
## the Free Software Foundation, either version 3 of the License, or
## (at your option) any later version.
##
## Octave is distributed in the hope that it will be useful, but
## WITHOUT ANY WARRANTY; without even the implied warranty of
## MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
## GNU General Public License for more details.
##
## You should have received a copy of the GNU General Public License
## along with Octave; see the file COPYING.  If not, see
## <https://www.gnu.org/licenses/>.
##
########################################################################

## -*- texinfo -*-
## @deftypefn  {} {} __hgsave_ofig__ (@var{filename})
## @deftypefnx {} {} __hgsave_ofig__ (@var{h}, @var{filename})
## @deftypefnx {} {} __hgsave_ofig__ (@var{h}, @var{filename}, @var{fmt})
## Save the graphics handle(s) @var{h} to the file @var{filename} in the format
## @var{fmt}.
##
## If unspecified, @var{h} is the current figure as returned by @code{gcf}.
##
## When @var{filename} does not have an extension the default filename
## extension @file{.ofig} will be appended.
##
## If present, @var{fmt} must be one of the following:
##
## @itemize @bullet
## @item @option{-binary}, @option{-float-binary}
##
## @item @option{-hdf5}, @option{-float-hdf5}
##
## @item @option{-V7}, @option{-v7}, @option{-7}, @option{-mat7-binary}
##
## @item @option{-V6}, @option{-v6}, @option{-6}, @option{-mat6-binary}
##
## @item @option{-text}
##
## @item @option{-zip}, @option{-z}
## @end itemize
##
## The default format is @option{-binary} to minimize storage.
##
## Programming Note: When producing graphics for final publication use
## @code{print} or @code{saveas}.  When it is important to be able to continue
## to edit a figure as an Octave object, use @code{__hgsave_ofig__}/@code{hgload}.
## @seealso{hgload, hdl2struct, savefig, saveas, print}
## @end deftypefn

function __hgsave_ofig__ (h, filename, fmt = "-text")

  if (nargin < 1)
    print_usage ();
  endif

  ## Check input arguments
  if (nargin == 1 && ischar (h))
    filename = h;
    h = get (0, "currentfigure");
    if (isempty (h))
      error ("hgsave: no current figure to save");
    endif
  elseif (! (all (ishghandle (h)) && ischar (filename)))
    print_usage ();
  endif

  ## Check file extension
  [~, ~, ext] = fileparts (filename);
  if (isempty (ext))
    filename = [filename ".ofig"];
  endif

  for i = 1:numel (h)
    s_oct40(i) = hdl2struct (h(i));
  endfor
  save (fmt, filename, "s_oct40");

endfunction



## Test input validation
