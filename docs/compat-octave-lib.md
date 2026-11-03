# GNU Octave compatibility report

Mode: Octave m-file library on path (/workspace/octave-src/scripts)

Files: 864, blocks passed 10013, failed 387, skipped 458, pass rate 96.3% (67 s)

| Area | Files | Pass | Fail | Skip | Rate |
|---|---:|---:|---:|---:|---:|
| +containers | 1 | 45 | 0 | 0 | 100.0% |
| +matlab | 4 | 48 | 0 | 0 | 100.0% |
| @ftp | 14 | 14 | 0 | 0 | 100.0% |
| audio | 32 | 68 | 1 | 28 | 98.6% |
| elfun | 27 | 83 | 0 | 0 | 100.0% |
| general | 67 | 1512 | 30 | 15 | 98.1% |
| geometry | 15 | 148 | 10 | 0 | 93.7% |
| gui | 26 | 89 | 2 | 8 | 97.8% |
| help | 16 | 37 | 11 | 1 | 77.1% |
| image | 50 | 531 | 14 | 24 | 97.4% |
| io | 7 | 40 | 2 | 3 | 95.2% |
| java | 4 | 14 | 1 | 14 | 93.3% |
| legacy | 9 | 140 | 4 | 0 | 97.2% |
| linear-algebra | 36 | 523 | 22 | 1 | 96.0% |
| miscellaneous | 92 | 603 | 28 | 18 | 95.6% |
| ode | 9 | 160 | 0 | 90 | 100.0% |
| optimization | 14 | 126 | 15 | 5 | 89.4% |
| path | 4 | 6 | 1 | 0 | 85.7% |
| plot | 197 | 913 | 132 | 13 | 87.4% |
| polynomial | 27 | 289 | 2 | 0 | 99.3% |
| prefs | 6 | 24 | 0 | 0 | 100.0% |
| profiler | 3 | 10 | 1 | 0 | 90.9% |
| set | 9 | 316 | 5 | 2 | 98.4% |
| signal | 21 | 502 | 3 | 9 | 99.4% |
| sparse | 29 | 248 | 42 | 198 | 85.5% |
| specfun | 21 | 419 | 19 | 0 | 95.7% |
| special-matrix | 11 | 193 | 0 | 0 | 100.0% |
| statistics | 49 | 1758 | 22 | 0 | 98.8% |
| strings | 34 | 494 | 13 | 26 | 97.4% |
| testfun | 13 | 265 | 7 | 2 | 97.4% |
| time | 14 | 377 | 0 | 1 | 100.0% |
| web | 3 | 18 | 0 | 0 | 100.0% |

## Failures

- audio/lin2mu.m:103 [error] error 'double: wrong type argument 'cell array'' does not match <invalid conversion>
- general/accumarray.m:452 [test] 'x' undefined
- general/bitcmp.m:124 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.61409010644959e+19 4.61168601842739e+18   Abs err 1.1529e+19 exceeds tol 0 by 1e+19
- general/blkdiag.m:214 [error] error 'vertical dimensions mismatch (1x2 vs 1x3)' does not match <arguments must be two-dimensional>
- general/cell2mat.m:138 [error] error 'vertical dimensions mismatch (1x1 vs 1x2)' does not match <dimension mismatch>
- general/cell2mat.m:139 [error] expected an error but none was raised
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
- general/structfun.m:158 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(1x3)      Structure sizes don't match
- general/structfun.m:181 [test] assert () failed
- general/structfun.m:197 [test] assert () failed
- general/xor.m:92 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           1            0         Abs err 1 exceeds tol 0 by 1     (2)           0            1         Abs err 1 exceeds tol 0 by 1
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
- image/hsv2rgb.m:134 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/hsv2rgb.m:135 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/hsv2rgb.m:136 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/image.m:238 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (2)           1            64        Abs err 63 exceeds tol 0 by 6e+01
- image/imformats.m:336 [test] '__magick_formats__' undefined
- image/imshow.m:277 [test] expected error <all MAP values must be in the range> but got <max_recursion_depth exceeded>
- image/ind2gray.m:90 [error] expected an error but none was raised
- image/ind2rgb.m:145 [error] expected an error but none was raised
- image/rgb2hsv.m:111 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/rgb2hsv.m:112 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/rgb2hsv.m:113 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- io/is_valid_file_id.m:54 [assert] assert () failed
- io/is_valid_file_id.m:55 [test] 'fid' undefined
- java/javaaddpath.m:184 [error] error 'The value cannot be an empty string. (Parameter 'path')' does not match <CLSPATH does not exist>
- legacy/textread.m:338 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason    (1,1)          0         0.035089     Abs err 0.035089 exceeds tol 0.01 by 0.03    (2,1)          0         0.21337      Abs err 0.21337 exceeds tol 0.01 by 0.2
- legacy/vectorize.m:71 [test] 'functions' undefined
- legacy/vectorize.m:77 [test] 'functions' undefined
- legacy/vectorize.m:83 [test] 'functions' undefined
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
- miscellaneous/tar.m:108 [test] The value cannot be an empty string. (Parameter 'path')
- miscellaneous/unpack.m:403 [error] error 'unpack: FILETYPE must be given for a directory' does not match <FILETYPE must be a string>
- miscellaneous/unpack.m:415 [error] error 'unpack: FILETYPE must be given for a directory' does not match <FILETYPE must be "gz" for a directory>
- miscellaneous/verLessThan.m:81 [assert] assert () failed
- miscellaneous/verLessThan.m:82 [assert] assert () failed
- miscellaneous/version.m:135 [assert] '__blas_version__' undefined
- miscellaneous/version.m:136 [assert] '__lapack_version__' undefined
- miscellaneous/what.m:192 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(2x1)      E(10x1)      Dimensions don't match
- miscellaneous/zip.m:98 [test] 'make_absolute_filename' undefined
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
- plot/appearance/axis.m:607 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (3)           1           -10        Abs err 11 exceeds tol 0 by 1e+01     (4)           10         -1e-05      Abs err 10 exceeds tol 0 by 1e+01
- plot/appearance/axis.m:621 [test] peaks: X and Y must be the same size
- plot/appearance/axis.m:633 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0            1         Abs err 1 exceeds tol 0 by 1     (2)           12           11        Abs err 1 exceeds tol 0 by 1     (3)           0            1         Abs err 1 exceeds tol 0 by 1     (4)           12           11        Abs err 1 exceeds tol 0 by 1
- plot/appearance/camlookat.m:215 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)          0.5          2.5        Abs err 2 exceeds tol 0 by 2     (2)          0.5           0         Abs err 0.5 exceeds tol 0 by 0.5     (3)          0.5           5         Abs err 4.5 exceeds tol 0 by 4
- plot/appearance/camlookat.m:284 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           -4        -22.8063     Rel err 0.82461 exceeds tol 1e-07 by 0.8     (2)           0         -24.5089     Rel err 1 exceeds tol 1e-07 by 1     (3)        52.1784      16.8359      Rel err 2.0992 exceeds tol 1e-07 by 2
- plot/appearance/camorbit.m:202 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)         17.143         0         Abs err 17.143 exceeds tol 2.2204e-16 by 2e+01     (1)        10.6829         10        Rel err 0.068287 exceeds tol 2.2204e-16 by 0.07     (3)        0.238901     17.3205      Rel err 0.98621 exceeds tol 2.2204e-16 by 1
- plot/appearance/camorbit.m:216 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)         3.0651     -0.724973     Rel err 5.2278 exceeds tol 5e-15 by 5     (2)          4.83       -9.37225     Rel err 1.5154 exceeds tol 5e-15 by 2     (3)         7.5475      14.5477      Rel err 0.48119 exceeds tol 5e-15 by 0.5
- plot/appearance/camorbit.m:232 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.81792     -0.215773     Rel err 4.7906 exceeds tol 2e-14 by 5     (2)         4.1518      -9.04927     Rel err 1.4588 exceeds tol 2e-14 by 1     (3)         8.346        14.767      Rel err 0.43482 exceeds tol 2e-14 by 0.4
- plot/appearance/camorbit.m:248 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        -3.7164      1.92212      Rel err 2.9335 exceeds tol 5e-15 by 3     (2)         3.5634      -6.48897     Rel err 1.5491 exceeds tol 5e-15 by 2     (3)         7.4162      15.9436      Rel err 0.53485 exceeds tol 5e-15 by 0.5
- plot/appearance/camorbit.m:268 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          0.5          0.5        Rel err 1.7764e-15 exceeds tol 4.4409e-16 by 1e-15
- plot/appearance/campos.m:145 [test] assert () failed
- plot/appearance/campos.m:185 [error] expected an error but none was raised
- plot/appearance/camroll.m:113 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.019112        0         Abs err 0.019112 exceeds tol 2.2204e-16 by 0.02     (2)        0.86483        0.5        Abs err 0.36483 exceeds tol 2.2204e-16 by 0.4     (3)        -0.5017      0.86603      Abs err 1.3677 exceeds tol 2.2204e-16 by 1
- plot/appearance/camroll.m:126 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)           1            0         Abs err 1 exceeds tol 2.2204e-16 by 1     (3)           0            1         Abs err 1 exceeds tol 2.2204e-16 by 1
- plot/appearance/camroll.m:153 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)          0.5        0.66028      Rel err 0.24274 exceeds tol 1e-05 by 0.2     (2)        0.86603      0.039151     Rel err 21.12 exceeds tol 1e-05 by 2e+01     (3)           0           0.75       Rel err 1 exceeds tol 1e-05 by 1
- plot/appearance/camtarget.m:156 [test] assert () failed
- plot/appearance/camtarget.m:196 [error] expected an error but none was raised
- plot/appearance/camup.m:152 [test] assert () failed
- plot/appearance/camup.m:192 [error] expected an error but none was raised
- plot/appearance/camva.m:135 [test] assert () failed
- plot/appearance/camva.m:175 [error] expected an error but none was raised
- plot/appearance/datetick.m:101 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(8x1)       E(1x8)      Dimensions don't match
- plot/appearance/material.m:227 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x7)       E(1x1)      Dimensions don't match
- plot/appearance/material.m:325 [error] error 'Invalid call to material' does not match <Invalid call to material.>
- plot/appearance/orient.m:125 [test] parse error near line 12, column 19: unexpected 'portrait'
- plot/appearance/private/__gnuplot_legend__.m:1800 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(1x1)      Dimensions don't match
- plot/appearance/private/__gnuplot_legend__.m:1816 [test] get: unknown axes property __autopos_tag__
- plot/appearance/private/__gnuplot_legend__.m:1833 [test] expected warning <plot data is empty> but got none
- plot/appearance/private/__gnuplot_legend__.m:1853 [test] expected warning <'best' not yet implemented> but got none
- plot/appearance/rticklabels.m:171 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '1'          '2'        Strings don't match
- plot/appearance/text.m:346 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      'char row 1char row 2' 'cchhaarr  rrooww  12'   Strings don't match
- plot/appearance/text.m:390 [test] __go_text__: argument must be a string
- plot/appearance/text.m:450 [test] __go_text__: argument must be a string
- plot/appearance/thetaticklabels.m:163 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '20'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '40'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '60'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'          '80'       Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '0'         '100'       Strings don't match
- plot/appearance/title.m:96 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .            O            E         Expected cell, but observed char
- plot/appearance/title.m:106 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/title.m:129 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []          '*'      'Liberation Serif'   Strings don't match
- plot/appearance/title.m:141 [test] title: invalid number of PROPERTY/VALUE arguments
- plot/appearance/xlabel.m:70 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (2)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (3)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1
- plot/appearance/xlabel.m:83 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/xlim.m:135 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/xticks.m:121 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'auto'      'manual'     Strings don't match
- plot/appearance/ylabel.m:72 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (2)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (3)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1
- plot/appearance/ylabel.m:85 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/ylim.m:136 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/yticks.m:123 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'auto'      'manual'     Strings don't match
- plot/appearance/zlabel.m:70 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (2)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (3)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1
- plot/appearance/zlabel.m:89 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/zlim.m:136 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/zticks.m:121 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'auto'      'manual'     Strings don't match
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
- plot/draw/loglog.m:103 [test] assert () failed
- plot/draw/patch.m:275 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(3x1)      Dimensions don't match
- plot/draw/patch.m:306 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           -0.1       Abs err 0.1 exceeds tol 0 by 0.1     (2)           1           1.9        Abs err 0.9 exceeds tol 0 by 0.9
- plot/draw/polar.m:659 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)        0.44706       0.447       Abs err 5.8824e-05 exceeds tol 2.2204e-16 by 6e-05     (3)        0.69804       0.741       Abs err 0.042961 exceeds tol 2.2204e-16 by 0.04
- plot/draw/semilogx.m:116 [test] assert () failed
- plot/draw/semilogy.m:116 [test] assert () failed
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
- plot/util/__next_line_color__.m:72 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           0            1         Abs err 1 exceeds tol 0 by 1     (2)        0.44706         0         Abs err 0.44706 exceeds tol 0 by 0.4     (3)        0.69804         0         Abs err 0.69804 exceeds tol 0 by 0.7  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.83529         0         Abs err 0.83529 exceeds tol 0 by 0.8     (2)        0.36863         1         Abs err 0.63137 exceeds tol 0 by 0.6  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)        0.61961         0         Abs err 0.61961 exceeds tol 0 by 0.6     (3)        0.45098         1         Abs err 0.54902 exceeds tol 0 by 0.5
- plot/util/__next_line_style__.m:69 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '-'          ':'        Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '-'          '--'       Strings don't match
- plot/util/axes.m:249 [test] max_recursion_depth exceeded
- plot/util/cla.m:94 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            2         Abs err 1 exceeds tol 0 by 1
- plot/util/clf.m:139 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []       'tabloid'    'usletter'    Strings don't match
- plot/util/close.m:171 [test] assert () failed
- plot/util/copyobj.m:228 [test] max_recursion_depth exceeded
- plot/util/findall.m:58 [test] Object reference not set to an instance of an object.
- plot/util/gnuplot_binary.m:68 [test] gnuplot_binary: NEW_PROG must be a non-empty string
- plot/util/hgload.m:114 [test] The input string '{' was not in a correct format.
- plot/util/isfigure.m:60 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x1)       E(1x2)      Dimensions don't match
- plot/util/ishold.m:69 [test] assert () failed
- plot/util/isprop.m:79 [test] assert () failed
- plot/util/linkaxes.m:122 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           1            0         Abs err 1 exceeds tol 0 by 1
- plot/util/linkprop.m:146 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (2)           1            0         Abs err 1 exceeds tol 0 by 1     (3)           0            1         Abs err 1 exceeds tol 0 by 1
- plot/util/newplot.m:254 [test] get: unknown axes property __autopos_tag__
- plot/util/openfig.m:130 [test] assert () failed
- plot/util/openfig.m:150 [error] expected an error but none was raised
- plot/util/openfig.m:160 [error] expected an error but none was raised
- plot/util/print.m:851 [error] error 'get: unknown axes property __autopos_tag__' does not match <a graphics handle>
- plot/util/printd.m:105 [test] 'mkstemp' undefined
- plot/util/savefig.m:135 [warning] expected a warning but none was issued
- plot/util/struct2hdl.m:793 [test] max_recursion_depth exceeded
- polynomial/poly.m:108 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
- polynomial/residue.m:340 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)          NaN      -0-0.092593i   'NaN' mismatch     (2)          NaN       0.22222+0i    'NaN' mismatch     (3)          NaN      0+0.092593i    'NaN' mismatch     (4)          NaN       0.22222+0i    'NaN' mismatch
- profiler/profile.m:123 [test] '__profiler_enable__' undefined
- set/powerset.m:103 [test] sort: argument must be a string
- set/powerset.m:108 [test] sort: argument must be a string
- set/unique.m:412 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .         O(31x1)      E(34x1)      Dimensions don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           30           34        Abs err 4 exceeds tol 0 by 4     (2)           24           14        Abs err 10 exceeds tol 0 by 1e+01     (3)           30           34        Abs err 4 exceeds tol 0 by 4     (4)           28           16        Abs err 12 exceeds tol 0 by 1e+01     (5)           16           30        Abs err 14 exceeds tol 0 by 1e+01     (6)           12           6         Abs err 6 exceeds tol 0 by 6     (7)           30           34        Abs err 4 exceeds tol 0 by 4     (8)           28           16        Abs err 12 exceeds tol 0 by 1e+01     (9)           16           30        Abs err 14 exceeds tol 0 by 1e+01     (10)          12           6         Abs err 6 exceeds tol 0 by 6     (11)          4            7         Abs err 3 exceeds tol 0 by 3     (12)          5            31        Abs err 26 exceeds tol 0 by 3e+01     (13)          15           28        Abs err 13 exceeds tol 0 by 1e+01     (14)          5            31        Abs err 26 exceeds tol 0 by 3e+01     (15)          29           12        Abs err 17 exceeds tol 0 by 2e+01     (16)          28           18        Abs err 10 exceeds tol 0 by 1e+01     (17)          23           8         Abs err 15 exceeds tol 0 by 2e+01     (18)          5            31        Abs err 26 exceeds tol 0 by 3e+01     (19)          29           12        Abs err 17 exceeds tol 0 by 2e+01     (20)          28           18        Abs err 10 exceeds tol 0 by 1e+01     (21)          23           8         Abs err 15 exceeds tol 0 by 2e+01     (22)          8            2         Abs err 6 exceeds tol 0 by 6     (23)          16           29        Abs err 13 exceeds tol 0 by 1e+01     (24)          25           22        Abs err 3 exceeds tol 0 by 3     (25)          16           29        Abs err 13 exceeds tol 0 by 1e+01     (26)          19           1         Abs err 18 exceeds tol 0 by 2e+01     (27)          11           21        Abs err 10 exceeds tol 0 by 1e+01     (28)          17           10        Abs err 7 exceeds tol 0 by 7     (29)          16           29        Abs err 13 exceeds tol 0 by 1e+01     (30)          19           1         Abs err 18 exceeds tol 0 by 2e+01     (31)          11           21        Abs err 10 exceeds tol 0 by 1e+01     (32)          17           10        Abs err 7 exceeds tol 0 by 7     (33)          11           9         Abs err 2 exceeds tol 0 by 2     (34)          1            3         Abs err 2 exceeds tol 0 by 2     (35)          26           11        Abs err 15 exceeds tol 0 by 2e+01     (36)          1            3         Abs err 2 exceeds tol 0 by 2     (37)          14           23        Abs err 9 exceeds tol 0 by 9     (38)          9            27        Abs err 18 exceeds tol 0 by 2e+01     (39)          27           26        Abs err 1 exceeds tol 0 by 1     (40)          1            3         Abs err 2 exceeds tol 0 by 2     (41)          14           23        Abs err 9 exceeds tol 0 by 9     (42)          9            27        Abs err 18 exceeds tol 0 by 2e+01     (43)          27           26        Abs err 1 exceeds tol 0 by 1     (44)          20           24        Abs err 4 exceeds tol 0 by 4     (45)          15           4         Abs err 11 exceeds tol 0 by 1e+01     (46)          6            32        Abs err 26 exceeds tol 0 by 3e+01     (47)          15           4         Abs err 11 exceeds tol 0 by 1e+01     (48)          7            25        Abs err 18 exceeds tol 0 by 2e+01     (49)          2            20        Abs err 18 exceeds tol 0 by 2e+01     (50)          13           19        Abs err 6 exceeds tol 0 by 6     (51)          15           4         Abs err 11 exceeds tol 0 by 1e+01     (52)          7            25        Abs err 18 exceeds tol 0 by 2e+01     (53)          2            20        Abs err 18 exceeds tol 0 by 2e+01     (54)          13           19        Abs err 6 exceeds tol 0 by 6     (55)          18           33        Abs err 15 exceeds tol 0 by 2e+01     (56)          31           13        Abs err 18 exceeds tol 0 by 2e+01     (57)          10           5         Abs err 5 exceeds tol 0 by 5     (58)          31           13        Abs err 18 exceeds tol 0 by 2e+01     (59)          22           15        Abs err 7 exceeds tol 0 by 7     (60)          3            2         Abs err 1 exceeds tol 0 by 1     (61)          20           24        Abs err 4 exceeds tol 0 by 4     (62)          31           13        Abs err 18 exceeds tol 0 by 2e+01     (63)          22           15        Abs err 7 exceeds tol 0 by 7     (64)          3            2         Abs err 1 exceeds tol 0 by 1     (65)          20           24        Abs err 4 exceeds tol 0 by 4     (66)          21           17        Abs err 4 exceeds tol 0 by 4
- set/uniquetol.m:317 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- set/uniquetol.m:318 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
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
- specfun/beta.m:79 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           -2          NaN        'NaN' mismatch     (2)           -3          NaN        'NaN' mismatch     (3)           -4          NaN        'NaN' mismatch     (4)           -5          NaN        'NaN' mismatch     (5)           -6          NaN        'NaN' mismatch     (6)           -7          NaN        'NaN' mismatch     (7)           -8          NaN        'NaN' mismatch     (8)           -9          NaN        'NaN' mismatch     (9)          -10          NaN        'NaN' mismatch
- specfun/beta.m:85 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           NaN        'NaN' mismatch     (2)           0           NaN        'NaN' mismatch     (5)           0           NaN        'NaN' mismatch     (6)           0           NaN        'NaN' mismatch
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
- statistics/iqr.m:390 [error] error 'Array dimensions exceeded supported range.' does not match <DIM must be .* positive integer>
- statistics/iqr.m:391 [error] error 'out of memory or dimension too large for Octave's index type' does not match <DIM must be .* positive integer>
- statistics/kendall.m:147 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != single
- statistics/mad.m:134 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != single
- statistics/mad.m:137 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != single
- statistics/mape.m:860 [error] expected an error but none was raised
- statistics/mean.m:760 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/mean.m:790 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            -1        Abs err 1 exceeds tol 0 by 1
- statistics/mean.m:1039 [error] expected an error but none was raised
- statistics/median.m:835 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:837 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:856 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:862 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/mode.m:311 [test] sparse: dimension mismatch
- statistics/normalize.m:608 [test] assert () failed
- statistics/normalize.m:613 [test] assert () failed
- statistics/rms.m:105 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != single
- statistics/rmse.m:638 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()          Inf      4.7373373668443e+307   'Inf' mismatch
- statistics/rmse.m:687 [error] expected an error but none was raised
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
- strings/strtok.m:224 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []       'beg\end'      'beg'       Strings don't match
- strings/strtrunc.m:73 [test] mat2cell: dimension vectors must add up to the size of the input (dim 1: 4 != 3)
- testfun/assert_equal.m:786 [error] error 'ASSERT errors for:  assert_equal (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x1)       E(0x1)      Dimensions don't match' does not match <Expected classdef object, but observed double>
- testfun/assert_equal.m:788 [error] error 'real: wrong type argument 'containers.Map'' does not match <Expected classdef object does not have an 'eq' method>
- testfun/assert_equal.m:791 [error] error 'real: wrong type argument 'containers.Map'' does not match <Expected classdef object does not have an 'eq' method>
- testfun/oruntests.m:197 [error] error ''dir_in_loadpath' undefined' does not match <DIRECTORY argument>
- testfun/rundemos.m:121 [error] error ''dir_in_loadpath' undefined' does not match <DIRECTORY argument>
- testfun/speed.m:430 [test] 'airy' undefined
- testfun/test.m:1084 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
