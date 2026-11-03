# GNU Octave compatibility report

Mode: Octave m-file library on path (/workspace/octave-src/scripts)

Files: 864, blocks passed 10096, failed 304, skipped 458, pass rate 97.1% (37 s)

| Area | Files | Pass | Fail | Skip | Rate |
|---|---:|---:|---:|---:|---:|
| +containers | 1 | 45 | 0 | 0 | 100.0% |
| +matlab | 4 | 48 | 0 | 0 | 100.0% |
| @ftp | 14 | 14 | 0 | 0 | 100.0% |
| audio | 32 | 69 | 0 | 28 | 100.0% |
| elfun | 27 | 83 | 0 | 0 | 100.0% |
| general | 67 | 1519 | 23 | 15 | 98.5% |
| geometry | 15 | 148 | 10 | 0 | 93.7% |
| gui | 26 | 89 | 2 | 8 | 97.8% |
| help | 16 | 37 | 11 | 1 | 77.1% |
| image | 50 | 537 | 8 | 24 | 98.5% |
| io | 7 | 40 | 2 | 3 | 95.2% |
| java | 4 | 14 | 1 | 14 | 93.3% |
| legacy | 9 | 143 | 1 | 0 | 99.3% |
| linear-algebra | 36 | 523 | 22 | 1 | 96.0% |
| miscellaneous | 92 | 603 | 28 | 18 | 95.6% |
| ode | 9 | 160 | 0 | 90 | 100.0% |
| optimization | 14 | 126 | 15 | 5 | 89.4% |
| path | 4 | 6 | 1 | 0 | 85.7% |
| plot | 197 | 960 | 85 | 13 | 91.9% |
| polynomial | 27 | 289 | 2 | 0 | 99.3% |
| prefs | 6 | 24 | 0 | 0 | 100.0% |
| profiler | 3 | 10 | 1 | 0 | 90.9% |
| set | 9 | 318 | 3 | 2 | 99.1% |
| signal | 21 | 502 | 3 | 9 | 99.4% |
| sparse | 29 | 248 | 42 | 198 | 85.5% |
| specfun | 21 | 421 | 17 | 0 | 96.1% |
| special-matrix | 11 | 193 | 0 | 0 | 100.0% |
| statistics | 49 | 1766 | 14 | 0 | 99.2% |
| strings | 34 | 496 | 11 | 26 | 97.8% |
| testfun | 13 | 270 | 2 | 2 | 99.3% |
| time | 14 | 377 | 0 | 1 | 100.0% |
| web | 3 | 18 | 0 | 0 | 100.0% |

## Failures

- general/accumarray.m:452 [test] 'x' undefined
- general/bitcmp.m:124 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.61409010644959e+19 4.61168601842739e+18   Abs err 1.1529e+19 exceeds tol 0 by 1e+19
- general/cplxpair.m:147 [assert] sparse: dimension mismatch
- general/cplxpair.m:171 [assert] sparse: dimension mismatch
- general/cplxpair.m:173 [error] error 'sparse: dimension mismatch' does not match <could not pair>
- general/cplxpair.m:175 [error] error 'sparse: dimension mismatch' does not match <could not pair>
- general/fliplr.m:77 [test] =: nonconformant arguments (op1 is 1x1x1, op2 is 1x4)
- general/integral.m:247 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()         1.9475       1.9475      Rel err 6.2282e-12 exceeds tol 1e-12 by 5e-12
- general/integral.m:271 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []        'double'     'single'     Strings don't match
- general/integral.m:281 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()           0       4.6502e-16+2.0944i   Abs err 2.0944 exceeds tol 2.2204e-16 by 2
- general/integral.m:294 [assert] '__inline_ctor__' undefined
- general/integral2.m:326 [assert] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()         1.3863       1.3863      Abs err 2.0662e-07 exceeds tol 1e-07 by 1e-07
- general/interp2.m:643 [test] interp2: XI, YI must have uniform spacing ('meshgrid' format)
- general/interp3.m:257 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)          NaN           NA        'NA' mismatch
- general/isequal.m:509 [shared] Array dimensions exceeded supported range.
- general/isequal.m:532 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            0         Abs err 1 exceeds tol 0 by 1
- general/isequal.m:535 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            0         Abs err 1 exceeds tol 0 by 1
- general/nextpow2.m:137 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (54)          53           54        Abs err 1 exceeds tol 0 by 1     (55)          54           55        Abs err 1 exceeds tol 0 by 1     (56)          55           56        Abs err 1 exceeds tol 0 by 1     (57)          56           57        Abs err 1 exceeds tol 0 by 1     (58)          57           58        Abs err 1 exceeds tol 0 by 1     (59)          58           59        Abs err 1 exceeds tol 0 by 1     (60)          59           60        Abs err 1 exceeds tol 0 by 1     (61)          60           61        Abs err 1 exceeds tol 0 by 1     (62)          61           62        Abs err 1 exceeds tol 0 by 1     (63)          62           63        Abs err 1 exceeds tol 0 by 1
- general/num2str.m:233 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '-9007199254740992' '-9007199254740993'   Strings don't match
- general/num2str.m:234 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '-9007199254740992' '-9007199254740993'   Strings don't match
- general/num2str.m:267 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '2.1                  1e+23      3.141592653589793' '2.1  9.999999999999999e+22      3.141592653589793'   Strings don't match
- general/quadgk.m:623 [test] '__inline_ctor__' undefined
- general/rng.m:236 [test] assert () failed
- geometry/dsearchn.m:74 [assert] '__dsearchn__' undefined
- geometry/dsearchn.m:75 [assert] '__dsearchn__' undefined
- geometry/dsearchn.m:76 [assert] '__dsearchn__' undefined
- geometry/dsearchn.m:77 [assert] '__dsearchn__' undefined
- geometry/dsearchn.m:78 [assert] '__dsearchn__' undefined
- geometry/dsearchn.m:79 [assert] '__dsearchn__' undefined
- geometry/griddata.m:324 [test] '__dsearchn__' undefined
- geometry/griddata.m:346 [test] '__dsearchn__' undefined
- geometry/griddata3.m:113 [test] '__dsearchn__' undefined
- geometry/griddatan.m:171 [test] '__dsearchn__' undefined
- gui/getpixelposition.m:98 [test] '__get_position__' undefined
- gui/getpixelposition.m:114 [test] '__get_position__' undefined
- help/__unimplemented__.m:1317 [test] unable to open macros.texi for reading
- help/__unimplemented__.m:1321 [test] unable to open macros.texi for reading
- help/get_first_help_sentence.m:189 [assert] 'get_help_text' undefined
- help/get_first_help_sentence.m:192 [assert] 'get_help_text' undefined
- help/help.m:223 [assert] 'get_help_text' undefined
- help/help.m:229 [error] error ''get_help_text' undefined' does not match <'_! UNLIKELY_FCN! _' not found>
- help/type.m:139 [test] value on right hand side of assignment is undefined
- help/type.m:154 [assert] type: 'fftw' undefined
- help/which.m:103 [test] index (-7): out of bound; value -7 out of bound 0
- help/which.m:114 [test] index (-7): out of bound; value -7 out of bound 0
- help/which.m:126 [warning] expected a warning but none was issued
- image/colormap.m:187 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .         O(64x3)       E(3x3)      Dimensions don't match
- image/colormap.m:205 [test] max_recursion_depth exceeded
- image/gray2ind.m:112 [error] expected an error but none was raised
- image/image.m:238 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (2)           1            64        Abs err 63 exceeds tol 0 by 6e+01
- image/imformats.m:336 [test] '__magick_formats__' undefined
- image/imshow.m:277 [test] expected error <all MAP values must be in the range> but got <max_recursion_depth exceeded>
- image/ind2gray.m:90 [error] expected an error but none was raised
- image/ind2rgb.m:145 [error] expected an error but none was raised
- io/is_valid_file_id.m:54 [assert] assert () failed
- io/is_valid_file_id.m:55 [test] 'fid' undefined
- java/javaaddpath.m:184 [error] error 'The value cannot be an empty string. (Parameter 'path')' does not match <CLSPATH does not exist>
- legacy/textread.m:338 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason    (1,1)          0         0.81242      Abs err 0.81242 exceeds tol 0.01 by 0.8    (2,1)          0         0.89025      Abs err 0.89025 exceeds tol 0.01 by 0.9
- linear-algebra/expm.m:151 [assert] 'balance' undefined
- linear-algebra/expm.m:152 [assert] 'balance' undefined
- linear-algebra/funm.m:851 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x1)       E(1x3)      Dimensions don't match
- linear-algebra/funm.m:883 [test] 'balance' undefined
- linear-algebra/funm.m:936 [test] assert () failed
- linear-algebra/funm.m:950 [test] Invalid call to funm
- linear-algebra/funm.m:956 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()         7.3891       7.3891      Abs err 7.9936e-15 exceeds tol 2.2204e-16 by 8e-15
- linear-algebra/funm.m:969 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        2.718282     2.718282     Abs err 4.885e-15 exceeds tol 2.2204e-16 by 5e-15     (2)        7.389056     7.389056     Abs err 2.3093e-14 exceeds tol 2.2204e-16 by 2e-14     (3)        20.08554     20.08554     Abs err 1.0658e-14 exceeds tol 2.2204e-16 by 1e-14     (4)        54.59815     54.59815     Abs err 2.4158e-13 exceeds tol 2.2204e-16 by 2e-13     (5)        148.4132     148.4132     Abs err 5.6843e-14 exceeds tol 2.2204e-16 by 6e-14
- linear-algebra/funm.m:982 [test] 'balance' undefined
- linear-algebra/gls.m:221 [test] matrix power with non-integer exponent is not implemented yet
- linear-algebra/logm.m:185 [test] 'balance' undefined
- linear-algebra/logm.m:192 [assert] 'balance' undefined
- linear-algebra/logm.m:193 [test] 'balance' undefined
- linear-algebra/lscov.m:139 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)      -3482258.6346 -3482258.6346   Abs err 4.1812e-06 exceeds tol 3e-06 by 1e-06
- linear-algebra/lscov.m:203 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        1.247212        0         Abs err 1.2472 exceeds tol 0.2 by 1     (2)        98.99579       100        Abs err 1.0042 exceeds tol 0.2 by 0.8     (3)        199.0519       200        Abs err 0.94815 exceeds tol 0.2 by 0.7
- linear-algebra/normest1.m:385 [error] error 'normest1: X0 must have 5 columns' does not match <X0 must have 2 columns>
- linear-algebra/null.m:82 [test] Invalid call to svd
- linear-algebra/null.m:122 [assert] Invalid call to svd
- linear-algebra/ordeig.m:127 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (3)        -1.1473        0+1i       Abs err 1.5219 exceeds tol 0.0001 by 2     (4)        0.068588      -0-1i       Abs err 1.0023 exceeds tol 0.0001 by 1
- linear-algebra/rank.m:157 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           3            1         Abs err 2 exceeds tol 0 by 2
- linear-algebra/rref.m:126 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()           2            1         Abs err 1 exceeds tol 2e-08 by 1
- linear-algebra/tensorprod.m:414 [error] expected an error but none was raised
- miscellaneous/clearvars.m:221 [test] assert () failed
- miscellaneous/dir.m:198 [test] rmfield: first argument must be a struct
- miscellaneous/dir.m:283 [warning] expected a warning but none was issued
- miscellaneous/edit.m:582 [test] parse error near line 8, column 11: unexpected 'home'
- miscellaneous/fieldnames.m:89 [test] fieldnames: Invalid input argument
- miscellaneous/fullfile.m:186 [error] expected an error but none was raised
- miscellaneous/inputParser.m:937 [test] expected error <failed validation of 'line'> but got <binary operator '>' not implemented for 'struct' operations>
- miscellaneous/inputParser.m:1013 [error] error 'binary operator '>' not implemented for 'struct' operations' does not match <failed validation of 'a'.*ischar>
- miscellaneous/memoize.m:80 [test] assert () failed
- miscellaneous/memoize.m:88 [test] 'MemoizedFunction' object cannot be indexed with (
- miscellaneous/methods.m:152 [test] methods: class 'ftp' not found
- miscellaneous/mkdir.m:119 [test] assert () failed
- miscellaneous/nthargout.m:146 [error] error 'element number 2 undefined in return list' does not match <too many output arguments: 2>
- miscellaneous/publish.m:1137 [error] error 'exist: argument must be a string' does not match <NAME must be a string>
- miscellaneous/publish.m:1139 [error] error 'publish: FILE does not exist' does not match <only script files can be published>
- miscellaneous/run.m:121 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
- miscellaneous/symvar.m:61 [assert] '__inline_ctor__' undefined
- miscellaneous/symvar.m:62 [assert] '__inline_ctor__' undefined
- miscellaneous/symvar.m:63 [assert] '__inline_ctor__' undefined
- miscellaneous/tar.m:108 [test] assert () failed
- miscellaneous/unpack.m:403 [error] error 'unpack: FILETYPE must be given for a directory' does not match <FILETYPE must be a string>
- miscellaneous/unpack.m:415 [error] error 'unpack: FILETYPE must be given for a directory' does not match <FILETYPE must be "gz" for a directory>
- miscellaneous/verLessThan.m:81 [assert] assert () failed
- miscellaneous/verLessThan.m:82 [assert] assert () failed
- miscellaneous/version.m:135 [assert] '__blas_version__' undefined
- miscellaneous/version.m:136 [assert] '__lapack_version__' undefined
- miscellaneous/what.m:192 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(2x1)      E(10x1)      Dimensions don't match
- miscellaneous/zip.m:98 [test] zip: zip failed with exit status = 127
- optimization/fminbnd.m:344 [assert] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()       0.00098057     0.001       Abs err 1.9428e-05 exceeds tol 1.4901e-10 by 2e-05
- optimization/fminbnd.m:346 [assert] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.71354      0.71353      Abs err 1.0334e-05 exceeds tol 1.4901e-08 by 1e-05
- optimization/fsolve.m:693 [test] 'balance' undefined
- optimization/fzero.m:540 [assert] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()           0          1e-24       Abs err 1e-24 exceeds tol 2.2204e-38 by 1e-24
- optimization/optimget.m:87 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(1x1)      Dimensions don't match
- optimization/optimget.m:89 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(1x1)      Dimensions don't match
- optimization/optimget.m:99 [error] expected an error but none was raised
- optimization/optimset.m:229 [assert] assert () failed
- optimization/optimset.m:230 [assert] assert () failed
- optimization/optimset.m:231 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(1x1)      Dimensions don't match
- optimization/optimset.m:232 [test] invalid use of undefined value
- optimization/optimset.m:249 [warning] expected a warning but none was issued
- optimization/optimset.m:250 [error] expected an error but none was raised
- optimization/qp.m:485 [test] '__qp__' undefined
- optimization/sqp.m:781 [test] '__qp__' undefined
- path/savepath.m:211 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .        O(2067x1)      E(0x1)      Dimensions don't match
- plot/appearance/annotation.m:1479 [test] Object reference not set to an instance of an object.
- plot/appearance/annotation.m:1495 [test] set: argument must be a scalar, not empty
- plot/appearance/annotation.m:1511 [test] set: argument must be a scalar, not empty
- plot/appearance/annotation.m:1529 [test] Object reference not set to an instance of an object.
- plot/appearance/annotation.m:1552 [test] set: argument must be a scalar, not empty
- plot/appearance/annotation.m:1569 [test] set: argument must be a scalar, not empty
- plot/appearance/annotation.m:1592 [warning] set: argument must be a scalar, not empty
- plot/appearance/axis.m:621 [test] peaks: X and Y must be the same size
- plot/appearance/axis.m:633 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0            1         Abs err 1 exceeds tol 0 by 1     (2)           12           11        Abs err 1 exceeds tol 0 by 1     (3)           0            1         Abs err 1 exceeds tol 0 by 1     (4)           12           11        Abs err 1 exceeds tol 0 by 1
- plot/appearance/camlookat.m:284 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        -21.8951     -22.8063     Rel err 0.039955 exceeds tol 1e-07 by 0.04     (2)        -17.491      -24.5089     Rel err 0.28634 exceeds tol 1e-07 by 0.3     (3)        7.48587      16.8359      Rel err 0.55536 exceeds tol 1e-07 by 0.6
- plot/appearance/camroll.m:126 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)           1            0         Abs err 1 exceeds tol 2.2204e-16 by 1     (3)           0            1         Abs err 1 exceeds tol 2.2204e-16 by 1
- plot/appearance/datetick.m:101 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(8x1)       E(1x8)      Dimensions don't match
- plot/appearance/material.m:227 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x7)       E(1x1)      Dimensions don't match
- plot/appearance/orient.m:125 [test] parse error near line 12, column 19: unexpected 'portrait'
- plot/appearance/private/__gnuplot_legend__.m:1800 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(1x1)      Dimensions don't match
- plot/appearance/private/__gnuplot_legend__.m:1816 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x0)       E(1x2)      Dimensions don't match
- plot/appearance/rticklabels.m:171 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '1'          '2'        Strings don't match
- plot/appearance/text.m:346 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      'char row 1char row 2' 'cchhaarr  rrooww  12'   Strings don't match
- plot/appearance/text.m:390 [test] __go_text__: argument must be a string
- plot/appearance/text.m:450 [test] __go_text__: argument must be a string
- plot/appearance/thetaticklabels.m:163 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '20'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '40'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '60'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '80'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'         '100'       Strings don't match
- plot/appearance/xlim.m:135 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/ylim.m:136 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/zlim.m:136 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/draw/bar.m:234 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/bar.m:674 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.5        Abs err 0.5 exceeds tol 0 by 0.5
- plot/draw/bar.m:788 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/bar.m:869 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/bar.m:899 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/barh.m:257 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/barh.m:653 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.5        Abs err 0.5 exceeds tol 0 by 0.5
- plot/draw/barh.m:767 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/barh.m:848 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/barh.m:878 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Dimensions don't match
- plot/draw/camlight.m:266 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        -0.20812     -3.30121     Rel err 0.93696 exceeds tol 4.4409e-15 by 0.9     (2)        0.97557      15.4749      Rel err 0.93696 exceeds tol 4.4409e-15 by 0.9     (3)        0.070344     1.11583      Rel err 0.93696 exceeds tol 4.4409e-15 by 0.9
- plot/draw/colorbar.m:1000 [test] max_recursion_depth exceeded
- plot/draw/errorbar.m:250 [error] error 'index (_,1): out of bound 0 (dimensions are 0x0)' does not match <2 column errorplot is only valid for xerr>
- plot/draw/errorbar.m:251 [error] error 'errorbar: too many arguments to plot' does not match <6 columns only valid for xyerr and boxxy>
- plot/draw/errorbar.m:252 [error] error 'errorbar: too many arguments to plot' does not match <error plot requires 2, 3, 4, or 6 arguments>
- plot/draw/fplot.m:271 [test] '__inline_ctor__' undefined
- plot/draw/light.m:581 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.70711         1         Abs err 0.29289 exceeds tol 0 by 0.3     (3)        0.70711         1         Abs err 0.29289 exceeds tol 0 by 0.3
- plot/draw/light.m:593 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.26726         1         Abs err 0.73274 exceeds tol 0 by 0.7     (2)        0.53452         2         Abs err 1.4655 exceeds tol 0 by 1     (3)        0.80178         3         Abs err 2.1982 exceeds tol 0 by 2
- plot/draw/lightangle.m:146 [test] light: invalid number of PROPERTY/VALUE arguments
- plot/draw/line.m:123 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (2)        0.44706       0.447       Abs err 5.8824e-05 exceeds tol 0 by 6e-05     (3)        0.69804       0.741       Abs err 0.042961 exceeds tol 0 by 0.04
- plot/draw/patch.m:275 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(3x1)      Dimensions don't match
- plot/draw/patch.m:306 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           -0.1       Abs err 0.1 exceeds tol 0 by 0.1     (2)           1           1.9        Abs err 0.9 exceeds tol 0 by 0.9
- plot/draw/polar.m:659 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)        0.44706       0.447       Abs err 5.8824e-05 exceeds tol 2.2204e-16 by 6e-05     (3)        0.69804       0.741       Abs err 0.042961 exceeds tol 2.2204e-16 by 0.04
- plot/draw/smooth3.m:193 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []        'double'     'single'     Strings don't match
- plot/draw/smooth3.m:268 [error] error 'smooth3: SZ of the convolution kernel must consist of positive odd integers' does not match <STD_DEV must be a real>
- plot/draw/smooth3.m:269 [error] error 'smooth3: SZ of the convolution kernel must consist of positive odd integers' does not match <STD_DEV .* must be .* 3-element>
- plot/draw/stem.m:212 [test] Invalid call to nargin
- plot/draw/stem.m:229 [error] error 'Invalid call to nargin' does not match <can not define Z for 2-D stem plot>
- plot/draw/stem.m:230 [error] error 'Invalid call to nargin' does not match <Y must be a vector or 2-D array>
- plot/draw/stem.m:231 [error] error 'Invalid call to nargin' does not match <X and Y must be numeric>
- plot/draw/stem.m:232 [error] error 'Invalid call to nargin' does not match <X and Y must be numeric>
- plot/draw/stem.m:233 [error] error 'Invalid call to nargin' does not match <inconsistent sizes for X and Y>
- plot/draw/stem.m:234 [error] error 'Invalid call to nargin' does not match <inconsistent sizes for X and Y>
- plot/draw/stem.m:235 [error] error 'Invalid call to nargin' does not match <inconsistent sizes for X and Y>
- plot/draw/stem.m:236 [error] error 'Invalid call to nargin' does not match <No value specified for property "FOO">
- plot/draw/stem3.m:94 [error] error 'Invalid call to nargin' does not match <must define X, Y, and Z>
- plot/draw/stem3.m:95 [error] error 'Invalid call to nargin' does not match <X, Y, and Z must be numeric>
- plot/draw/stem3.m:96 [error] error 'Invalid call to nargin' does not match <X, Y, and Z must be numeric>
- plot/draw/stem3.m:97 [error] error 'Invalid call to nargin' does not match <X, Y, and Z must be numeric>
- plot/draw/stem3.m:98 [error] error 'Invalid call to nargin' does not match <inconsistent sizes for X, Y, and Z>
- plot/draw/stem3.m:99 [error] error 'Invalid call to nargin' does not match <inconsistent sizes for X, Y, and Z>
- plot/draw/stem3.m:100 [error] error 'Invalid call to nargin' does not match <inconsistent sizes for X, Y, and Z>
- plot/draw/stem3.m:101 [error] error 'Invalid call to nargin' does not match <No value specified for property "FOO">
- plot/draw/stream2.m:203 [test] '__streameuler2d__' undefined
- plot/draw/stream3.m:226 [test] '__streameuler3d__' undefined
- plot/draw/surfnorm.m:199 [error] error 'surfnorm: first argument must be axes handle' does not match <X, Y, and Z must be 2-D real matrices>
- plot/draw/surfnorm.m:200 [error] error 'surfnorm: first argument must be axes handle' does not match <X, Y, and Z must be 2-D real matrices>
- plot/draw/xline.m:775 [test] __go_text__: argument must be a string
- plot/draw/yline.m:736 [test] __go_text__: argument must be a string
- plot/util/axes.m:249 [test] max_recursion_depth exceeded
- plot/util/copyobj.m:228 [test] max_recursion_depth exceeded
- plot/util/findall.m:58 [test] Object reference not set to an instance of an object.
- plot/util/gnuplot_binary.m:68 [test] gnuplot_binary: NEW_PROG must be a non-empty string
- plot/util/hgload.m:114 [test] save: format '-binary' is not supported
- plot/util/linkaxes.m:122 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           1            0         Abs err 1 exceeds tol 0 by 1
- plot/util/openfig.m:130 [test] save: format '-binary' is not supported
- plot/util/openfig.m:150 [error] error 'save: format '-binary' is not supported' does not match <input argument 3 must be a string>
- plot/util/openfig.m:160 [error] error 'save: format '-binary' is not supported' does not match <unknown option 'foobar'>
- plot/util/printd.m:105 [test] 'mkstemp' undefined
- plot/util/savefig.m:135 [warning] expected a warning but none was issued
- plot/util/struct2hdl.m:793 [test] max_recursion_depth exceeded
- polynomial/poly.m:108 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
- polynomial/residue.m:340 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)          NaN      -0-0.092593i   'NaN' mismatch     (2)          NaN       0.22222+0i    'NaN' mismatch     (3)          NaN      0+0.092593i    'NaN' mismatch     (4)          NaN       0.22222+0i    'NaN' mismatch
- profiler/profile.m:123 [test] '__profiler_enable__' undefined
- set/powerset.m:103 [test] sort: argument must be a string
- set/powerset.m:108 [test] sort: argument must be a string
- set/unique.m:412 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .         O(32x1)      E(34x1)      Dimensions don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           16           34        Abs err 18 exceeds tol 0 by 2e+01     (2)           21           14        Abs err 7 exceeds tol 0 by 7     (3)           16           34        Abs err 18 exceeds tol 0 by 2e+01     (4)           9            16        Abs err 7 exceeds tol 0 by 7     (5)           18           30        Abs err 12 exceeds tol 0 by 1e+01     (6)           30           6         Abs err 24 exceeds tol 0 by 2e+01     (7)           16           34        Abs err 18 exceeds tol 0 by 2e+01     (8)           9            16        Abs err 7 exceeds tol 0 by 7     (9)           18           30        Abs err 12 exceeds tol 0 by 1e+01     (10)          30           6         Abs err 24 exceeds tol 0 by 2e+01     (11)          10           7         Abs err 3 exceeds tol 0 by 3     (12)          4            31        Abs err 27 exceeds tol 0 by 3e+01     (13)          17           28        Abs err 11 exceeds tol 0 by 1e+01     (14)          4            31        Abs err 27 exceeds tol 0 by 3e+01     (15)          20           12        Abs err 8 exceeds tol 0 by 8     (16)          27           18        Abs err 9 exceeds tol 0 by 9     (17)          24           8         Abs err 16 exceeds tol 0 by 2e+01     (18)          4            31        Abs err 27 exceeds tol 0 by 3e+01     (19)          20           12        Abs err 8 exceeds tol 0 by 8     (20)          27           18        Abs err 9 exceeds tol 0 by 9     (21)          24           8         Abs err 16 exceeds tol 0 by 2e+01     (22)          11           2         Abs err 9 exceeds tol 0 by 9     (23)          32           29        Abs err 3 exceeds tol 0 by 3     (24)          5            22        Abs err 17 exceeds tol 0 by 2e+01     (25)          32           29        Abs err 3 exceeds tol 0 by 3     (26)          15           1         Abs err 14 exceeds tol 0 by 1e+01     (27)          8            21        Abs err 13 exceeds tol 0 by 1e+01     (28)          7            10        Abs err 3 exceeds tol 0 by 3     (29)          32           29        Abs err 3 exceeds tol 0 by 3     (30)          15           1         Abs err 14 exceeds tol 0 by 1e+01     (31)          8            21        Abs err 13 exceeds tol 0 by 1e+01     (32)          7            10        Abs err 3 exceeds tol 0 by 3     (33)          22           9         Abs err 13 exceeds tol 0 by 1e+01     (34)          28           3         Abs err 25 exceeds tol 0 by 3e+01     (35)          29           11        Abs err 18 exceeds tol 0 by 2e+01     (36)          28           3         Abs err 25 exceeds tol 0 by 3e+01     (37)          22           23        Abs err 1 exceeds tol 0 by 1     (38)          3            27        Abs err 24 exceeds tol 0 by 2e+01     (39)          14           26        Abs err 12 exceeds tol 0 by 1e+01     (40)          28           3         Abs err 25 exceeds tol 0 by 3e+01     (41)          22           23        Abs err 1 exceeds tol 0 by 1     (42)          3            27        Abs err 24 exceeds tol 0 by 2e+01     (43)          14           26        Abs err 12 exceeds tol 0 by 1e+01     (44)          23           24        Abs err 1 exceeds tol 0 by 1     (45)          12           4         Abs err 8 exceeds tol 0 by 8     (46)          6            32        Abs err 26 exceeds tol 0 by 3e+01     (47)          12           4         Abs err 8 exceeds tol 0 by 8     (49)          6            20        Abs err 14 exceeds tol 0 by 1e+01     (50)          1            19        Abs err 18 exceeds tol 0 by 2e+01     (51)          12           4         Abs err 8 exceeds tol 0 by 8     (53)          6            20        Abs err 14 exceeds tol 0 by 1e+01     (54)          1            19        Abs err 18 exceeds tol 0 by 2e+01     (55)          26           33        Abs err 7 exceeds tol 0 by 7     (56)          2            13        Abs err 11 exceeds tol 0 by 1e+01     (57)          29           5         Abs err 24 exceeds tol 0 by 2e+01     (58)          2            13        Abs err 11 exceeds tol 0 by 1e+01     (59)          19           15        Abs err 4 exceeds tol 0 by 4     (60)          31           2         Abs err 29 exceeds tol 0 by 3e+01     (61)          13           24        Abs err 11 exceeds tol 0 by 1e+01     (62)          2            13        Abs err 11 exceeds tol 0 by 1e+01     (63)          19           15        Abs err 4 exceeds tol 0 by 4     (64)          31           2         Abs err 29 exceeds tol 0 by 3e+01     (65)          13           24        Abs err 11 exceeds tol 0 by 1e+01     (66)          21           17        Abs err 4 exceeds tol 0 by 4
- signal/movfun.m:1589 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (9)          NaN           0         'NaN' mismatch     (10)         NaN           0         'NaN' mismatch
- signal/spectral_adf.m:79 [error] error ''invalid_lw' undefined' does not match <unable to find function for @invalid_lw>
- signal/spectral_xdf.m:81 [error] error ''invalid_sw' undefined' does not match <unable to find function for @invalid_sw>
- sparse/bicg.m:470 [test] sparse: dimension mismatch
- sparse/bicgstab.m:455 [test] sparse: dimension mismatch
- sparse/cgs.m:420 [test] sparse: dimension mismatch
- sparse/ichol.m:289 [test] Exception of type 'System.OutOfMemoryException' was thrown.
- sparse/ichol.m:342 [test] Exception of type 'System.OutOfMemoryException' was thrown.
- sparse/ichol.m:351 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.019736      0.0195      Abs err 0.00023602 exceeds tol 0.0001 by 0.0001
- sparse/ichol.m:361 [error] expected an error but none was raised
- sparse/ichol.m:362 [error] expected an error but none was raised
- sparse/ichol.m:395 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.12362       0.2377      Abs err 0.11408 exceeds tol 0.0001 by 0.1
- sparse/ichol.m:415 [test] Exception of type 'System.OutOfMemoryException' was thrown.
- sparse/ichol.m:425 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()       1.1993e-16     0.2044      Abs err 0.2044 exceeds tol 0.0001 by 0.2
- sparse/ilu.m:284 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.26817        0.05       Abs err 0.21817 exceeds tol 0.01 by 0.2
- sparse/ilu.m:332 [shared] sparse: dimension mismatch
- sparse/ilu.m:348 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:352 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:356 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:365 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:369 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:373 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:384 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:390 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:396 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:404 [shared] sparse: dimension mismatch
- sparse/ilu.m:424 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:428 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:432 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:441 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:445 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:449 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:460 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:466 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:472 [test] ilu: A must be a sparse square matrix
- sparse/ilu.m:483 [test] ilu: encountered a pivot equal to 0
- sparse/ilu.m:506 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason    (3,2)          7            7         Abs err 8.8818e-16 exceeds tol 2.2204e-16 by 7e-16    (4,4)          1            1         Abs err 8.8818e-16 exceeds tol 2.2204e-16 by 7e-16
- sparse/qmr.m:315 [test] sparse: dimension mismatch
- sparse/sprand.m:66 [test] sparse: dimension mismatch
- sparse/sprand.m:72 [test] operator *: nonconformant arguments (op1 is 100x100, op2 is 200x200)
- sparse/sprand.m:93 [test] Array dimensions exceeded supported range.
- sparse/sprandn.m:66 [test] sparse: dimension mismatch
- sparse/sprandn.m:72 [test] operator *: nonconformant arguments (op1 is 100x100, op2 is 200x200)
- sparse/sprandn.m:92 [test] Array dimensions exceeded supported range.
- sparse/sprandsym.m:167 [test] sparse: dimension mismatch
- specfun/betaincinv.m:283 [assert] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()          0.01         0.01       Abs err 1.6482e-13 exceeds tol 5.5511e-14 by 1e-13
- specfun/cosint.m:176 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()        -Inf+3i      -Inf-3i      Abs err 6.2832 exceeds tol 0 by 6
- specfun/cosint.m:226 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          -Inf     0.83787+1.5708i   'Inf' mismatch     (3)          -Inf     2.4527+1.5708i   'Inf' mismatch
- specfun/cosint.m:235 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        -Inf+3i    0.83787-1.5708i   'Inf' mismatch     (2)        -Inf+3i    2.4527-1.5708i   'Inf' mismatch
- specfun/cosint.m:243 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (6)      -8.6331+3.1316i -8.6331+3.1416i   Rel err 0.0010885 exceeds tol 6.6613e-16 by 0.001
- specfun/expint.m:268 [assert] Object reference not set to an instance of an object.
- specfun/factor.m:201 [assert] '__pollardrho__' undefined
- specfun/factor.m:202 [assert] '__pollardrho__' undefined
- specfun/factor.m:209 [assert] '__pollardrho__' undefined
- specfun/factor.m:210 [assert] '__pollardrho__' undefined
- specfun/factorial.m:67 [assert] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()      1.1978571669968e+100 1.19785716699699e+100   Rel err 1.6202e-13 exceeds tol 2.8422e-14 by 1e-13
- specfun/gammainc.m:521 [assert] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.46249      0.46249      Rel err 1.8004e-15 exceeds tol 1.5543e-15 by 2e-16
- specfun/gammainc.m:552 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()      650986687.1916+211518396.654i 650986687.075+211518396.2911i   Rel err 5.5674e-10 exceeds tol 1e-13 by 6e-10
- specfun/isprime.m:197 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
- specfun/nchoosek.m:252 [test] assert () failed
- specfun/nchoosek.m:335 [warning] expected a warning but none was issued
- specfun/nchoosek.m:339 [warning] expected a warning but none was issued
- statistics/iqr.m:364 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)     E(0x0x1x2)    Dimensions don't match
- statistics/kendall.m:147 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != single
- statistics/mean.m:760 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/mean.m:790 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            -1        Abs err 1 exceeds tol 0 by 1
- statistics/median.m:835 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:837 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:856 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:862 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/mode.m:311 [test] sparse: dimension mismatch
- statistics/normalize.m:608 [test] assert () failed
- statistics/normalize.m:613 [test] assert () failed
- statistics/rmse.m:638 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()          Inf      4.7373373668443e+307   'Inf' mismatch
- statistics/var.m:904 [test] assert () failed
- statistics/var.m:908 [test] assert () failed
- strings/dec2base.m:287 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '0111111111111111111111111111111111111111111111111111111111111111' '1000000000000000000000000000000000000000000000000000000000000000'   Strings don't match
- strings/dec2base.m:307 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '8000000000000000' '7FFFFFFFFFFFFFFF'   Strings don't match
- strings/dec2bin.m:132 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '0111111111111111111111111111111111111111111111111111111111111111' '1000000000000000000000000000000000000000000000000000000000000000'   Strings don't match
- strings/dec2bin.m:134 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '0111111111111111111111111111111111111111111111111111111111111111' '1000000000000000000000000000000000000000000000000000000000000000'   Strings don't match
- strings/dec2bin.m:136 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '1000000000000000000000000000000000000000000000000000000000000000' '1000000000000000000000000000000000000000000000000000000000000001'   Strings don't match
- strings/dec2hex.m:118 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '7FFFFFFFFFFFFFFF' '8000000000000000'   Strings don't match
- strings/dec2hex.m:119 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '7FFFFFFFFFFFFFFF' '8000000000000000'   Strings don't match
- strings/dec2hex.m:120 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '8000000000000000' '8000000000000001'   Strings don't match
- strings/dec2hex.m:129 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      'FEDCBA9876543000' 'FEDCBA9876543210'   Strings don't match
- strings/isstrprop.m:157 [assert] ischar: wrong type argument 'cell array'
- strings/native2unicode.m:87 [assert] '__native2unicode__' undefined
- testfun/speed.m:430 [test] 'airy' undefined
- testfun/test.m:1084 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
