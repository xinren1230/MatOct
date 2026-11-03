# GNU Octave compatibility report

Mode: core builtins only (Octave m-files not on path)

Files: 864, blocks passed 9988, failed 366, skipped 458, pass rate 96.5% (101 s)

| Area | Files | Pass | Fail | Skip | Rate |
|---|---:|---:|---:|---:|---:|
| +containers | 1 | 45 | 0 | 0 | 100.0% |
| +matlab | 4 | 48 | 0 | 0 | 100.0% |
| @ftp | 14 | 14 | 0 | 0 | 100.0% |
| audio | 32 | 64 | 5 | 28 | 92.8% |
| elfun | 27 | 83 | 0 | 0 | 100.0% |
| general | 67 | 1509 | 32 | 15 | 97.9% |
| geometry | 15 | 158 | 0 | 0 | 100.0% |
| gui | 26 | 89 | 2 | 8 | 97.8% |
| help | 16 | 35 | 13 | 1 | 72.9% |
| image | 50 | 531 | 14 | 24 | 97.4% |
| io | 7 | 35 | 7 | 3 | 83.3% |
| java | 4 | 14 | 1 | 14 | 93.3% |
| legacy | 9 | 137 | 7 | 0 | 95.1% |
| linear-algebra | 36 | 531 | 14 | 1 | 97.4% |
| miscellaneous | 92 | 594 | 37 | 18 | 94.1% |
| ode | 9 | 160 | 0 | 90 | 100.0% |
| optimization | 14 | 139 | 2 | 5 | 98.6% |
| path | 4 | 6 | 1 | 0 | 85.7% |
| plot | 197 | 931 | 114 | 13 | 89.1% |
| polynomial | 27 | 289 | 2 | 0 | 99.3% |
| prefs | 6 | 24 | 0 | 0 | 100.0% |
| profiler | 3 | 10 | 1 | 0 | 90.9% |
| set | 9 | 316 | 5 | 2 | 98.4% |
| signal | 21 | 502 | 3 | 9 | 99.4% |
| sparse | 29 | 225 | 20 | 198 | 91.8% |
| specfun | 21 | 419 | 19 | 0 | 95.7% |
| special-matrix | 11 | 193 | 0 | 0 | 100.0% |
| statistics | 49 | 1745 | 35 | 0 | 98.0% |
| strings | 34 | 494 | 13 | 26 | 97.4% |
| testfun | 13 | 253 | 19 | 2 | 93.0% |
| time | 14 | 377 | 0 | 1 | 100.0% |
| web | 3 | 18 | 0 | 0 | 100.0% |

## Failures

- audio/@audioplayer/audioplayer.m:155 [error] error ''audioplayer' undefined' does not match <Y must be non-empty numeric data>
- audio/@audiorecorder/audiorecorder.m:94 [error] error ''audiorecorder' undefined' does not match <Invalid call>
- audio/@audiorecorder/audiorecorder.m:95 [error] error ''audiorecorder' undefined' does not match <Invalid call>
- audio/@audiorecorder/audiorecorder.m:96 [error] error ''audiorecorder' undefined' does not match <Invalid call>
- audio/lin2mu.m:103 [error] error 'double: wrong type argument 'cell array'' does not match <invalid conversion>
- general/accumarray.m:452 [test] 'x' undefined
- general/bitcmp.m:100 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != uint8
- general/bitcmp.m:108 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != uint16
- general/bitcmp.m:116 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != uint32
- general/bitcmp.m:124 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.61409010644959e+19 -4.61168601842739e+18   Abs err 2.0753e+19 exceeds tol 0 by 2e+19
- general/blkdiag.m:214 [error] error 'vertical dimensions mismatch (1x2 vs 1x3)' does not match <arguments must be two-dimensional>
- general/cell2mat.m:138 [error] error 'vertical dimensions mismatch (1x1 vs 1x2)' does not match <dimension mismatch>
- general/cell2mat.m:139 [error] expected an error but none was raised
- general/fliplr.m:77 [test] =: nonconformant arguments (op1 is 1x1x1, op2 is 1x4)
- general/integral.m:294 [assert] 'inline' undefined
- general/interp2.m:643 [test] interp2: XI, YI must have uniform spacing ('meshgrid' format)
- general/interp3.m:257 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)          NaN           0         'NaN' mismatch
- general/isequal.m:530 [assert] Arithmetic operation resulted in an overflow.
- general/isequal.m:531 [assert] Arithmetic operation resulted in an overflow.
- general/isequal.m:532 [assert] Arithmetic operation resulted in an overflow.
- general/isequal.m:533 [assert] Arithmetic operation resulted in an overflow.
- general/isequal.m:534 [assert] Arithmetic operation resulted in an overflow.
- general/isequal.m:535 [assert] Arithmetic operation resulted in an overflow.
- general/nextpow2.m:137 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (54)          53           54        Abs err 1 exceeds tol 0 by 1     (55)          54           55        Abs err 1 exceeds tol 0 by 1     (56)          55           56        Abs err 1 exceeds tol 0 by 1     (57)          56           57        Abs err 1 exceeds tol 0 by 1     (58)          57           58        Abs err 1 exceeds tol 0 by 1     (59)          58           59        Abs err 1 exceeds tol 0 by 1     (60)          59           60        Abs err 1 exceeds tol 0 by 1     (61)          60           61        Abs err 1 exceeds tol 0 by 1     (62)          61           62        Abs err 1 exceeds tol 0 by 1     (63)          62           63        Abs err 1 exceeds tol 0 by 1
- general/num2str.m:230 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'NaN'         'NA'       Strings don't match
- general/num2str.m:231 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []        'NaN+1i'     'NA+1i'      Strings don't match
- general/num2str.m:232 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []        '1+NaNi'     '1+NAi'      Strings don't match
- general/num2str.m:233 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '-9007199254740992' '-9007199254740993'   Strings don't match
- general/num2str.m:234 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '-9007199254740992' '-9007199254740993'   Strings don't match
- general/num2str.m:267 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []      '2.1                  1e+23      3.141592653589793' '2.1  9.999999999999999e+22      3.141592653589793'   Strings don't match
- general/quadgk.m:623 [test] 'inline' undefined
- general/structfun.m:132 [test] regexp: unknown option "matches"
- general/structfun.m:146 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(2x1)      E(2x1x3)     Dimensions don't match
- general/structfun.m:158 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .            O            E         Expected struct, but observed cell
- general/structfun.m:181 [test] assert () failed
- general/structfun.m:197 [test] assert () failed
- general/xor.m:92 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           1            0         Abs err 1 exceeds tol 0 by 1     (2)           0            1         Abs err 1 exceeds tol 0 by 1
- gui/getpixelposition.m:98 [test] '__get_position__' undefined
- gui/getpixelposition.m:114 [test] '__get_position__' undefined
- help/__unimplemented__.m:1317 [test] unable to open macros.texi for reading
- help/__unimplemented__.m:1321 [test] unable to open macros.texi for reading
- help/get_first_help_sentence.m:189 [assert] 'get_help_text' undefined
- help/get_first_help_sentence.m:192 [assert] 'get_help_text' undefined
- help/help.m:224 [assert] help: '.' not found
- help/type.m:139 [test] value on right hand side of assignment is undefined
- help/type.m:145 [test] type: couldn't open '<core>/ls.m' for reading
- help/type.m:150 [test] type: couldn't open '<core>/ls.m' for reading
- help/type.m:154 [assert] type: 'fftw' undefined
- help/which.m:100 [test] index (-6): out of bound; value -6 out of bound 11
- help/which.m:103 [test] index (-7): out of bound; value -7 out of bound 0
- help/which.m:114 [test] index (-7): out of bound; value -7 out of bound 0
- help/which.m:126 [warning] expected a warning but none was issued
- image/colormap.m:187 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .         O(64x3)       E(3x3)      Dimensions don't match
- image/colormap.m:205 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .         O(16x3)       E(3x3)      Dimensions don't match
- image/gray2ind.m:112 [error] expected an error but none was raised
- image/hsv2rgb.m:134 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/hsv2rgb.m:135 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/hsv2rgb.m:136 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/image.m:238 [test] axis: LIMITS vector must have 4, 6, or 8 elements
- image/imformats.m:336 [test] '__magick_formats__' undefined
- image/imshow.m:277 [test] expected error <argument number 2 is invalid> but got none
- image/ind2gray.m:90 [error] expected an error but none was raised
- image/ind2rgb.m:145 [error] expected an error but none was raised
- image/rgb2hsv.m:111 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/rgb2hsv.m:112 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- image/rgb2hsv.m:113 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- io/fileread.m:93 [test] fread: invalid SIZE specification
- io/importdata.m:462 [test] fread: invalid SIZE specification
- io/importdata.m:477 [test] fread: invalid SIZE specification
- io/importdata.m:565 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .            O            E         Expected struct, but observed double
- io/importdata.m:641 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           NaN        'NaN' mismatch     (2)           0           NaN        'NaN' mismatch
- io/is_valid_file_id.m:54 [assert] assert () failed
- io/is_valid_file_id.m:55 [test] 'fid' undefined
- java/javaaddpath.m:184 [error] error 'The value cannot be an empty string. (Parameter 'path')' does not match <CLSPATH does not exist>
- legacy/textread.m:338 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(4x1)       E(2x2)      Dimensions don't match
- legacy/textread.m:347 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(4x1)       E(1x4)      Dimensions don't match
- legacy/textread.m:359 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(4x1)       E(1x4)      Dimensions don't match
- legacy/textread.m:443 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(4x1)      Dimensions don't match
- legacy/vectorize.m:71 [test] 'functions' undefined
- legacy/vectorize.m:77 [test] 'functions' undefined
- legacy/vectorize.m:83 [test] 'functions' undefined
- linear-algebra/funm.m:851 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x1)       E(1x3)      Dimensions don't match
- linear-algebra/funm.m:883 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason    (1,1)       -0.46558     -0.46558     Abs err 6.6946e-12 exceeds tol 8.8818e-16 by 7e-12    (2,1)       -0.22264     -0.22264     Abs err 9.5991e-12 exceeds tol 8.8818e-16 by 1e-11    (1,2)       -0.14842     -0.14842     Abs err 6.3993e-12 exceeds tol 8.8818e-16 by 6e-12    (2,2)       -0.68822     -0.68822     Abs err 2.9045e-12 exceeds tol 8.8818e-16 by 3e-12
- linear-algebra/funm.m:936 [test] assert () failed
- linear-algebra/funm.m:950 [test] Invalid call to funm
- linear-algebra/funm.m:956 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()         7.3891       7.3891      Abs err 7.9936e-15 exceeds tol 2.2204e-16 by 8e-15
- linear-algebra/funm.m:969 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        2.718282     2.718282     Abs err 4.885e-15 exceeds tol 2.2204e-16 by 5e-15     (2)        7.389056     7.389056     Abs err 2.3093e-14 exceeds tol 2.2204e-16 by 2e-14     (3)        20.08554     20.08554     Abs err 1.0658e-14 exceeds tol 2.2204e-16 by 1e-14     (4)        54.59815     54.59815     Abs err 2.4158e-13 exceeds tol 2.2204e-16 by 2e-13     (5)        148.4132     148.4132     Abs err 5.6843e-14 exceeds tol 2.2204e-16 by 6e-14
- linear-algebra/gls.m:221 [test] matrix power with non-integer exponent is not implemented yet
- linear-algebra/logm.m:193 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason    (1,1)     0.251+1.0061e-14i    0.251       Abs err 1.0117e-14 exceeds tol 9.3259e-15 by 8e-16    (3,1)     -1.9769+1.4766e-14i   -1.9769      Abs err 1.7464e-14 exceeds tol 9.3259e-15 by 8e-15    (1,3)     -1.2252+1.1324e-14i   -1.2252      Abs err 1.2612e-14 exceeds tol 9.3259e-15 by 3e-15    (3,3)     -0.5831+1.3212e-14i   -0.5831      Abs err 1.4205e-14 exceeds tol 9.3259e-15 by 5e-15
- linear-algebra/lscov.m:139 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)      -3482258.6346 -3482258.6346   Abs err 4.1812e-06 exceeds tol 3e-06 by 1e-06
- linear-algebra/lscov.m:203 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        1.247212        0         Abs err 1.2472 exceeds tol 0.2 by 1     (2)        98.99579       100        Abs err 1.0042 exceeds tol 0.2 by 0.8     (3)        199.0519       200        Abs err 0.94815 exceeds tol 0.2 by 0.7
- linear-algebra/normest1.m:385 [error] error 'normest1: X0 must have 5 columns' does not match <X0 must have 2 columns>
- linear-algebra/rank.m:157 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           3            1         Abs err 2 exceeds tol 0 by 2
- linear-algebra/rref.m:126 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()           2            1         Abs err 1 exceeds tol 2e-08 by 1
- linear-algebra/tensorprod.m:414 [error] expected an error but none was raised
- miscellaneous/clearvars.m:221 [test] assert () failed
- miscellaneous/dir.m:198 [test] rmfield: first argument must be a struct
- miscellaneous/dir.m:283 [warning] expected a warning but none was issued
- miscellaneous/edit.m:582 [test] parse error near line 8, column 11: unexpected 'home'
- miscellaneous/fieldnames.m:89 [test] fieldnames: Invalid input argument
- miscellaneous/fieldnames.m:95 [test] 'ftp' undefined
- miscellaneous/fullfile.m:186 [error] expected an error but none was raised
- miscellaneous/getfield.m:64 [test] merge: wrong type argument 'cell array'
- miscellaneous/inputParser.m:937 [test] expected error <failed validation of 'line'> but got <binary operator '>' not implemented for 'struct' operations>
- miscellaneous/inputParser.m:1013 [error] error 'binary operator '>' not implemented for 'struct' operations' does not match <failed validation of 'a'.*ischar>
- miscellaneous/isfile.m:59 [assert] assert () failed
- miscellaneous/isfile.m:61 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0            1         Abs err 1 exceeds tol 0 by 1
- miscellaneous/isfile.m:62 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0            1         Abs err 1 exceeds tol 0 by 1
- miscellaneous/ismethod.m:58 [assert] 'ftp' undefined
- miscellaneous/memoize.m:80 [test] assert () failed
- miscellaneous/memoize.m:88 [test] 'MemoizedFunction' object cannot be indexed with (
- miscellaneous/methods.m:152 [test] methods: class 'ftp' not found
- miscellaneous/mkdir.m:119 [test] assert () failed
- miscellaneous/nthargout.m:146 [error] error 'element number 2 undefined in return list' does not match <too many output arguments: 2>
- miscellaneous/orderfields.m:188 [test] struct2cell: only scalar structs are supported
- miscellaneous/orderfields.m:207 [test] struct2cell: only scalar structs are supported
- miscellaneous/publish.m:1137 [error] error 'exist: argument must be a string' does not match <NAME must be a string>
- miscellaneous/publish.m:1139 [error] error 'publish: FILE does not exist' does not match <only script files can be published>
- miscellaneous/run.m:121 [test] 'is_same_file' undefined
- miscellaneous/setfield.m:144 [test] merge: wrong type argument 'cell array'
- miscellaneous/symvar.m:61 [assert] 'inline' undefined
- miscellaneous/symvar.m:62 [assert] 'inline' undefined
- miscellaneous/symvar.m:63 [assert] 'inline' undefined
- miscellaneous/tar.m:108 [test] The value cannot be an empty string. (Parameter 'path')
- miscellaneous/unpack.m:403 [error] error 'unpack: FILETYPE must be given for a directory' does not match <FILETYPE must be a string>
- miscellaneous/unpack.m:415 [error] error 'unpack: FILETYPE must be given for a directory' does not match <FILETYPE must be "gz" for a directory>
- miscellaneous/verLessThan.m:81 [assert] assert () failed
- miscellaneous/verLessThan.m:82 [assert] assert () failed
- miscellaneous/version.m:135 [assert] '__blas_version__' undefined
- miscellaneous/version.m:136 [assert] '__lapack_version__' undefined
- miscellaneous/what.m:192 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(2x1)      E(10x1)      Dimensions don't match
- miscellaneous/zip.m:98 [test] 'make_absolute_filename' undefined
- optimization/qp.m:485 [test] '__qp__' undefined
- optimization/sqp.m:781 [test] '__qp__' undefined
- path/savepath.m:211 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .         O(128x1)      E(0x1)      Dimensions don't match
- plot/appearance/annotation.m:1479 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x1)       E(1x1)      Dimensions don't match
- plot/appearance/annotation.m:1495 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .                      'Hello!'     Expected string, but observed number
- plot/appearance/annotation.m:1511 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .                      'Hello!'     Expected string, but observed number
- plot/appearance/annotation.m:1529 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x1)       E(4x1)      Dimensions don't match
- plot/appearance/annotation.m:1552 [test] set: unknown hggroup property units
- plot/appearance/annotation.m:1569 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           2            1         Abs err 1 exceeds tol 0 by 1
- plot/appearance/axis.m:607 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (3)           1           -10        Abs err 11 exceeds tol 0 by 1e+01     (4)           10         -1e-05      Abs err 10 exceeds tol 0 by 1e+01
- plot/appearance/axis.m:621 [test] peaks: X and Y must be the same size
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
- plot/appearance/legend.m:2227 [test] element number 2 undefined in return list
- plot/appearance/legend.m:2242 [test] element number 2 undefined in return list
- plot/appearance/lighting.m:184 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'none'       'flat'      Strings don't match
- plot/appearance/material.m:227 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x7)       E(1x1)      Dimensions don't match
- plot/appearance/material.m:325 [error] error 'Invalid call to material' does not match <Invalid call to material.>
- plot/appearance/orient.m:125 [test] parse error near line 12, column 19: unexpected 'portrait'
- plot/appearance/private/__gnuplot_legend__.m:1800 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(1x1)      Dimensions don't match
- plot/appearance/private/__gnuplot_legend__.m:1816 [test] get: unknown axes property __autopos_tag__
- plot/appearance/private/__gnuplot_legend__.m:1833 [test] expected warning <plot data is empty> but got none
- plot/appearance/private/__gnuplot_legend__.m:1853 [test] expected warning <'best' not yet implemented> but got none
- plot/appearance/rticklabels.m:171 [test] rticklabels: rticklabels can only be used on a polar plot
- plot/appearance/shading.m:265 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'none'      'interp'     Strings don't match
- plot/appearance/thetaticklabels.m:163 [test] thetaticklabels: function can only be used on a polar plot
- plot/appearance/title.m:96 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .            O            E         Expected cell, but observed char
- plot/appearance/title.m:106 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/title.m:129 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []          '*'      'Liberation Serif'   Strings don't match
- plot/appearance/title.m:141 [test] title: invalid number of PROPERTY/VALUE arguments
- plot/appearance/xlabel.m:70 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (2)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (3)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1
- plot/appearance/xlabel.m:83 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/xlim.m:135 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/xtickangle.m:82 [test] set: unknown axes property xticklabelrotation
- plot/appearance/xtickangle.m:100 [error] error 'isnan: wrong type argument 'cell array'' does not match <ANGLE must be .* numeric>
- plot/appearance/xticks.m:121 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'auto'      'manual'     Strings don't match
- plot/appearance/ylabel.m:72 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (2)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (3)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1
- plot/appearance/ylabel.m:85 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/ylim.m:136 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/ytickangle.m:82 [test] set: unknown axes property yticklabelrotation
- plot/appearance/ytickangle.m:100 [error] error 'isnan: wrong type argument 'cell array'' does not match <ANGLE must be .* numeric>
- plot/appearance/yticks.m:123 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'auto'      'manual'     Strings don't match
- plot/appearance/zlabel.m:70 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (2)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1     (3)           0           0.15       Abs err 0.15 exceeds tol 0 by 0.1
- plot/appearance/zlabel.m:89 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           11           15        Abs err 4 exceeds tol 0 by 4
- plot/appearance/zlim.m:136 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)          1.2          1.4        Abs err 0.2 exceeds tol 2.2204e-16 by 0.2
- plot/appearance/ztickangle.m:82 [test] set: unknown axes property zticklabelrotation
- plot/appearance/ztickangle.m:100 [error] error 'isnan: wrong type argument 'cell array'' does not match <ANGLE must be .* numeric>
- plot/appearance/zticks.m:121 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []         'auto'      'manual'     Strings don't match
- plot/draw/camlight.m:266 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.66446      -3.30121     Rel err 1.2013 exceeds tol 4.4409e-15 by 1     (2)        -0.66446     15.4749      Rel err 1.0429 exceeds tol 4.4409e-15 by 1     (3)        0.34202      1.11583      Rel err 0.69348 exceeds tol 4.4409e-15 by 0.7
- plot/draw/contour.m:112 [test] set: unknown hggroup property levellist
- plot/draw/contour.m:126 [test] set: unknown hggroup property levelstep
- plot/draw/ezplot.m:118 [test] axis: LIMITS vector must have 4, 6, or 8 elements
- plot/draw/fplot.m:271 [test] 'inline' undefined
- plot/draw/fplot.m:294 [test] axis: LIMITS vector must have 4, 6, or 8 elements
- plot/draw/fplot.m:305 [test] plot: invalid line specification '+;mysin;'
- plot/draw/light.m:581 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.70711         1         Abs err 0.29289 exceeds tol 0 by 0.3     (3)        0.70711         1         Abs err 0.29289 exceeds tol 0 by 0.3
- plot/draw/light.m:593 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.26726         1         Abs err 0.73274 exceeds tol 0 by 0.7     (2)        0.53452         2         Abs err 1.4655 exceeds tol 0 by 1     (3)        0.80178         3         Abs err 2.1982 exceeds tol 0 by 2
- plot/draw/lightangle.m:146 [test] light: invalid number of PROPERTY/VALUE arguments
- plot/draw/line.m:123 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (2)        0.44706       0.447       Abs err 5.8824e-05 exceeds tol 0 by 6e-05     (3)        0.69804       0.741       Abs err 0.042961 exceeds tol 0 by 0.04
- plot/draw/loglog.m:103 [test] assert () failed
- plot/draw/patch.m:275 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x0)       E(3x1)      Dimensions don't match
- plot/draw/patch.m:306 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           0           -0.1       Abs err 0.1 exceeds tol 0 by 0.1     (2)           1           1.9        Abs err 0.9 exceeds tol 0 by 0.9
- plot/draw/patch.m:320 [error] expected an error but none was raised
- plot/draw/polar.m:617 [test] assert () failed
- plot/draw/polar.m:659 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)        0.44706       0.447       Abs err 5.8824e-05 exceeds tol 2.2204e-16 by 6e-05     (3)        0.69804       0.741       Abs err 0.042961 exceeds tol 2.2204e-16 by 0.04
- plot/draw/polar.m:689 [test] expected error <first argument must be axes handle> but got <polar: FMT argument must be a string>
- plot/draw/semilogx.m:116 [test] assert () failed
- plot/draw/semilogy.m:116 [test] assert () failed
- plot/draw/stem.m:212 [test] get: unknown line property baseline
- plot/util/__next_line_color__.m:72 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           0            1         Abs err 1 exceeds tol 0 by 1     (2)        0.44706         0         Abs err 0.44706 exceeds tol 0 by 0.4     (3)        0.69804         0         Abs err 0.69804 exceeds tol 0 by 0.7  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)        0.83529         0         Abs err 0.83529 exceeds tol 0 by 0.8     (2)        0.36863         1         Abs err 0.63137 exceeds tol 0 by 0.6  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (2)        0.61961         0         Abs err 0.61961 exceeds tol 0 by 0.6     (3)        0.45098         1         Abs err 0.54902 exceeds tol 0 by 0.5
- plot/util/__next_line_style__.m:69 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '-'          ':'        Strings don't match  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      []          '-'          '--'       Strings don't match
- plot/util/axes.m:249 [test] assert () failed
- plot/util/cla.m:94 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            2         Abs err 1 exceeds tol 0 by 1
- plot/util/clf.m:139 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      []       'tabloid'    'usletter'    Strings don't match
- plot/util/close.m:171 [test] assert () failed
- plot/util/colstyle.m:78 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .         1  0  0        'r'        Expected string, but observed number
- plot/util/copyobj.m:228 [test] set: "currentpoint" is read-only
- plot/util/findall.m:58 [test] Object reference not set to an instance of an object.
- plot/util/gnuplot_binary.m:68 [test] gnuplot_binary: NEW_PROG must be a non-empty string
- plot/util/hgload.m:114 [test] The input string '{' was not in a correct format.
- plot/util/hold.m:171 [test] patch: invalid color specification C
- plot/util/hold.m:187 [test] patch: invalid color specification C
- plot/util/isfigure.m:60 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x1)       E(1x2)      Dimensions don't match
- plot/util/isgraphics.m:57 [test] axis: LIMITS vector must have 4, 6, or 8 elements
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
- plot/util/struct2hdl.m:793 [test] set: invalid color specification
- plot/util/subplot.m:621 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           1            0         Abs err 1 exceeds tol 0 by 1     (2)           1            0         Abs err 1 exceeds tol 0 by 1     (3)           1            0         Abs err 1 exceeds tol 0 by 1     (4)           1            0         Abs err 1 exceeds tol 0 by 1     (5)           1            0         Abs err 1 exceeds tol 0 by 1     (6)           1            0         Abs err 1 exceeds tol 0 by 1
- plot/util/subplot.m:636 [test] Invalid call to subplot
- plot/util/subplot.m:652 [test] Invalid call to subplot
- plot/util/subplot.m:664 [test] assert () failed
- polynomial/poly.m:108 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
- polynomial/residue.m:340 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)          NaN      -0-0.092593i   'NaN' mismatch     (2)          NaN       0.22222+0i    'NaN' mismatch     (3)          NaN      0+0.092593i    'NaN' mismatch     (4)          NaN       0.22222+0i    'NaN' mismatch
- profiler/profile.m:123 [test] '__profiler_enable__' undefined
- set/powerset.m:103 [test] sort: argument must be a string
- set/powerset.m:108 [test] sort: argument must be a string
- set/unique.m:412 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           23           26        Abs err 3 exceeds tol 0 by 3     (2)           38           22        Abs err 16 exceeds tol 0 by 2e+01     (3)           4            34        Abs err 30 exceeds tol 0 by 3e+01     (4)           37           45        Abs err 8 exceeds tol 0 by 8     (5)           17           57        Abs err 40 exceeds tol 0 by 4e+01     (6)           56           6         Abs err 50 exceeds tol 0 by 5e+01     (7)           16           11        Abs err 5 exceeds tol 0 by 5     (8)           2            17        Abs err 15 exceeds tol 0 by 2e+01     (9)           48           33        Abs err 15 exceeds tol 0 by 2e+01     (10)          44           28        Abs err 16 exceeds tol 0 by 2e+01     (11)          6            35        Abs err 29 exceeds tol 0 by 3e+01     (12)          13           15        Abs err 2 exceeds tol 0 by 2     (13)          24           56        Abs err 32 exceeds tol 0 by 3e+01     (14)          15           2         Abs err 13 exceeds tol 0 by 1e+01     (15)          22           59        Abs err 37 exceeds tol 0 by 4e+01     (16)          57           4         Abs err 53 exceeds tol 0 by 5e+01     (17)          28           66        Abs err 38 exceeds tol 0 by 4e+01     (18)          11           16        Abs err 5 exceeds tol 0 by 5     (19)          49           50        Abs err 1 exceeds tol 0 by 1     (20)          50           49        Abs err 1 exceeds tol 0 by 1     (21)          1            27        Abs err 26 exceeds tol 0 by 3e+01     (22)          59           24        Abs err 35 exceeds tol 0 by 4e+01     (23)          27           37        Abs err 10 exceeds tol 0 by 1e+01     (24)          12           44        Abs err 32 exceeds tol 0 by 3e+01     (25)          33           48        Abs err 15 exceeds tol 0 by 2e+01     (26)          46           39        Abs err 7 exceeds tol 0 by 7     (27)          35           38        Abs err 3 exceeds tol 0 by 3     (28)          26           13        Abs err 13 exceeds tol 0 by 1e+01     (29)          60           23        Abs err 37 exceeds tol 0 by 4e+01     (30)          45           5         Abs err 40 exceeds tol 0 by 4e+01     (31)          34           12        Abs err 22 exceeds tol 0 by 2e+01     (32)          5            46        Abs err 41 exceeds tol 0 by 4e+01     (34)          66           1         Abs err 65 exceeds tol 0 by 7e+01  ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason     (1)           21           34        Abs err 13 exceeds tol 0 by 1e+01     (2)           8            14        Abs err 6 exceeds tol 0 by 6     (3)           21           34        Abs err 13 exceeds tol 0 by 1e+01     (4)           3            16        Abs err 13 exceeds tol 0 by 1e+01     (5)           32           30        Abs err 2 exceeds tol 0 by 2     (6)           11           6         Abs err 5 exceeds tol 0 by 5     (7)           21           34        Abs err 13 exceeds tol 0 by 1e+01     (8)           3            16        Abs err 13 exceeds tol 0 by 1e+01     (9)           32           30        Abs err 2 exceeds tol 0 by 2     (10)          11           6         Abs err 5 exceeds tol 0 by 5     (11)          18           7         Abs err 11 exceeds tol 0 by 1e+01     (12)          24           31        Abs err 7 exceeds tol 0 by 7     (13)          12           28        Abs err 16 exceeds tol 0 by 2e+01     (14)          24           31        Abs err 7 exceeds tol 0 by 7     (15)          14           12        Abs err 2 exceeds tol 0 by 2     (16)          7            18        Abs err 11 exceeds tol 0 by 1e+01     (17)          5            8         Abs err 3 exceeds tol 0 by 3     (18)          24           31        Abs err 7 exceeds tol 0 by 7     (19)          14           12        Abs err 2 exceeds tol 0 by 2     (20)          7            18        Abs err 11 exceeds tol 0 by 1e+01     (21)          5            8         Abs err 3 exceeds tol 0 by 3     (22)          15           2         Abs err 13 exceeds tol 0 by 1e+01     (23)          1            29        Abs err 28 exceeds tol 0 by 3e+01     (24)          13           22        Abs err 9 exceeds tol 0 by 9     (25)          1            29        Abs err 28 exceeds tol 0 by 3e+01     (26)          28           1         Abs err 27 exceeds tol 0 by 3e+01     (27)          23           21        Abs err 2 exceeds tol 0 by 2     (28)          17           10        Abs err 7 exceeds tol 0 by 7     (29)          1            29        Abs err 28 exceeds tol 0 by 3e+01     (30)          28           1         Abs err 27 exceeds tol 0 by 3e+01     (31)          23           21        Abs err 2 exceeds tol 0 by 2     (32)          17           10        Abs err 7 exceeds tol 0 by 7     (33)          25           9         Abs err 16 exceeds tol 0 by 2e+01     (34)          31           3         Abs err 28 exceeds tol 0 by 3e+01     (35)          27           11        Abs err 16 exceeds tol 0 by 2e+01     (36)          31           3         Abs err 28 exceeds tol 0 by 3e+01     (37)          4            23        Abs err 19 exceeds tol 0 by 2e+01     (38)          2            27        Abs err 25 exceeds tol 0 by 3e+01     (39)          28           26        Abs err 2 exceeds tol 0 by 2     (40)          31           3         Abs err 28 exceeds tol 0 by 3e+01     (41)          4            23        Abs err 19 exceeds tol 0 by 2e+01     (42)          2            27        Abs err 25 exceeds tol 0 by 3e+01     (43)          28           26        Abs err 2 exceeds tol 0 by 2     (44)          10           24        Abs err 14 exceeds tol 0 by 1e+01     (45)          30           4         Abs err 26 exceeds tol 0 by 3e+01     (46)          26           32        Abs err 6 exceeds tol 0 by 6     (47)          30           4         Abs err 26 exceeds tol 0 by 3e+01     (48)          9            25        Abs err 16 exceeds tol 0 by 2e+01     (49)          19           20        Abs err 1 exceeds tol 0 by 1     (50)          20           19        Abs err 1 exceeds tol 0 by 1     (51)          30           4         Abs err 26 exceeds tol 0 by 3e+01     (52)          9            25        Abs err 16 exceeds tol 0 by 2e+01     (53)          19           20        Abs err 1 exceeds tol 0 by 1     (54)          20           19        Abs err 1 exceeds tol 0 by 1     (56)          6            13        Abs err 7 exceeds tol 0 by 7     (57)          16           5         Abs err 11 exceeds tol 0 by 1e+01     (58)          6            13        Abs err 7 exceeds tol 0 by 7     (59)          22           15        Abs err 7 exceeds tol 0 by 7     (60)          29           2         Abs err 27 exceeds tol 0 by 3e+01     (61)          20           24        Abs err 4 exceeds tol 0 by 4     (62)          6            13        Abs err 7 exceeds tol 0 by 7     (63)          22           15        Abs err 7 exceeds tol 0 by 7     (64)          29           2         Abs err 27 exceeds tol 0 by 3e+01     (65)          20           24        Abs err 4 exceeds tol 0 by 4     (66)          34           17        Abs err 17 exceeds tol 0 by 2e+01
- set/uniquetol.m:317 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- set/uniquetol.m:318 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- signal/movfun.m:1589 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (9)          NaN           0         'NaN' mismatch     (10)         NaN           0         'NaN' mismatch
- signal/spectral_adf.m:79 [error] error ''invalid_lw' undefined' does not match <unable to find function for @invalid_lw>
- signal/spectral_xdf.m:81 [error] error ''invalid_sw' undefined' does not match <unable to find function for @invalid_sw>
- sparse/bicg.m:479 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            2         Abs err 1 exceeds tol 0 by 1
- sparse/bicgstab.m:478 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           4            2         Abs err 2 exceeds tol 0 by 2
- sparse/cgs.m:427 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            2         Abs err 1 exceeds tol 0 by 1
- sparse/eigs.m:1435 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)           5            1         Abs err 4 exceeds tol 0 by 4     (2)           1            4         Abs err 3 exceeds tol 0 by 3     (3)           4            5         Abs err 1 exceeds tol 0 by 1
- sparse/gmres.m:582 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            2         Abs err 1 exceeds tol 0 by 1
- sparse/ichol.m:289 [test] Exception of type 'System.OutOfMemoryException' was thrown.
- sparse/ichol.m:342 [test] Exception of type 'System.OutOfMemoryException' was thrown.
- sparse/ichol.m:351 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.019736      0.0195      Abs err 0.00023602 exceeds tol 0.0001 by 0.0001
- sparse/ichol.m:361 [error] expected an error but none was raised
- sparse/ichol.m:362 [error] expected an error but none was raised
- sparse/ichol.m:395 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.12362       0.2377      Abs err 0.11408 exceeds tol 0.0001 by 0.1
- sparse/ichol.m:415 [test] Invalid call to tril
- sparse/ichol.m:425 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()       1.1993e-16     0.2044      Abs err 0.2044 exceeds tol 0.0001 by 0.2
- sparse/ilu.m:284 [test] ASSERT errors for:  assert (,,)    Location  |  Observed  |  Expected  |  Reason      ()        0.26817        0.05       Abs err 0.21817 exceeds tol 0.01 by 0.2
- sparse/ilu.m:356 [test] timeout
- sparse/pcg.m:629 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            2         Abs err 1 exceeds tol 0 by 1
- sparse/sprand.m:72 [test] sprand: RC must be a scalar or vector of length min (M, N)
- sparse/sprandn.m:72 [test] sprandn: RC must be a scalar or vector of length min (M, N)
- sparse/sprandsym.m:175 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x1)       E(2x1)      Dimensions don't match
- sparse/tfqmr.m:429 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1            2         Abs err 1 exceeds tol 0 by 1
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
- statistics/bounds.m:110 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x3)       E(1x3)      Dimensions don't match
- statistics/bounds.m:156 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           1           NaN        'NaN' mismatch
- statistics/bounds.m:168 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason    (3,1)          12          NaN        'NaN' mismatch
- statistics/iqr.m:351 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(0x1)      E(0x1x2)     Dimensions don't match
- statistics/iqr.m:355 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x0)      E(1x0x2)     Dimensions don't match
- statistics/iqr.m:356 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x0)    E(1x0x1x1x1x1x1x1x2)   Dimensions don't match
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
- statistics/meansq.m:138 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         Class double != single
- statistics/median.m:835 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:837 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason     (1)      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:856 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:862 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()      1.84467440737096e+19 1.84467440737096e+19   Abs err 1 exceeds tol 0 by 1
- statistics/median.m:874 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- statistics/median.m:875 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- statistics/median.m:878 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- statistics/median.m:879 [assert] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           O            E         non-sparse != sparse
- statistics/normalize.m:608 [test] assert () failed
- statistics/normalize.m:613 [test] assert () failed
- statistics/range.m:101 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      .          O(3x3)       E(1x3)      Dimensions don't match
- statistics/range.m:133 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           26          NaN        'NaN' mismatch
- statistics/range.m:143 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           9           NaN        'NaN' mismatch
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
- testfun/assert.m:608 [error] error 'ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()          NaN           1         'NaN' mismatch' does not match <'NA' mismatch>
- testfun/assert.m:610 [test] NaN reported for NA assert
- testfun/assert_equal.m:619 [error] error 'ASSERT errors for:  assert_equal (,)    Location  |  Observed  |  Expected  |  Reason      ()          NaN           1         'NaN' mismatch' does not match <'NA' mismatch>
- testfun/assert_equal.m:621 [test] NaN reported for NA assert_equal
- testfun/assert_equal.m:786 [error] error 'ASSERT errors for:  assert_equal (,)    Location  |  Observed  |  Expected  |  Reason      .          O(1x1)       E(0x1)      Dimensions don't match' does not match <Expected classdef object, but observed double>
- testfun/assert_equal.m:788 [error] error 'real: wrong type argument 'containers.Map'' does not match <Expected classdef object does not have an 'eq' method>
- testfun/assert_equal.m:791 [error] error 'real: wrong type argument 'containers.Map'' does not match <Expected classdef object does not have an 'eq' method>
- testfun/demo.m:194 [warning] 'is_same_file' undefined
- testfun/example.m:113 [assert] 'is_same_file' undefined
- testfun/example.m:115 [test] 'is_same_file' undefined
- testfun/example.m:127 [warning] 'is_same_file' undefined
- testfun/oruntests.m:197 [error] error ''dir_in_loadpath' undefined' does not match <DIRECTORY argument>
- testfun/rundemos.m:121 [error] error ''dir_in_loadpath' undefined' does not match <DIRECTORY argument>
- testfun/speed.m:430 [test] 'airy' undefined
- testfun/test.m:965 [fail] expected error <C must be a vector> but got none
- testfun/test.m:967 [fail] expected error <C and R must be vectors> but got none
- testfun/test.m:971 [fail] expected error <called with too many inputs> but got <Invalid call to toeplitz>
- testfun/test.m:1071 [test] 'is_same_file' undefined
- testfun/test.m:1084 [test] ASSERT errors for:  assert (,)    Location  |  Observed  |  Expected  |  Reason      ()           0            1         Abs err 1 exceeds tol 0 by 1
