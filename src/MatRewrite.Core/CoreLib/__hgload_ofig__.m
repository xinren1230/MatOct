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
## @deftypefn  {} {@var{h} =} __hgload_ofig__ (@var{filename})
## @deftypefnx {} {[@var{h}, @var{old_prop}] =} __hgload_ofig__ (@var{filename}, @var{prop_struct})
## Load the graphics objects in @var{filename} into a vector of graphics
## handles @var{h}.
##
## If @var{filename} has no extension, Octave will try to find the file with
## and without the default extension @file{.ofig}.
##
## If provided, the elements of structure @var{prop_struct} will be used to
## override the properties of top-level objects stored in @var{filename}, and
## the saved values from @var{filename} will be stored in @var{old_prop}.
## @var{old_prop} is a cell array matching the size of @var{h}; each cell
## contains a structure of the existing property names and values before being
## overridden.
##
## @seealso{openfig, hgsave, struct2hdl}
## @end deftypefn

function [h, old_prop] = __hgload_ofig__ (filename, prop_struct = struct ())

  ## Check number of input arguments
  if (nargin == 0)
    print_usage ();
  endif

  ## Check type of second input argument
  if (! isstruct (prop_struct))
    error ("hgload: PROP_STRUCT must be a struct");
  endif

  ## Check file existence
  [~, ~, ext] = fileparts (filename);
  if (isempty (ext))
    if (! isempty (file_in_loadpath ([filename ".ofig"])))
      filename = [filename ".ofig"];
    elseif (isempty (file_in_loadpath (filename)))
      error ("hgload: unable to locate file %s", filename);
    endif
  else
    if (isempty (file_in_loadpath (filename)))
      error ("hgload: unable to locate file %s", filename);
    endif
  endif

  ## Load the handle structure
  hgs = {"s_oct40", "hgS_050200", "hgS_070000"};
  hg = load (filename);
  fig_file_version = isfield (hg, hgs);
  if (nnz (fig_file_version) != 1)
    error ("hgload: could not load hgsave-formatted object in file %s", filename);
  endif
  hg = hg.(hgs{fig_file_version});

  ## Override properties of top-level objects
  calc_old_prop = false;
  if (nargout > 1)
    calc_old_prop = true;
    old_prop = repmat ({[]}, 1, numel (hg));
  endif
  fn_new = fieldnames (prop_struct);
  if (! isempty (fn_new))
    for i = 1:numel (hg)
      fn_old = fieldnames (hg(i).properties);
      for j = 1:numel (fn_new)
        idx = ismember (lower (fn_old), lower (fn_new{j}));
        if (any (idx))
          if (calc_old_prop)
            old_prop{i}.(fn_new{j}) = hg(i).properties.(fn_old{idx});
          endif
          hg(i).properties.(fn_old{idx}) = prop_struct.(fn_new{j});
        endif
      endfor
    endfor
  endif

  ## Build the graphics handle object
  h = zeros (1, numel (hg));
  for i = 1:numel (hg)
    h(i) = struct2hdl (hg(i));
  endfor

endfunction


## Functional test for __hgload_ofig__/hgsave pair is in hgsave.m

## Test overriding saved properties with second input

## Test input validation
