using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace SumikkoCompanion
{
    // All parts are registered to this artboard. PNGs stay editable and unmodified;
    // transforms attach each sleeve to its shoulder instead of moving it as a sticker.
    public sealed partial class CharacterView : FrameworkElement
    {
        public const double ArtWidth = 1469, ArtHeight = 1071;
        public double MotionStrength { get; set; }
        // The adjustable numbers of the motion (see MotionTuning). After changing one, call
        // TuningChanged so a character at rest is drawn again with it.
        public MotionTuning Tuning { get { return tune; } }
        private readonly MotionTuning tune = new MotionTuning();
        public void TuningChanged() { everDrawn = false; }
        public MouseMotionRange MouseRange { get; set; }
        private MouseMotionRange appliedMouseRange = MouseMotionRange.Default;
        // The measurements of this character's artwork. The fields below named like constants
        // are read from it once, in the constructor.
        public CharacterProfile Profile { get { return profile; } }
        private readonly CharacterProfile profile;
        public Point MouseOffset
        {
            get { return new Point(Clamp(mouseX.Value + mouseDriftX, appliedMouseRange.Left, appliedMouseRange.Right),
                Clamp(mouseY.Value + mousePress.Value * tune.MouseClickSink.Value + mouseDriftY, appliedMouseRange.Top, appliedMouseRange.Bottom)); }
        }
        // The mouse hand shifts a few pixels by itself: a slow drift, the breath, and a nudge
        // from each keystroke. Switched off while the range is calibrated so the hand holds still.
        public bool IdleMouseMotion { get; set; }
        private double mouseDriftX, mouseDriftY;
        private readonly BitmapSource backplate, head, mouseArm, keyboardArm, expression, closedEyes;
        private readonly Rect leftEyeBox, rightEyeBox;
        private readonly Rect artboard = new Rect(0, 0, ArtWidth, ArtHeight);
        private readonly Geometry leftEye, rightEye, mouthRegion, collarForeground;
        private readonly Geometry keyRootClip, keySleeveClip, keyRimClip, keyHandClip;
        private readonly Spring mouseX = new Spring(), mouseY = new Spring();
        private readonly Spring mousePress = new Spring(), mouseLean = new Spring();
        private double mouseSquash, clickBob, clickStarted = -10;
        // The near sleeve is pinned at the shoulder line and carries the wrist line with the mouse.
        private readonly double MouseShoulderY, MouseWristY;
        // The sleeve artwork ends in a soft cap with no dark outline, which left the shoulder's
        // outline broken between the sleeve and the torso. The cap is cut along this line (the
        // sleeve's own outline carried on to the torso's) and the missing outline is drawn on it.
        private readonly double[] MouseShoulderLine;
        private readonly Pen ShoulderPen;
        private readonly Geometry mouseShoulderClip, mouseSleeveClip;
        // Below the wrist line: the hand and mouse, which keep their shape.
        private readonly Geometry MouseHandClip;
        private bool mouseWasDown;
        // A click shows as three short strokes bursting from the front of the mouse onto the
        // open desk, in the outline colour and weight of the artwork. Progress runs 0..1 while they are visible.
        private const double ClickPopSeconds = .24;
        private readonly Point ClickOrigin;
        private readonly double[] ClickRays;
        private readonly Pen ClickPen;
        internal double MouseClickPop
        {
            get { double pop = (time - clickStarted) / ClickPopSeconds; return pop >= 0 && pop < 1 ? pop : -1; }
        }
        // Sideways slant of the mouse and hand below the wrist, and how far a click
        // flattens them (both zero when the hand is still).
        internal double MouseHandLean { get { return mouseLean.Value; } }
        internal double MouseHandSquash { get { return mouseSquash; } }
        private readonly Spring headAngle = new Spring(), headX = new Spring(), headY = new Spring();
        // Lips pressed together (0 = the drawn mouth, 1 = fully pursed).
        private readonly Spring purse = new Spring();
        private readonly Brush mouthSkin, mouthFeather;
        private readonly Geometry pursedMouthRegion, neckRegion, headWithoutNeck;

        // The ahoge (the lock of hair standing up from the bun) trails the head and springs
        // back. Only the part standing free against the background moves: it is cut along a
        // line across its root (from where it leaves the bun to where it leaves the head) and
        // sheared about that line, so the root stays joined; the tip half bends again about a
        // second line, which makes the lock whip rather than tilt as a stiff piece.
        // The two pieces are outlines traced through the transparent gap between the lock and
        // the bun. A character without such a lock has none, and nothing sways.
        private readonly bool hasAhoge;
        private readonly Point AhogeRootA, AhogeRootB;
        private readonly Vector AhogeAlong, AhogeUp;
        private readonly double AhogeTipLine;
        private readonly Geometry ahogeBaseClip, ahogeTipClip, headWithoutNeckOrAhoge;
        // Lean along the root line, stretch away from it, and the tip's own flap (all as
        // displacement per pixel of distance from the line they are pinned to).
        private readonly Spring ahogeSway = new Spring(), ahogeStretch = new Spring(), ahogeTip = new Spring();
        private Point ahogeRootWas;
        private bool ahogeTracked;
        internal Vector AhogeBend { get { return new Vector(ahogeSway.Value, ahogeStretch.Value); } }
        internal double AhogeTipBend { get { return ahogeTip.Value; } }

        private static Vector Unit(Vector v)
        {
            v.Normalize();
            return v;
        }
        private readonly Point MouthCenter, MouthPivot, MouthCover, YawnTearSpot, HeadPivot;
        private readonly double HeadRise;
        private readonly double[] NearLid, FarLid;
        private readonly double[] WaveHand;
        private readonly double TriumphLift;
        // The curved lids (see CharacterProfile); empty for a character drawn the first way.
        private readonly double[] NearLidCurve, NearLidLower, FarLidCurve, FarLidLower;
        private double headTilt, nextTilt = 5, nextPurse = 8, purseUntil = -10, squeezeStart = -10;
        // Keyboard hand, in artboard pixels away from the resting artwork: travel across
        // the keys, height above them, and the wrist angle (positive = fingertips down).
        private readonly Spring reachX = new Spring(), reachY = new Spring();
        private readonly Spring lift = new Spring(), flick = new Spring(), nod = new Spring();
        private double time, mouth, blink, nextBlink = 2.7, blinkStarted = -10;
        private double lastTap = -10, strokeStart = -10, strokeWind, strokePeak;
        private double reachTargetX, reachTargetY;
        // Where the last new key landed: a held key keeps hammering around this spot.
        private double holdSpotX, holdSpotY;
        private bool strokeAccent, wasTyping;
        // The blink in progress: how slowly it runs, how far the lids close, and how many
        // quick blinks still follow it.
        private double blinkPace = 1, blinkDepth = 1;
        private int blinksToFollow;
        private int demoBeat = -1;
        private readonly Random random = new Random(42), strokeRandom = new Random(7);
        private readonly Pen NearLidPen, FarLidPen;

        // Lip sync: the level is multiplied by the gain, and anything at or below the floor
        // keeps the mouth shut. The defaults are the values used before they could be set.
        public const double DefaultLipSyncGain = 1, DefaultLipSyncFloor = .009;
        public const double MinLipSyncGain = .25, MaxLipSyncGain = 4, MaxLipSyncFloor = .1;
        public double LipSyncGain { get; set; }
        public double LipSyncFloor { get; set; }

        // Everything OnRender depends on, as of the last redraw. A frame that would look the
        // same is not drawn again, so a character at rest costs (almost) nothing to show.
        private const int DrawnValues = 24;
        private readonly double[] drawn = new double[DrawnValues], current = new double[DrawnValues];
        private bool everDrawn;
        // How many frames asked WPF to redraw (for the checks).
        internal int Redraws { get; private set; }

        // The decoded artwork is shared by every view (the range dialog has its own preview).
        private static readonly System.Collections.Generic.Dictionary<string, BitmapSource> LoadedImages =
            new System.Collections.Generic.Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        // The far sleeve bends at a hinge line parallel to its cuff. Up to the hinge it keeps
        // the resting artwork, so it always leaves the collar and hair at the same place;
        // from there it leans toward the wrist, and the cuff rim travels with the hand.
        private readonly Point KeyHinge;
        private readonly Vector KeyCuffAxis, KeyReach;
        private readonly double KeyForearm;
        // The hand is a rigid piece. Its edge inside the cuff is a circle about this point
        // (fitted to the outline within a pixel), so turning the hand keeps that edge in place.
        private readonly Point KeyPalm;
        // Outer edge of the hand's outline where it meets the cuff, top to bottom. Both ends
        // cross the outline that the sleeve and hand share, right where the two lines merge.
        private readonly double[] KeyHandEdge;
        // The hand swells and splats about this point near the wrist, so it grows out
        // over the keys rather than back over the sleeve.
        private readonly Point KeyKnuckle;
        internal bool KeyboardHandAtRest
        {
            get { return reachX.Value == 0 && reachY.Value == 0 && lift.Value == 0 && flick.Value == 0 && nod.Value == 0; }
        }
        internal Point KeyboardHandTarget
        {
            get { return new Point(reachTargetX, reachTargetY); }
        }
        internal Point KeyboardHandOffset
        {
            get { return new Point(reachX.Value, reachY.Value - lift.Value); }
        }

        public CharacterView(string assetsDirectory) : this(assetsDirectory, CharacterProfile.Load(assetsDirectory))
        {
        }

        public CharacterView(string assetsDirectory, CharacterProfile character)
        {
            profile = character;
            profile.ApplyTuning(tune);
            Color outline = profile.Color("outlineColor");
            double[] lidWidth = profile.Numbers("lidWidth");
            ShoulderPen = MakeOutlinePen(outline, 9.5);
            ClickPen = MakeOutlinePen(outline, 10);
            NearLidPen = MakeOutlinePen(outline, lidWidth[0]);
            FarLidPen = MakeOutlinePen(outline, lidWidth[1]);
            ZeePen = MakeOutlinePen(outline, 11);
            SymbolPen = MakeOutlinePen(outline, 8);
            Ink = MakeBrush(outline.R, outline.G, outline.B);
            MouseShoulderY = profile.Number("mouseShoulderY");
            MouseWristY = profile.Number("mouseWristY");
            MouseShoulderLine = profile.Numbers("mouseShoulderLine");
            MouseHandClip = Frozen(new RectangleGeometry(new Rect(0, MouseWristY, ArtWidth, ArtHeight - MouseWristY)));
            ClickOrigin = profile.Point("clickOrigin");
            ClickRays = profile.Numbers("clickRays");
            MouthCenter = profile.Point("mouthCenter");
            MouthPivot = profile.Point("mouthPivot");
            MouthCover = profile.Point("mouthCover");
            YawnTearSpot = profile.Point("yawnTear");
            HeadPivot = profile.Point("headPivot");
            HeadRise = Math.Max(0, profile.Number("headRise"));
            NearLid = profile.Numbers("nearLid");
            FarLid = profile.Numbers("farLid");
            WaveHand = profile.Numbers("waveHand");
            TriumphLift = profile.Number("triumphLift");
            NearLidCurve = profile.Numbers("nearLidCurve"); NearLidLower = profile.Numbers("nearLidLower");
            FarLidCurve = profile.Numbers("farLidCurve"); FarLidLower = profile.Numbers("farLidLower");
            KeyHinge = profile.Point("keyHinge");
            KeyCuffAxis = (Vector)profile.Point("keyCuffAxis");
            // Across the cuff, out along the forearm.
            KeyReach = new Vector(KeyCuffAxis.Y, -KeyCuffAxis.X);
            KeyForearm = profile.Number("keyForearm");
            KeyPalm = profile.Point("keyPalm");
            KeyKnuckle = profile.Point("keyKnuckle");
            KeyHandEdge = profile.Numbers("keyHandEdge");
            double[] rows = profile.Numbers("keyRows"), depth = profile.Numbers("keyDepth"), sideways = profile.Numbers("keyReachX");
            AlongMin = rows[0]; AlongMax = rows[1]; AlongStep = new Vector(rows[2], rows[3]);
            AcrossMin = depth[0]; AcrossMax = depth[1]; AcrossStep = new Vector(depth[2], depth[3]);
            ReachMinX = sideways[0]; ReachMaxX = sideways[1];
            double[] headArea = profile.Numbers("headArea");
            HeadCenter = new Point(headArea[0], headArea[1]);
            HeadRadiusX = headArea[2]; HeadRadiusY = headArea[3];
            SymbolAnchor = profile.Point("symbolAnchor");
            Width = ArtWidth;
            Height = ArtHeight;
            MotionStrength = 1;
            LipSyncGain = DefaultLipSyncGain;
            LipSyncFloor = DefaultLipSyncFloor;
            IdleMouseMotion = true;
            Scenes = true;
            PointerOnArt = new Point(double.NaN, double.NaN);
            MouseRange = profile.MouseRange;
            backplate = Load(assetsDirectory, "base.png");
            head = Load(assetsDirectory, "head.png");
            mouseArm = Load(assetsDirectory, "mouse-arm.png");
            keyboardArm = Load(assetsDirectory, "keyboard-arm.png");
            // The open mouth is cut from this one; the closed eyes may come from another (below).
            expression = Load(assetsDirectory, profile.OpenMouthSource(assetsDirectory));
            // These patches include the original feature and a little surrounding skin.
            // They are sampled from the registered expression image, not redrawn as UI shapes.
            leftEye = Polygon(PointsOf(profile.Numbers("leftEye")));
            rightEye = Polygon(PointsOf(profile.Numbers("rightEye")));
            double[] openMouth = profile.Numbers("mouthRegion");
            mouthRegion = new EllipseGeometry(new Point(openMouth[0], openMouth[1]), openMouth[2], openMouth[3]);
            // The far arm comes out from behind the body. Repaint the original torso after
            // the sleeve, out to the cardigan's own outer outline, so the sleeve emerges at
            // the far side of the body instead of beside the bow.
            double[] collar = profile.Numbers("collar");
            collarForeground = collar.Length > 0 ? Polygon(PointsOf(collar)) : null;
            // Keyboard arm pieces, split along lines parallel to the cuff. Neighbours overlap
            // by a pixel and agree along their shared line, so no seam shows between them.
            Point rim = KeyHinge + KeyReach * KeyForearm;
            double[] armBox = profile.Numbers("keyArmBox");
            double armLeft = armBox[0], armTop = armBox[1], armRight = armBox[2], armBottom = armBox[3];
            keyRootClip = Polygon(new Point(armLeft, armTop), AlongCuff(KeyHinge, armTop, 1), AlongCuff(KeyHinge, armBottom, 1), new Point(armLeft, armBottom));
            keySleeveClip = Polygon(AlongCuff(KeyHinge, armTop, 0), AlongCuff(rim, armTop, 1), AlongCuff(rim, armBottom, 1), AlongCuff(KeyHinge, armBottom, 0));
            var handPiece = new PointCollection();
            var rimPiece = new PointCollection();
            for (int i = 0; i < KeyHandEdge.Length; i += 2)
            {
                var edge = new Point(KeyHandEdge[i], KeyHandEdge[i + 1]);
                handPiece.Add(edge);
                // The rim keeps its own copy of the outline: it is the cuff's edge wherever
                // a turned hand slides out from under it.
                Vector inward = KeyPalm - edge;
                inward.Normalize();
                rimPiece.Add(edge + inward * 7);
            }
            // Both pieces close on the far side of the edge, between its first and last heights.
            double edgeTop = KeyHandEdge[1], edgeBottom = KeyHandEdge[KeyHandEdge.Length - 1];
            handPiece.Add(new Point(armRight, edgeBottom));
            handPiece.Add(new Point(armRight, edgeTop));
            rimPiece.Add(AlongCuff(rim, edgeBottom + 10, 0));
            rimPiece.Add(AlongCuff(rim, edgeTop, 0));
            keyHandClip = Polygon(handPiece);
            keyRimClip = Polygon(rimPiece);
            // Everything of the sleeve cap outside the shoulder line is dropped; past the end of
            // the line the cut follows the underside of the torso's own outline, so the cap no
            // longer thins that outline either.
            // (mouseShoulderCut: along the shoulder line, on along the torso's outline, and back
            // down the outer edge of the sleeve's outline, which the cap also fringes.)
            double[] capCut = profile.Numbers("mouseShoulderCut");
            // Above the shoulder line the sleeve stays where it is drawn; below, it follows the hand.
            // The clips are combined once here; a CombinedGeometry would be worked out again
            // on every redraw (measured: about 6% of the idle drawing cost).
            mouseShoulderClip = new RectangleGeometry(new Rect(0, 0, ArtWidth, MouseShoulderY + 1));
            mouseSleeveClip = new RectangleGeometry(new Rect(0, MouseShoulderY, ArtWidth, MouseWristY + 1.5 - MouseShoulderY));
            if (capCut.Length > 0)
            {
                Geometry cap = Polygon(PointsOf(capCut));
                mouseShoulderClip = Geometry.Combine(mouseShoulderClip, cap, GeometryCombineMode.Exclude, null);
                mouseSleeveClip = Geometry.Combine(mouseSleeveClip, cap, GeometryCombineMode.Exclude, null);
            }
            mouseShoulderClip.Freeze(); mouseSleeveClip.Freeze();
            leftEye.Freeze(); rightEye.Freeze(); mouthRegion.Freeze();
            closedEyes = ComposeClosedEyes(assetsDirectory, head, Load(assetsDirectory, profile.ClosedEyesSource(assetsDirectory)), leftEye, rightEye);
            leftEyeBox = Rect.Inflate(leftEye.Bounds, EyeFeather * 3, EyeFeather * 3);
            rightEyeBox = Rect.Inflate(rightEye.Bounds, EyeFeather * 3, EyeFeather * 3);
            // Pursing redraws the original mouth narrower. Both the skin laid over the drawn
            // mouth and the narrowed copy fade out toward their edges, inside plain skin.
            Color skin = profile.Color("skinColor"), clear = Color.FromArgb(0, skin.R, skin.G, skin.B);
            var cover = new RadialGradientBrush();
            cover.GradientStops.Add(new GradientStop(skin, 0));
            cover.GradientStops.Add(new GradientStop(skin, .8));
            cover.GradientStops.Add(new GradientStop(clear, 1));
            cover.Freeze();
            mouthSkin = cover;
            var feather = new RadialGradientBrush();
            feather.MappingMode = BrushMappingMode.Absolute;
            feather.Center = feather.GradientOrigin = MouthCenter;
            Point pursed = profile.Point("pursedMouth");
            feather.RadiusX = pursed.X; feather.RadiusY = pursed.Y;
            feather.GradientStops.Add(new GradientStop(Colors.Black, 0));
            feather.GradientStops.Add(new GradientStop(Colors.Black, .8));
            feather.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            feather.Freeze();
            mouthFeather = feather;
            pursedMouthRegion = new EllipseGeometry(MouthCenter, pursed.X, pursed.Y);
            pursedMouthRegion.Freeze();
            // The head artwork carries a short neck below the jaw line. It belongs to the body:
            // drawn in place under the head, it stays seated in the collar while the head moves.
            // The top edge follows the underside of the jaw outline; the rest lies in clear space.
            // (A head drawn without a neck has no such region, and is one whole piece.)
            double[] neck = profile.Numbers("neck");
            headWithoutNeck = new RectangleGeometry(artboard);
            if (neck.Length > 0)
            {
                neckRegion = Polygon(PointsOf(neck));
                headWithoutNeck = Geometry.Combine(headWithoutNeck, neckRegion, GeometryCombineMode.Exclude, null);
            }
            headWithoutNeck.Freeze();
            double[] lockBase = profile.Numbers("ahogeBase"), lockTip = profile.Numbers("ahogeTip");
            hasAhoge = lockBase.Length > 0;
            if (hasAhoge)
            {
                // The root line is the first two points of the base piece.
                AhogeRootA = new Point(lockBase[0], lockBase[1]);
                AhogeRootB = new Point(lockBase[2], lockBase[3]);
                AhogeAlong = Unit(AhogeRootB - AhogeRootA);
                AhogeUp = new Vector(AhogeAlong.Y, -AhogeAlong.X);
                // The moving pieces reach a pixel and a half past their pinned lines, under the
                // part that stays, so no hairline opens along the cuts.
                var basePiece = PointsOf(lockBase);
                basePiece[0] = AhogeRootA - AhogeUp * 1.5;
                basePiece[1] = AhogeRootB - AhogeUp * 1.5;
                ahogeBaseClip = Polygon(basePiece);
                Geometry locks = Polygon(PointsOf(lockBase));
                if (lockTip.Length > 0)
                {
                    // The tip bends about the vertical line its first and last points lie on.
                    AhogeTipLine = lockTip[0];
                    var tipPiece = PointsOf(lockTip);
                    double middle = 0;
                    foreach (Point point in tipPiece) middle += point.X / tipPiece.Count;
                    double under = middle < AhogeTipLine ? 1.5 : -1.5;
                    int last = tipPiece.Count - 1;
                    tipPiece[0] = new Point(tipPiece[0].X + under, tipPiece[0].Y);
                    tipPiece[last] = new Point(tipPiece[last].X + under, tipPiece[last].Y);
                    ahogeTipClip = Polygon(tipPiece);
                    locks = Geometry.Combine(locks, Polygon(PointsOf(lockTip)), GeometryCombineMode.Union, null);
                }
                headWithoutNeckOrAhoge = Geometry.Combine(headWithoutNeck, locks, GeometryCombineMode.Exclude, null);
                headWithoutNeckOrAhoge.Freeze();
            }
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            IsHitTestVisible = false;
        }

        // The point at height y on the line through origin parallel to the cuff, moved right by shift.
        private Point AlongCuff(Point origin, double y, double shift)
        {
            return new Point(origin.X + (y - origin.Y) * KeyCuffAxis.X / KeyCuffAxis.Y + shift, y);
        }

        // The artwork's own outline colour, for everything drawn over it.
        private static Pen MakeOutlinePen(Color colour, double width)
        {
            var pen = new Pen(new SolidColorBrush(colour), width);
            pen.StartLineCap = pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();
            return pen;
        }

        private static Geometry Frozen(Geometry geometry)
        {
            geometry.Freeze();
            return geometry;
        }

        private static PointCollection PointsOf(double[] pairs)
        {
            var points = new PointCollection();
            for (int i = 0; i < pairs.Length; i += 2) points.Add(new Point(pairs[i], pairs[i + 1]));
            return points;
        }

        private static Geometry Polygon(params Point[] points)
        {
            return Polygon(new PointCollection(points));
        }

        private static Geometry Polygon(PointCollection points)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(points[0], true, true);
                for (int i = 1; i < points.Count; i++) g.LineTo(points[i], true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        private static BitmapSource Load(string directory, string name)
        {
            string path = Path.GetFullPath(Path.Combine(directory, name));
            lock (LoadedImages)
            {
                BitmapSource loaded;
                if (LoadedImages.TryGetValue(path, out loaded)) return loaded;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                LoadedImages[path] = image;
                return image;
            }
        }

        // The head with its eyes closed: the closed eyes of the expression artwork laid into
        // the head through soft-edged patches. A hard-edged patch is fine for a blink, but
        // held for a while (asleep, smiling) its edge shows as a faint line across the skin
        // and a step in the bangs.
        private const double EyeFeather = 5;

        private static BitmapSource ComposeClosedEyes(string directory, BitmapSource head, BitmapSource expression, params Geometry[] eyes)
        {
            string key = Path.GetFullPath(Path.Combine(directory, "head.png")) + "|closed-eyes";
            lock (LoadedImages)
            {
                BitmapSource made;
                if (LoadedImages.TryGetValue(key, out made)) return made;
                int width = head.PixelWidth, height = head.PixelHeight;
                var all = new Rect(0, 0, width, height);
                // The patches, a little larger than the eye regions and blurred at the edge.
                var grown = new Pen(Brushes.Black, EyeFeather * 2);
                grown.LineJoin = PenLineJoin.Round;
                var shape = new DrawingVisual();
                using (DrawingContext dc = shape.RenderOpen())
                {
                    dc.PushTransform(new ScaleTransform(width / ArtWidth, height / ArtHeight));
                    foreach (Geometry eye in eyes) dc.DrawGeometry(Brushes.Black, grown, eye);
                    dc.Pop();
                }
                shape.Effect = new BlurEffect { Radius = EyeFeather * 2, KernelType = KernelType.Gaussian };
                var mask = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                mask.Render(shape);
                var picture = new DrawingVisual();
                using (DrawingContext dc = picture.RenderOpen())
                {
                    dc.DrawImage(head, all);
                    dc.PushOpacityMask(new ImageBrush(mask));
                    dc.DrawImage(expression, all);
                    dc.Pop();
                }
                var composed = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                composed.Render(picture);
                composed.Freeze();
                LoadedImages[key] = composed;
                return composed;
            }
        }

        // How far the mouth opens (0..1) for an audio level, as Tick drives it.
        internal static double MouthOpening(double audioLevel, double gain, double floor)
        {
            double level = Clamp(audioLevel, 0, 1) * Clamp(gain, MinLipSyncGain, MaxLipSyncGain);
            floor = Clamp(floor, 0, MaxLipSyncFloor);
            return level <= floor ? 0 : Clamp(Math.Sqrt((level - floor) / .21), 0, 1);
        }

        // True while something moves fast enough that a lower frame rate would show: a stroke
        // or its settling, a click, the hand chasing the pointer, the mouth, a squeezed blink.
        // The slow idle sways, blinks and head tilts look the same at 30 fps.
        internal bool NeedsFullFrameRate
        {
            get
            {
                return !KeyboardHandAtRest || mousePress.Value != 0 || MouseClickPop >= 0 || mouth > 0 || ActingFast ||
                    Math.Abs(ahogeStretch.Velocity) + Math.Abs(ahogeTip.Velocity) > .12 ||
                    Math.Abs(mouseX.Velocity) + Math.Abs(mouseY.Velocity) > 30 || Math.Abs(mouseLean.Value) > .002 ||
                    (time >= squeezeStart && time - squeezeStart < .3);
            }
        }

        // Synthetic levels go through the same envelope and artwork as real audio.
        // Three increasing openings followed by silence make both opening and closing visible.
        internal static double LipSyncTestLevel(double elapsed)
        {
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)return 0;
            double phase=elapsed%4.2;
            if(phase>=2.4)return 0;
            int syllable=(int)(phase/.8);
            double peak=syllable==0?.045:(syllable==1?.11:.24);
            double wave=Math.Sin(Math.PI*(phase-syllable*.8)/.8);
            return peak*wave*wave;
        }

        public void Tick(double elapsed, double dt, bool keyDown, bool mouseDown,
            double pointerX, double pointerY, double audioLevel, bool demo, bool reducedMotion,
            bool keyAccent = false, bool keyRepeat = false)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed)) return;
            dt = Clamp(dt, 0, .05);
            time = elapsed;
            if (demo)
            {
                pointerX = Math.Sin(time * .83) * .8;
                pointerY = Math.Sin(time * .57) * .65;
                // A fast burst, a slower phrase, one emphatic Enter, then rest.
                double phrase = time % 7;
                int beat = phrase < 2.2 ? (int)(phrase * 9) : (phrase < 3.8 ? 100 + (int)((phrase - 2.2) * 3.6) :
                    (phrase < 4.4 ? 200 : -1));
                keyDown = beat >= 0 && beat != demoBeat;
                demoBeat = beat;
                keyAccent = beat == 200;
                mouseDown = time % 4.3 > 3.9;
                audioLevel = time % 6 < 3.5 ? .035 + .14 * Math.Pow(Math.Max(0, Math.Sin(time * 8.3)), 2) : 0;
            }
            double amount = (reducedMotion ? .32 : 1) * Clamp(MotionStrength, .2, 1.6);
            appliedMouseRange = MouseRange ?? profile.MouseRange;
            if(reducedMotion)appliedMouseRange=appliedMouseRange.Reduced();
            Point mouseTarget = appliedMouseRange.Map(pointerX,pointerY);
            // Lively follow: the hand darts after the pointer and settles with a small overshoot.
            mouseX.Step(mouseTarget.X, dt, tune.MouseFollow.Value, tune.MouseDamping.Value);
            mouseY.Step(mouseTarget.Y, dt, tune.MouseFollow.Value, tune.MouseDamping.Value);
            // A smaller range applies immediately, including existing spring momentum.
            mouseX.Constrain(appliedMouseRange.Left,appliedMouseRange.Right);
            mouseY.Constrain(appliedMouseRange.Top,appliedMouseRange.Bottom);
            // A click punches down and bounces; releasing pops back up past rest.
            mousePress.Step(mouseDown ? 1 : 0, dt, 38, .45);
            if (mouseDown && !mouseWasDown) clickStarted = time;
            mouseWasDown = mouseDown;
            double liveliness = reducedMotion ? .32 : 1;
            mouseSquash = tune.MouseSquash.Value * mousePress.Value * liveliness;
            // How hard the click is pressing (0..1 and a little over), for the head's dip.
            clickBob = tune.MouseSquash.Value > 0 ? Math.Max(0, mouseSquash) / tune.MouseSquash.Value
                : Math.Max(0, mousePress.Value * liveliness);
            // The mouse trails the wrist on a quick sideways move and swings back after it.
            mouseLean.Step(Clamp(-mouseX.Velocity * tune.MouseLean.Value, -tune.MouseLeanMax.Value, tune.MouseLeanMax.Value) * liveliness, dt, 30, .45);
            if (reducedMotion || !IdleMouseMotion) mouseDriftX = mouseDriftY = 0;
            else
            {
                mouseDriftX = (4 * Math.Sin(time * .33 + 2.1) + 2 * Math.Sin(time * .83 + .4)) * tune.MouseDrift.Value + nod.Value * .8;
                mouseDriftY = (2.2 * Math.Sin(time * .47 + 1.1) + 1.2 * Math.Sin(time * 1.65)) * tune.MouseDrift.Value + nod.Value * 1.6;
            }
            // NativeSignals sends one pulse per actual press. Never repeat a held key
            // or drive this arm with idle breathing; it must settle when typing stops.
            if (keyDown)
            {
                BeginStroke(keyAccent, keyRepeat, amount);
                ActOnKey(keyAccent, keyRepeat);
            }
            StepKeyboardHand(dt, amount);
            StepActing(dt, keyDown, mouseDown, pointerX, pointerY, reducedMotion);
            double breathing = reducedMotion ? 0 : Math.Sin(time * 1.65);
            // Idle life: slow overlapping sways that never repeat exactly, a lean toward the
            // pointer, and now and then the head tips to one side for a moment.
            if (reducedMotion) headTilt = 0;
            else if (time >= nextTilt && !actQuiet)
            {
                bool tipping = headTilt == 0;
                // Eyes usually blink as the head starts to move.
                if (random.NextDouble() < .7) BlinkSoon(.05 + .15 * random.NextDouble());
                headTilt = tipping ? (random.NextDouble() < .5 ? -1 : 1) * (1.2 + 1.3 * random.NextDouble()) * tune.HeadTiltSize.Value : 0;
                nextTilt = time + (tipping ? 1.4 + 2.2 * random.NextDouble() : (5 + 8 * random.NextDouble()) * tune.HeadTiltEvery.Value);
            }
            double sway = tune.HeadSway.Value * (1 - .6 * sleepLevel);
            headAngle.Step(reducedMotion ? 0 : -tune.HeadPointerLean.Value * pointerX + .55 * Math.Sin(time * .71) * sway +
                .35 * Math.Sin(time * .29 + 1.3) * sway + headTilt + actHeadAngle, dt, headAngleFrequency, headAngleDamping);
            headX.Step(reducedMotion ? 0 : 2.2 * Math.Sin(time * .37 + .6) * sway - 2.5 * pointerX, dt, 5, 1);
            // Breathing only ever lowers the head from the original artwork, so the jaw
            // never lifts off the neck (a deeper breath also rests lower).
            double breath = tune.HeadBreath.Value;
            headY.Step(reducedMotion ? 0 : Math.Max(4.5, breath + 1.3) + breathing * breath * (1 + .9 * sleepLevel) +
                1.3 * Math.Sin(time * .47) + actHeadY, dt, headYFrequency, headYDamping);
            // The lips press together while keys are being hit, hardest on an emphatic one,
            // and occasionally on their own.
            if (time >= nextPurse && !actQuiet)
            {
                purseUntil = time + 1 + .8 * random.NextDouble();
                nextPurse = time + (9 + 11 * random.NextDouble()) * tune.PurseEvery.Value;
            }
            double sinceTap = time - lastTap;
            // An open mouth (a smile, a yawn) is never pursed at the same time.
            purse.Step(actMouth > .05 ? 0 : (sinceTap < (strokeAccent ? .8 : .4) ? (strokeAccent ? 1 : tune.PurseTyping.Value) :
                (time < purseUntil ? .8 : 0)), dt, 12, .75);

            // Blinking is irregular on purpose: mostly a couple of seconds apart, sometimes in
            // quick succession, sometimes after a long stare; held back while keys are being
            // hit and released when the typing pauses.
            bool typing = sinceTap < .6;
            if (wasTyping && !typing) BlinkSoon(.05);
            wasTyping = typing;
            // Eyes held shut by acting do not blink, nor the moment they open again.
            if (actEyes > 0) nextBlink = Math.Max(nextBlink, time + .35);
            if (time >= nextBlink)
            {
                blinkStarted = time;
                if (blinksToFollow > 0)
                {
                    blinksToFollow--;
                    blinkPace = .8 * tune.BlinkSlowness.Value * drowsyPace;
                    blinkDepth = 1;
                }
                else
                {
                    double kind = random.NextDouble(), run = random.NextDouble();
                    // Quick, ordinary or lazy; now and then the lids do not quite meet.
                    blinkPace = (kind < .3 ? .8 : (kind < .85 ? 1 : 1.6)) * tune.BlinkSlowness.Value * drowsyPace;
                    blinkDepth = random.NextDouble() < .12 ? .7 : 1;
                    blinksToFollow = run < tune.BlinkDouble.Value ? 1 : (run < tune.BlinkDouble.Value + .05 ? 2 : 0);
                }
                if (blinksToFollow > 0) nextBlink = time + .25 * blinkPace + .05 + .12 * random.NextDouble();
                else
                {
                    double gap = random.NextDouble();
                    gap = gap < .2 ? .9 + .9 * random.NextDouble() : (gap < .85 ? 1.8 + 2.4 * random.NextDouble() : 4.2 + 3 * random.NextDouble());
                    nextBlink = time + gap * (typing ? 1.5 : 1) * tune.BlinkEvery.Value;
                }
            }
            double blinkAge = (time - blinkStarted) / blinkPace;
            if (blinkAge < .085) blink = Smooth(blinkAge / .085);
            else if (blinkAge < .125) blink = 1;
            else if (blinkAge < .25) blink = 1 - Smooth((blinkAge - .125) / .125);
            else blink = 0;
            blink *= blinkDepth;
            // An emphatic key lands with the eyes squeezed shut for an instant.
            double squeezeAge = time - squeezeStart;
            if (squeezeAge >= 0 && squeezeAge < .3)
                blink = Math.Max(blink, squeezeAge < .05 ? Smooth(squeezeAge / .05) : (squeezeAge < .2 ? 1 : 1 - Smooth((squeezeAge - .2) / .1)));
            blink = Math.Max(blink, actEyes);

            // A noise floor and asymmetric envelope prevent idle chatter and abrupt closing.
            double target = MouthOpening(audioLevel, LipSyncGain, LipSyncFloor);
            double tau = target > mouth ? .035 : .11;
            mouth += (target - mouth) * (1 - Math.Exp(-dt / tau));
            if (mouth < .006) mouth = 0;
            mouthShown = Math.Max(mouth, actMouth);
            StepAhoge(dt, reducedMotion);
            if (DrawnStateChanged())
            {
                Redraws++;
                InvalidateVisual();
            }
        }

        // Where the head is drawn: turned about the base of the neck and moved, never above
        // its place in the artwork.
        private Matrix HeadPlacement()
        {
            var placement = Matrix.Identity;
            // Each keystroke lands with a small sympathetic dip of the head.
            // A click gets a smaller dip of the head, leaning toward the mouse side.
            placement.RotateAt(headAngle.Value + nod.Value * tune.HeadDipTilt.Value - clickBob * .5, HeadPivot.X, HeadPivot.Y);
            placement.Translate(headX.Value, Math.Max(-HeadRise, headY.Value + nod.Value * tune.HeadDip.Value + clickBob * tune.HeadClickDip.Value));
            return placement;
        }

        // The lock has weight: it lags behind whatever the head does (measured at its root,
        // so a turn of the head counts as much as a drop) and swings back past rest.
        private void StepAhoge(double dt, bool reducedMotion)
        {
            if (!hasAhoge) return;
            double amount = reducedMotion ? 0 : tune.HairAhoge.Value;
            Point root = HeadPlacement().Transform(AhogeRootA + (AhogeRootB - AhogeRootA) * .5);
            root.Y += BodyDrop;
            Vector speed = ahogeTracked && dt > 0 ? (root - ahogeRootWas) / dt : new Vector();
            ahogeRootWas = root;
            ahogeTracked = true;
            double along = speed.X * AhogeAlong.X + speed.Y * AhogeAlong.Y, up = speed.X * AhogeUp.X + speed.Y * AhogeUp.Y;
            // A breath of air keeps it from ever being quite still.
            double breeze = amount > 0 ? .011 * Math.Sin(time * 1.37) + .006 * Math.Sin(time * 3.1 + 1) : 0;
            ahogeSway.Step(amount * Clamp(-along * .0011, -.2, .2) + breeze, dt, 18, .22);
            ahogeStretch.Step(amount * Clamp(-up * .0009, -.12, .18), dt, 23, .26);
            // The tip is lighter and slower: it follows the root's motion a beat late.
            ahogeTip.Step(amount * Clamp(-speed.Y * .0011, -.16, .16) + breeze * .8, dt, 15, .2);
        }

        private void DrawAhoge(DrawingContext dc)
        {
            // Every point moves in proportion to its distance from the root line.
            Vector push = AhogeAlong * ahogeSway.Value + AhogeUp * ahogeStretch.Value;
            double line = AhogeRootA.X * AhogeUp.X + AhogeRootA.Y * AhogeUp.Y;
            var lean = new Matrix(1 + AhogeUp.X * push.X, AhogeUp.X * push.Y, AhogeUp.Y * push.X, 1 + AhogeUp.Y * push.Y,
                -line * push.X, -line * push.Y);
            dc.PushTransform(new MatrixTransform(lean));
            dc.PushClip(ahogeBaseClip);
            dc.DrawImage(head, artboard);
            dc.Pop(); dc.Pop();
            if (ahogeTipClip == null) return;
            // The tip half bends about its own line first, then leans with the rest.
            var flap = new Matrix(1, -ahogeTip.Value, 0, 1, 0, AhogeTipLine * ahogeTip.Value);
            flap.Append(lean);
            dc.PushTransform(new MatrixTransform(flap));
            dc.PushClip(ahogeTipClip);
            dc.DrawImage(head, artboard);
            dc.Pop(); dc.Pop();
        }

        // Compares what OnRender would draw now with the last redraw. Springs come to rest on
        // their exact targets, so a character that has settled compares equal.
        private bool DrawnStateChanged()
        {
            Point hand = MouseOffset;
            current[0] = reachX.Value; current[1] = reachY.Value; current[2] = lift.Value;
            current[3] = flick.Value; current[4] = nod.Value;
            current[5] = hand.X; current[6] = hand.Y; current[7] = mouseLean.Value; current[8] = mouseSquash;
            current[9] = MouseClickPop;
            current[10] = headAngle.Value; current[11] = headX.Value; current[12] = headY.Value;
            current[13] = blink; current[14] = purse.Value; current[15] = mouthShown; current[16] = clickBob;
            // Symbols move by the clock alone, so while any is shown every frame differs.
            current[17] = BodyDrop; current[18] = symbols.Count > 0 ? time : -1; current[19] = yawnTear;
            current[20] = ahogeSway.Value; current[21] = ahogeStretch.Value; current[22] = ahogeTip.Value;
            current[23] = handCalm;
            bool changed = !everDrawn;
            for (int i = 0; i < DrawnValues && !changed; i++)
                if (current[i] != drawn[i]) changed = true;
            if (!changed) return false;
            Array.Copy(current, drawn, DrawnValues);
            everDrawn = true;
            return true;
        }

        // Brings the next blink forward, but never into the middle of the current one.
        private void BlinkSoon(double delay)
        {
            nextBlink = Math.Min(nextBlink, Math.Max(time + delay, blinkStarted + .25 * blinkPace + .06));
        }

        // Stroke spots are picked on the keyboard drawing: along its rows (toward the body) and
        // across them (out along the desk), within what the sleeve drawing reaches without
        // being squashed. spread (0..1) shrinks the area with the motion amount.
        private readonly double AlongMin, AlongMax, AcrossMin, AcrossMax, ReachMinX, ReachMaxX;
        // How far one unit along the rows, and one across them, moves the hand.
        private readonly Vector AlongStep, AcrossStep;

        private Point KeySpot(double along, double across, double spread)
        {
            return new Point(Clamp(spread * (along * AlongStep.X + across * AcrossStep.X), ReachMinX, ReachMaxX),
                spread * (along * AlongStep.Y + across * AcrossStep.Y));
        }

        // The bounding box of every spot KeySpot can return for this spread.
        private Rect KeyArea(double spread)
        {
            var area = new Rect(KeySpot(AlongMin, AcrossMin, spread), KeySpot(AlongMax, AcrossMax, spread));
            area.Union(KeySpot(AlongMin, AcrossMax, spread));
            area.Union(KeySpot(AlongMax, AcrossMin, spread));
            return area;
        }

        private void BeginStroke(bool accent, bool repeat, double amount)
        {
            double interval = time - lastTap;
            lastTap = time;
            double spread = Clamp(amount, 0, 1);
            if (repeat)
            {
                // A held key: hammer the spot where it first landed, each short stroke a few
                // pixels off it. The offset is never accumulated, so holding the key for any
                // length of time cannot carry the hand away from the keys.
                Rect area = KeyArea(spread);
                reachTargetX = Clamp(holdSpotX + (strokeRandom.NextDouble() - .5) * 14 * spread, area.Left, area.Right);
                reachTargetY = Clamp(holdSpotY + (strokeRandom.NextDouble() - .5) * 8 * spread, area.Top, area.Bottom);
                strokePeak = (tune.KeyHoldHeight.Value + 10 * strokeRandom.NextDouble()) * amount;
                strokeWind = lift.Value > 7 * amount ? 0 : .03;
                strokeAccent = false;
                strokeStart = time;
                return;
            }
            // An isolated press gets a full swing; rapid typing has no time for one.
            double slow = Smooth((interval - .09) / .3);
            // Every stroke lands on a new spot well away from the last one.
            double apart;
            Point target;
            int tries = 0;
            do
            {
                double along = AlongMin + (AlongMax - AlongMin) * strokeRandom.NextDouble();
                double across = AcrossMin + (AcrossMax - AcrossMin) * strokeRandom.NextDouble();
                target = KeySpot(along, across, spread);
                apart = Math.Sqrt((target.X - reachTargetX) * (target.X - reachTargetX) + (target.Y - reachTargetY) * (target.Y - reachTargetY));
            } while (apart < tune.KeyApart.Value * spread && ++tries < 8);
            reachTargetX = holdSpotX = target.X;
            reachTargetY = holdSpotY = target.Y;
            double hopX = reachTargetX - reachX.Value, hopY = reachTargetY - reachY.Value;
            double hop = Math.Sqrt(hopX * hopX + hopY * hopY);
            // No two strokes are identical; a far key is reached in a higher arc.
            double vary = .85 + .3 * strokeRandom.NextDouble();
            strokePeak = Math.Min(tune.KeyMaxHeight.Value, ((tune.KeyHeight.Value + tune.KeyHeightSlow.Value * slow) * amount +
                tune.KeyHopLift.Value * hop) * vary * (accent ? tune.KeyAccent.Value : 1));
            // A hand already in the air strikes at once; from rest it has to wind up first.
            bool airborne = lift.Value > 7 * amount;
            strokeWind = (airborne ? 0 : .04) + Math.Min(.04, hop * .0004) + .01 * slow + (accent ? .025 : 0);
            strokeAccent = accent;
            strokeStart = time;
            if (accent) squeezeStart = time + strokeWind;
        }

        private void StepKeyboardHand(double dt, double amount)
        {
            double age = time - strokeStart;
            double strikeEnd = strokeWind + .055;
            double hover = tune.KeyHover.Value * amount, press = tune.KeyPress.Value * amount;
            bool typing = age < strikeEnd + tune.KeyLinger.Value;
            double target, frequency, damping;
            if (age < strokeWind) { target = strokePeak; frequency = 58; damping = .6; }
            else if (age < strikeEnd) { target = -press; frequency = 85; damping = .5; }
            else if (age < strikeEnd + .07) { target = hover + strokePeak * tune.KeyRebound.Value; frequency = 55; damping = .5; }
            else if (typing) { target = hover; frequency = 30; damping = .55; }
            else { target = 0; frequency = 13; damping = 1; }
            // Between phrases the hand glides home and sets down on the resting artwork.
            double reachFrequency = typing ? 50 : 11, reachDamping = typing ? .9 : 1;
            double reachGoalX = typing ? reachTargetX : 0, reachGoalY = typing ? reachTargetY : 0;
            // Acting may take the hand over (a wave, a held pose, drumming); NaN = no say on the wrist.
            double wrist = double.NaN;
            ActOnHand(amount, age, strikeEnd, ref typing, ref target, ref frequency, ref damping,
                ref reachGoalX, ref reachGoalY, ref reachFrequency, ref wrist);
            lift.Step(target, dt, frequency, damping);
            // The keys stop the hand instead of letting the spring sink through the board.
            lift.Floor(-press * 1.3, .25);
            reachX.Step(reachGoalX, dt, reachFrequency, reachDamping);
            reachY.Step(reachGoalY, dt, reachFrequency, reachDamping);
            // Fingers trail the wrist: they hang on the way up and whip down at impact.
            flick.Step(double.IsNaN(wrist) ? Clamp(lift.Velocity * tune.KeyFlick.Value, -tune.KeyFlickMax.Value, tune.KeyFlickMax.Value) : wrist,
                dt, 44, .45);
            bool striking = age >= strokeWind && age < strikeEnd + .03;
            accentImpact = striking && strokeAccent;
            nod.Step(striking ? (strokeAccent ? tune.HeadAccent.Value : 1) * amount : 0, dt, 30, .5);
        }

        private void DrawKeyboardArm(DrawingContext dc)
        {
            double x = reachX.Value, y = reachY.Value - lift.Value, turn = flick.Value;
            if (x == 0 && y == 0 && turn == 0 && nod.Value == 0)
            {
                // Resting: the untouched artwork, with no seam at the wrist.
                dc.DrawImage(keyboardArm, artboard);
                return;
            }
            DrawKeyboardPiece(dc, keyRootClip, Matrix.Identity);
            // Each point of the sleeve moves in proportion to its distance from the hinge,
            // reaching the hand's full offset at the cuff rim.
            double gx = x / KeyForearm, gy = y / KeyForearm;
            double hinge = KeyReach.X * KeyHinge.X + KeyReach.Y * KeyHinge.Y;
            DrawKeyboardPiece(dc, keySleeveClip, new Matrix(1 + gx * KeyReach.X, gy * KeyReach.X,
                gx * KeyReach.Y, 1 + gy * KeyReach.Y, -gx * hinge, -gy * hinge));
            var carried = Matrix.Identity;
            carried.Translate(x, y);
            DrawKeyboardPiece(dc, keyRimClip, carried);
            // The hand follows part of the forearm's lean, then adds its own wrist motion.
            Vector forearm = KeyReach * KeyForearm;
            // Typing, the hand stays flat over the keys and takes only a little of that lean.
            // Held up to wave it has to turn with the arm: left flat, the wrist edge no longer
            // lines up with the cuff, the heel of the hand hangs below the sleeve, and the
            // hand looks cut off at the wrist.
            double follow = .3 + .6 * handCalm, most = 9 + 26 * handCalm;
            double lean = Clamp(Vector.AngleBetween(forearm, forearm + new Vector(x, y)) * follow, -most, most);
            // Comic emphasis: the hand swells as it rises and splats wide when it lands. It
            // never gets smaller than the artwork, so it always covers the cuff rim.
            double swell = 1 + tune.KeySwell.Value * (1 - .7 * handCalm) * Clamp(lift.Value / 90, 0, 1), splat = tune.KeySplat.Value * Clamp(nod.Value, 0, 2.6);
            var hand = Matrix.Identity;
            hand.ScaleAt(swell + splat, swell, KeyKnuckle.X, KeyKnuckle.Y);
            double furthest = 26 + 24 * handCalm;
            hand.RotateAt(Clamp(turn + lean, -furthest, furthest), KeyPalm.X, KeyPalm.Y);
            hand.Translate(x, y);
            DrawKeyboardPiece(dc, keyHandClip, hand);
        }

        private void DrawKeyboardPiece(DrawingContext dc, Geometry clip, Matrix transform)
        {
            dc.PushTransform(new MatrixTransform(transform));
            dc.PushClip(clip);
            dc.DrawImage(keyboardArm, artboard);
            dc.Pop(); dc.Pop();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            // A hard landing drops the whole picture for an instant (never upward: the
            // artwork is cut off at the bottom edge).
            dc.PushTransform(new TranslateTransform(0, BodyDrop));
            dc.DrawImage(backplate, artboard);

            DrawKeyboardArm(dc);
            if (collarForeground != null)
            {
                dc.PushClip(collarForeground);
                dc.DrawImage(backplate, artboard);
                dc.Pop();
            }

            // An affine sleeve deformation pins the shoulder while carrying the wrist and mouse.
            double dx = MouseOffset.X;
            double dy = MouseOffset.Y;
            double shoulderY = MouseShoulderY, wristY = MouseWristY, reach = wristY - shoulderY;
            var mouseMatrix = new Matrix(1, 0, dx / reach, 1 + dy / reach,
                -shoulderY * dx / reach, -shoulderY * dy / reach);
            // The top of the shoulder belongs to the body and never moves.
            dc.PushClip(mouseShoulderClip);
            dc.DrawImage(mouseArm, artboard);
            dc.Pop();
            dc.PushTransform(new MatrixTransform(mouseMatrix));
            // The sleeve runs a little past the wrist line, under the hand piece, so no hairline shows.
            dc.PushClip(mouseSleeveClip);
            dc.DrawImage(mouseArm, artboard);
            dc.Pop(); dc.Pop();
            // The shoulder's outline, bent with the sleeve below the shoulder line.
            if (MouseShoulderLine.Length > 0)
            {
                var shoulder = new StreamGeometry();
                using (var g = shoulder.Open())
                {
                    for (int i = 0; i < MouseShoulderLine.Length; i += 2)
                    {
                        var point = new Point(MouseShoulderLine[i], MouseShoulderLine[i + 1]);
                        if (point.Y > shoulderY) point = mouseMatrix.Transform(point);
                        if (i == 0) g.BeginFigure(point, false, false);
                        else g.LineTo(point, true, true);
                    }
                }
                dc.DrawGeometry(null, ShoulderPen, shoulder);
            }
            // The hand and mouse keep their shape while following the wrist. Leaning and click
            // squash are both measured from the wrist line, so they stay joined to the sleeve.
            dc.PushTransform(new MatrixTransform(new Matrix(1, 0, mouseLean.Value, 1 - mouseSquash,
                dx - wristY * mouseLean.Value, dy + wristY * mouseSquash)));
            dc.PushClip(MouseHandClip);
            dc.DrawImage(mouseArm, artboard);
            dc.Pop(); dc.Pop();
            double pop = MouseClickPop;
            double rays = tune.MouseClickLines.Value;
            if (pop >= 0 && rays > 0)
            {
                // The strokes shoot outward, stretching first and shrinking as they fade.
                double spread = 1 - (1 - pop) * (1 - pop);
                double inner = 84 + 48 * spread, outer = 104 + 50 * Math.Min(1, spread * 1.6);
                dc.PushOpacity(pop < .65 ? 1 : (1 - pop) / .35);
                foreach (double ray in ClickRays)
                {
                    double radians = ray * Math.PI / 180;
                    var direction = new Vector(Math.Cos(radians), Math.Sin(radians));
                    Point origin = ClickOrigin + new Vector(dx, dy);
                    dc.DrawLine(ClickPen, origin + direction * inner, origin + direction * (inner + (outer - inner) * rays));
                }
                dc.Pop();
            }

            Matrix headMatrix = HeadPlacement();
            if (neckRegion != null)
            {
                dc.PushClip(neckRegion);
                dc.DrawImage(head, artboard);
                dc.Pop();
            }
            dc.PushTransform(new MatrixTransform(headMatrix));
            // With the lock at rest the head is one untouched piece.
            bool lockMoves = ahogeSway.Value != 0 || ahogeStretch.Value != 0 || ahogeTip.Value != 0;
            dc.PushClip(lockMoves ? headWithoutNeckOrAhoge : headWithoutNeck);
            dc.DrawImage(head, artboard);
            dc.Pop();
            if (lockMoves) DrawAhoge(dc);
            if (blink > .001)
            {
                DrawBlinkEye(dc, leftEye, leftEyeBox, false);
                DrawBlinkEye(dc, rightEye, rightEyeBox, true);
            }
            if (purse.Value > .01)
            {
                double pressed = Clamp(purse.Value, 0, 1.15);
                dc.PushOpacity(Math.Min(1, pressed * 6));
                dc.DrawEllipse(mouthSkin, null, MouthCenter, MouthCover.X, MouthCover.Y);
                dc.Pop();
                dc.PushTransform(new ScaleTransform(1 - .24 * pressed, 1 + .07 * pressed, MouthCenter.X, MouthCenter.Y - 8));
                dc.PushClip(pursedMouthRegion);
                dc.PushOpacityMask(mouthFeather);
                dc.DrawImage(head, artboard);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
            if (mouthShown > .018)
            {
                // A local patch retains the hand-drawn mouth. Vertical scaling makes a
                // continuous opening rather than switching between two whole face images.
                // Past fully open (a yawn) it grows tall and a little narrower.
                double yawning = Math.Max(0, mouthShown - 1);
                dc.PushTransform(new ScaleTransform(1 - .14 * yawning, .43 + mouthShown * .57 + .3 * yawning, MouthPivot.X, MouthPivot.Y));
                DrawPatch(dc, mouthRegion, Math.Min(1, mouthShown * 9));
                dc.Pop();
            }
            if (yawnTear > .01)
            {
                // The tear after a yawn sits at the outer corner of the near eye, on the head.
                dc.PushTransform(new ScaleTransform(.5 * yawnTear, .5 * yawnTear, YawnTearSpot.X, YawnTearSpot.Y));
                dc.PushTransform(new TranslateTransform(YawnTearSpot.X, YawnTearSpot.Y));
                dc.DrawGeometry(SweatFill, SymbolPen, SweatShape);
                dc.Pop(); dc.Pop();
            }
            dc.Pop();
            dc.Pop();
            DrawSymbols(dc);
        }

        private void DrawBlinkEye(DrawingContext dc, Geometry clip, Rect box, bool far)
        {
            // Iris pixels retain their shape: the eyelids cover them instead of squashing
            // the pupils or crossfading ghost irises. The closed state comes from the artwork,
            // laid in through a soft-edged patch (box is the patch with its soft edge).
            double[] curve = far ? FarLidCurve : NearLidCurve, under = far ? FarLidLower : NearLidLower;
            if (curve.Length == 12 && under.Length == 6 && blink < .96)
            {
                DrawCurvedLids(dc, clip, curve, under, far ? FarLidPen : NearLidPen);
                return;
            }
            dc.PushClip(new RectangleGeometry(box));
            dc.DrawImage(closedEyes, artboard);
            dc.Pop();
            dc.PushClip(clip);
            if (blink < .96)
            {
                double[] lidLine = far ? FarLid : NearLid;
                double top = lidLine[0] + lidLine[1] * blink;
                double bottom = lidLine[2] - lidLine[3] * blink;
                Point start = new Point(lidLine[4], top + lidLine[5]);
                Point control = new Point(lidLine[6], top + lidLine[7]);
                Point end = new Point(lidLine[8], top + lidLine[9]);
                var opening = new StreamGeometry();
                using (var g = opening.Open())
                {
                    g.BeginFigure(start, true, true);
                    g.QuadraticBezierTo(control, end, true, false);
                    g.LineTo(new Point(end.X, bottom), true, false);
                    g.LineTo(new Point(start.X, bottom), true, false);
                }
                dc.PushClip(opening);
                dc.DrawImage(head, artboard);
                dc.Pop();
                var lid = new StreamGeometry();
                using (var g = lid.Open())
                {
                    g.BeginFigure(start, false, false);
                    g.QuadraticBezierTo(control, end, true, false);
                }
                dc.DrawGeometry(null, far ? FarLidPen : NearLidPen, lid);
            }
            dc.Pop();
        }

        // The lids as two curves that meet on the closed lash line. The open eye stays as it is
        // between them; the closed artwork (plain skin there, until the very end) shows above
        // the upper one and below the lower one. The upper curve is drawn as the lash line.
        private void DrawCurvedLids(DrawingContext dc, Geometry clip, double[] curve, double[] under, Pen pen)
        {
            double t = blink;
            Point start = Between(curve, 0, curve, 6, t), control = Between(curve, 2, curve, 8, t), end = Between(curve, 4, curve, 10, t);
            Point lowStart = Between(under, 0, curve, 6, t), lowControl = Between(under, 2, curve, 8, t), lowEnd = Between(under, 4, curve, 10, t);
            Rect all = Rect.Inflate(clip.Bounds, 20, 20);
            var covered = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var g = covered.Open())
            {
                g.BeginFigure(new Point(all.Left, start.Y), true, true);
                g.LineTo(start, true, false);
                g.QuadraticBezierTo(control, end, true, false);
                g.LineTo(new Point(all.Right, end.Y), true, false);
                g.LineTo(all.TopRight, true, false);
                g.LineTo(all.TopLeft, true, false);
                // The lower lid reaches out sideways at the height of its ends as well, so that
                // nothing of the open eye is left beside the corners when the lids meet.
                g.BeginFigure(new Point(all.Left, lowStart.Y), true, true);
                g.LineTo(lowStart, true, false);
                g.QuadraticBezierTo(lowControl, lowEnd, true, false);
                g.LineTo(new Point(all.Right, lowEnd.Y), true, false);
                g.LineTo(all.BottomRight, true, false);
                g.LineTo(all.BottomLeft, true, false);
            }
            dc.PushClip(clip);
            dc.PushClip(covered);
            dc.DrawImage(closedEyes, artboard);
            dc.Pop();
            var lid = new StreamGeometry();
            using (var g = lid.Open())
            {
                g.BeginFigure(start, false, false);
                g.QuadraticBezierTo(control, end, true, false);
            }
            dc.DrawGeometry(null, pen, lid);
            dc.Pop();
        }

        private static Point Between(double[] from, int at, double[] to, int toAt, double t)
        {
            return new Point(from[at] + (to[toAt] - from[at]) * t, from[at + 1] + (to[toAt + 1] - from[at + 1]) * t);
        }

        private void DrawPatch(DrawingContext dc, Geometry clip, double opacity)
        {
            dc.PushOpacity(Clamp(opacity, 0, 1));
            dc.PushClip(clip);
            dc.DrawImage(expression, artboard);
            dc.Pop(); dc.Pop();
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return lo;
            return Math.Max(lo, Math.Min(hi, v));
        }

        private static double Smooth(double x)
        {
            x = Clamp(x, 0, 1);
            return x * x * (3 - 2 * x);
        }

        private sealed class Spring
        {
            public double Value;
            private double velocity;
            public double Velocity { get { return velocity; } }
            public void Floor(double min, double restitution)
            {
                if (Value >= min) return;
                Value = min;
                if (velocity < 0) velocity = -velocity * restitution;
            }
            public void Constrain(double min,double max)
            {
                double constrained=Clamp(Value,min,max);
                if(constrained!=Value || min==max)velocity=0;
                Value=constrained;
            }
            public void Step(double target, double dt, double frequency, double damping)
            {
                // Substeps keep motion stable across slow frames / resizing / menu interaction.
                int steps = Math.Max(1, (int)Math.Ceiling(dt / .006));
                double h = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    velocity += ((target - Value) * frequency * frequency - 2 * damping * frequency * velocity) * h;
                    Value += velocity * h;
                }
                if (Math.Abs(Value - target) < .00001 && Math.Abs(velocity) < .00001)
                {
                    Value = target;
                    velocity = 0;
                }
            }
        }
    }
}
