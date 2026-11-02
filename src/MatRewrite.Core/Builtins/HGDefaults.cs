// MatRewrite - GPL-3.0-or-later
// Phase 6: factory defaults of the graphics object types.
using System.IO;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static readonly Dictionary<string, Dictionary<string, Func<Value>>> TypeDefaults = BuildTypeDefaults();

        static Dictionary<string, Func<Value>> D_(params (string k, Func<Value> v)[] e)
        {
            var d = new Dictionary<string, Func<Value>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in e) d[k] = v; return d;
        }
        static Func<Value> S_(string s) => () => Str(s);
        static Func<Value> N_(double x) => () => Num(x);
        static Func<Value> R_(params double[] x) => () => MatrixValue.Row(x);
        static Value Alpha64() => new MatrixValue(64, 1, Enumerable.Range(0, 64).Select(i => i / 63.0).ToArray());

        static Dictionary<string, Dictionary<string, Func<Value>>> BuildTypeDefaults()
        {
            var black = R_(0, 0, 0); var dark = R_(0.15, 0.15, 0.15);
            var lineLike = new (string, Func<Value>)[]
            {
                ("color", R_(0, 0.447, 0.741)), ("linestyle", S_("-")), ("linewidth", () => Num(Graphics.FigStyle.Current.LineWidth)), ("marker", S_("none")),
                ("markersize", () => Num(Graphics.FigStyle.Current.MarkerSize)), ("markeredgecolor", S_("auto")), ("markerfacecolor", S_("none")),
                ("xdata", R_(0, 1)), ("ydata", R_(0, 1)), ("zdata", E), ("xdatasource", S_("")), ("ydatasource", S_("")),
                ("zdatasource", S_("")), ("interpreter", S_("tex")), ("linejoin", S_("round")),
            };
            var fig = D_(
                ("color", R_(1, 1, 1)), ("position", R_(300, 200, 560, 420)), ("outerposition", R_(300, 200, 560, 500)),
                ("innerposition", R_(300, 200, 560, 420)), ("units", S_("pixels")), ("windowstyle", S_("normal")), ("currentobject", E),
                ("currentpoint", R_(0, 0)), ("currentcharacter", S_("")), ("menubar", S_("figure")), ("toolbar", S_("auto")),
                ("paperunits", S_("inches")), ("paperposition", R_(0.25, 2.5, 8, 6)), ("papersize", R_(8.5, 11)),
                ("paperorientation", S_("portrait")), ("paperpositionmode", S_("auto")), ("papertype", S_("usletter")),
                ("renderer", S_("opengl")), ("renderermode", S_("auto")), ("nextplot", S_("add")), ("closerequestfcn", S_("closereq")),
                ("sizechangedfcn", E), ("resizefcn", E), ("resize", S_("on")), ("integerhandle", S_("on")), ("filename", S_("")),
                ("alphamap", Alpha64),
                ("windowbuttondownfcn", E), ("windowbuttonmotionfcn", E), ("windowbuttonupfcn", E), ("windowkeypressfcn", E),
                ("windowkeyreleasefcn", E), ("windowscrollwheelfcn", E), ("keypressfcn", E), ("keyreleasefcn", E),
                ("inverthardcopy", S_("on")), ("graphicssmoothing", S_("on")), ("dockcontrols", S_("off")), ("pointer", S_("arrow")),
                ("selectiontype", S_("normal")), ("number", E), ("name", S_("")), ("numbertitle", S_("on")), ("windowstate", S_("normal")),
                ("__graphics_toolkit__", S_("qt")), ("__plot_stream__", E), ("__rotate_mode__", E), ("__zoom_mode__", E), ("__pan_mode__", E),
                ("__mouse_mode__", S_("none")), ("currentaxes", E), ("colormap", E), ("visible", S_("on")));
            var axes = D_(
                ("position", R_(0.13, 0.11, 0.775, 0.815)), ("outerposition", R_(0, 0, 1, 1)), ("innerposition", R_(0.13, 0.11, 0.775, 0.815)),
                ("units", S_("normalized")), ("color", R_(1, 1, 1)), ("xcolor", dark), ("ycolor", dark), ("zcolor", dark),
                ("fontsize", () => Num(Graphics.FigStyle.Current.FontSize)), ("fontname", () => Str(Graphics.FigStyle.Current.FontName)), ("fontweight", S_("normal")), ("fontangle", S_("normal")), ("fontunits", S_("points")),
                ("fontsizemode", S_("auto")), ("linewidth", () => Num(Graphics.FigStyle.Current.AxesLineWidth)), ("xtickmode", S_("auto")), ("ytickmode", S_("auto")), ("ztickmode", S_("auto")),
                ("xticklabelmode", S_("auto")), ("yticklabelmode", S_("auto")), ("zticklabelmode", S_("auto")),
                ("xlimmode", S_("auto")), ("ylimmode", S_("auto")), ("zlimmode", S_("auto")), ("climmode", S_("auto")), ("alim", R_(0, 1)), ("alimmode", S_("auto")),
                ("colororderindex", N_(1)), ("linestyleorder", S_("-")), ("linestyleorderindex", N_(1)),
                ("dataaspectratio", R_(1, 1, 1)), ("dataaspectratiomode", S_("auto")), ("plotboxaspectratio", R_(1, 1, 1)), ("plotboxaspectratiomode", S_("auto")),
                ("cameraposition", R_(0.5, 0.5, 9.16)), ("camerapositionmode", S_("auto")), ("cameratarget", R_(0.5, 0.5, 0.5)), ("cameratargetmode", S_("auto")),
                ("cameraupvector", R_(0, 1, 0)), ("cameraupvectormode", S_("auto")), ("cameraviewangle", N_(6.6086)), ("cameraviewanglemode", S_("auto")),
                ("projection", S_("orthographic")), ("xaxislocation", S_("bottom")), ("yaxislocation", S_("left")), ("xdir", S_("normal")), ("zdir", S_("normal")),
                ("xminorgrid", S_("off")), ("yminorgrid", S_("off")), ("zminorgrid", S_("off")), ("zgrid", S_("off")),
                ("xminortick", S_("off")), ("yminortick", S_("off")), ("zminortick", S_("off")), ("tickdir", () => Str(Graphics.FigStyle.Current.TickDir)), ("tickdirmode", S_("auto")),
                ("ticklength", R_(0.01, 0.025)), ("layer", S_("bottom")), ("gridlinestyle", S_("-")), ("gridalpha", N_(0.15)), ("gridcolor", R_(0.15, 0.15, 0.15)),
                ("minorgridlinestyle", S_(":")), ("minorgridalpha", N_(0.25)), ("minorgridcolor", R_(0.1, 0.1, 0.1)),
                ("activepositionproperty", S_("outerposition")), ("positionconstraint", S_("outerposition")),
                ("labelfontsizemultiplier", N_(1.1)), ("titlefontsizemultiplier", N_(1.1)), ("titlefontweight", () => Str(Graphics.FigStyle.Current.TitleBold ? "bold" : "normal")),
                ("tightinset", R_(0.04, 0.04, 0.01, 0.02)), ("looseinset", R_(0.13, 0.11, 0.095, 0.075)), ("ambientlightcolor", R_(1, 1, 1)),
                ("sortmethod", S_("childorder")), ("zscale", S_("linear")), ("xticklabel", () => new CellValue(0, 1)), ("yticklabel", () => new CellValue(0, 1)),
                ("zticklabel", () => new CellValue(0, 1)), ("ztick", E), ("currentpoint", () => new MatrixValue(2, 3)), ("drawmode", S_("normal")),
                ("xlimitmethod", S_("tickaligned")), ("ylimitmethod", S_("tickaligned")), ("zlimitmethod", S_("tickaligned")), ("clippingstyle", S_("3dbox")),
                ("colorscale", S_("linear")), ("legend", E), ("colorbar", E), ("__colorbar__", S_("none")), ("__legend__", E), ("mousewheelzoom", N_(0.5)),
                ("alphamap", Alpha64), ("alphascale", S_("linear")),
                ("ticklabelinterpreter", S_("tex")), ("xtick", E), ("ytick", E), ("xlim", R_(0, 1)), ("ylim", R_(0, 1)), ("zlim", R_(0, 1)), ("clim", R_(0, 1)),
                ("view", R_(0, 90)), ("xscale", S_("linear")), ("yscale", S_("linear")), ("nextplot", S_("replace")), ("xgrid", S_("off")), ("ygrid", S_("off")),
                ("box", () => Str(Graphics.FigStyle.Current.Box ? "on" : "off")), ("xdir", S_("normal")), ("ydir", S_("normal")), ("colororder", E), ("title", E), ("xlabel", E), ("ylabel", E), ("zlabel", E),
                ("colormap", E), ("nextseriesindex", N_(1)), ("fontsmoothing", S_("on")));
            var text = D_(
                ("string", S_("")), ("position", R_(0, 0, 0)), ("units", S_("data")), ("fontsize", N_(10)), ("fontname", S_("*")),
                ("fontweight", S_("normal")), ("fontangle", S_("normal")), ("fontunits", S_("points")), ("horizontalalignment", S_("left")),
                ("verticalalignment", S_("middle")), ("rotation", N_(0)), ("color", black), ("interpreter", S_("tex")),
                ("extent", R_(0, 0, 0, 0)), ("backgroundcolor", S_("none")), ("edgecolor", S_("none")), ("margin", N_(2)),
                ("linewidth", N_(0.5)), ("linestyle", S_("-")), ("editing", S_("off")), ("fontsmoothing", S_("on")), ("__autopos_tag__", S_("none")),
                ("positionmode", S_("auto")), ("rotationmode", S_("auto")), ("horizontalalignmentmode", S_("auto")), ("verticalalignmentmode", S_("auto")));
            var patchLike = new (string, Func<Value>)[]
            {
                ("faces", E), ("vertices", E), ("xdata", E), ("ydata", E), ("zdata", E), ("cdata", E), ("facecolor", black),
                ("edgecolor", black), ("facealpha", N_(1)), ("edgealpha", N_(1)), ("linestyle", S_("-")), ("linewidth", N_(0.5)),
                ("marker", S_("none")), ("markersize", N_(6)), ("markeredgecolor", S_("auto")), ("markerfacecolor", S_("none")),
                ("facevertexcdata", E), ("facevertexalphadata", E), ("facelighting", S_("flat")), ("edgelighting", S_("none")),
                ("backfacelighting", S_("reverselit")), ("ambientstrength", N_(0.3)), ("diffusestrength", N_(0.6)),
                ("specularstrength", N_(0.9)), ("specularexponent", N_(10)), ("specularcolorreflectance", N_(1)),
                ("vertexnormals", E), ("facenormals", E), ("vertexnormalsmode", S_("auto")), ("facenormalsmode", S_("auto")),
                ("cdatamapping", S_("scaled")), ("alphadatamapping", S_("scaled")), ("alphadata", N_(1)),
            };
            var patch = D_(patchLike);
            var surface = D_(patchLike);
            foreach (var (k, v) in new (string, Func<Value>)[] { ("facecolor", S_("flat")), ("meshstyle", S_("both")), ("cdatasource", S_("")),
                ("xdatasource", S_("")), ("ydatasource", S_("")), ("zdatasource", S_("")) }) surface[k] = v;
            var image = D_(("cdata", E), ("xdata", R_(1, 1)), ("ydata", R_(1, 1)), ("cdatamapping", S_("direct")), ("alphadata", N_(1)),
                ("alphadatamapping", S_("none")), ("interpolation", S_("nearest")));
            var light = D_(("color", R_(1, 1, 1)), ("position", R_(1, 0, 1)), ("style", S_("infinite")));
            var root = D_(("screensize", R_(1, 1, 1920, 1080)), ("screenpixelsperinch", N_(96)), ("screendepth", N_(24)),
                ("monitorpositions", R_(1, 1, 1920, 1080)), ("units", S_("pixels")), ("callbackobject", E), ("currentobject", E),
                ("showhiddenhandles", S_("off")), ("pointerlocation", R_(0, 0)), ("pointerwindow", N_(0)), ("commandwindowsize", R_(80, 24)),
                ("diary", S_("off")), ("diaryfile", S_("diary")), ("echo", S_("off")), ("format", S_("short")), ("formatspacing", S_("loose")),
                ("language", S_("ascii")), ("recursionlimit", N_(256)), ("fixedwidthfontname", S_("Courier")), ("name", S_("")), ("currentfigure", E));
            var d = new Dictionary<string, Dictionary<string, Func<Value>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["root"] = root, ["figure"] = fig, ["axes"] = axes, ["line"] = D_(lineLike), ["text"] = text, ["patch"] = patch,
                ["surface"] = surface, ["image"] = image, ["light"] = light,
                ["hggroup"] = D_(("erasemode", S_("normal"))),
                ["scatter"] = D_(("xdata", E), ("ydata", E), ("zdata", E), ("cdata", E), ("sizedata", N_(36)), ("marker", S_("o")),
                    ("markeredgecolor", S_("flat")), ("markerfacecolor", S_("none")), ("linewidth", N_(0.5))),
                ["uicontrol"] = D_(("style", S_("pushbutton")), ("string", S_("")), ("value", N_(0)), ("callback", E), ("position", R_(20, 20, 80, 30)),
                    ("units", S_("pixels")), ("enable", S_("on")), ("min", N_(0)), ("max", N_(1)), ("backgroundcolor", R_(0.94, 0.94, 0.94)),
                    ("foregroundcolor", black), ("fontsize", N_(10)), ("tooltipstring", S_("")), ("horizontalalignment", S_("center"))),
                ["uipanel"] = D_(("title", S_("")), ("position", R_(0, 0, 1, 1)), ("units", S_("normalized")), ("backgroundcolor", R_(0.94, 0.94, 0.94)),
                    ("bordertype", S_("etchedin")), ("resizefcn", E), ("sizechangedfcn", E)),
                ["uibuttongroup"] = D_(("title", S_("")), ("position", R_(0, 0, 1, 1)), ("units", S_("normalized")), ("selectedobject", E), ("selectionchangedfcn", E)),
                ["uimenu"] = D_(("label", S_("")), ("text", S_("")), ("callback", E), ("menuselectedfcn", E), ("enable", S_("on")), ("checked", S_("off")),
                    ("accelerator", S_("")), ("separator", S_("off")), ("position", N_(1))),
                ["uicontextmenu"] = D_(("callback", E), ("position", R_(0, 0))),
                ["uitoolbar"] = D_(), ["uipushtool"] = D_(("cdata", E), ("clickedcallback", E), ("tooltipstring", S_(""))),
                ["uitoggletool"] = D_(("cdata", E), ("clickedcallback", E), ("state", S_("off")), ("oncallback", E), ("offcallback", E)),
                ["uitable"] = D_(("data", E), ("columnname", S_("numbered")), ("rowname", S_("numbered")), ("position", R_(20, 20, 300, 300))),
            };
            return d;
        }
    }
}
