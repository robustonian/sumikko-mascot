using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SumikkoCompanion;

// Scratch tool: renders keyboard-arm poses and scripted typing with the app's own renderer.
internal static class Harness
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void SetSpring(CharacterView view, string name, double value)
    {
        object spring = typeof(CharacterView).GetField(name, Private).GetValue(view);
        spring.GetType().GetField("Value").SetValue(spring, value);
    }

    private static double GetSpring(CharacterView view, string name)
    {
        object spring = typeof(CharacterView).GetField(name, Private).GetValue(view);
        return (double)spring.GetType().GetField("Value").GetValue(spring);
    }

    private static void Save(CharacterView view, string file, Int32Rect crop, double scale)
    {
        view.InvalidateVisual();
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1469, 1071, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var dc = backdrop.RenderOpen())
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(58, 60, 72)), null, new Rect(0, 0, 1469, 1071));
        bitmap.Render(backdrop);
        bitmap.Render(view);
        BitmapSource result = new CroppedBitmap(bitmap, crop);
        if (scale != 1) result = new TransformedBitmap(result, new ScaleTransform(scale, scale));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(result));
        using (var stream = File.Create(file)) encoder.Save(stream);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        string mode = args[2];
        Directory.CreateDirectory(output);
        // --character=<folder under characters/> draws that character instead of the one in assets/.
        string artwork = Path.Combine(root, "assets");
        foreach (string arg in args)
            if (arg.StartsWith("--character=")) artwork = Path.Combine(root, "characters", arg.Substring(12));
        // (Taken out again, so it never counts as one of the positional arguments.)
        args = Array.FindAll(args, a => !a.StartsWith("--character="));
        var view = new CharacterView(artwork);
        foreach (string problem in view.Profile.Problems) Console.Error.WriteLine("character.txt: " + problem);
        view.Measure(new Size(1469, 1071));
        view.Arrange(new Rect(0, 0, 1469, 1071));
        if (args.Length > 4 && mode != "scene") view.MotionStrength = double.Parse(args[4], CultureInfo.InvariantCulture);
        // Every mode but "scene" was written for the plain motion, without acting.
        view.Scenes = mode == "scene" || Array.IndexOf(args, "--scenes") >= 0;
        foreach (string arg in args)
            if (arg.StartsWith("set:")) { string[] kv = arg.Substring(4).Split('='); view.Tuning.Find(kv[0]).Set(double.Parse(kv[1], CultureInfo.InvariantCulture)); }
        view.Tick(0, 1.0 / 60, false, false, 0, 0, 0, false, false);
        var inv = CultureInfo.InvariantCulture;
        if (mode == "pose")
        {
            // name:x:y:lift:flick ...
            foreach (string spec in args[3].Split(';'))
            {
                string[] p = spec.Split(':');
                SetSpring(view, "reachX", double.Parse(p[1], inv));
                SetSpring(view, "reachY", double.Parse(p[2], inv));
                SetSpring(view, "lift", double.Parse(p[3], inv));
                SetSpring(view, "flick", double.Parse(p[4], inv));
                Save(view, Path.Combine(output, p[0] + ".png"), new Int32Rect(560, 440, 740, 460), 1);
            }
            return;
        }
        if (mode == "face")
        {
            // name:field=value,field=value;...  (private double fields of the view)
            foreach (string spec in args[3].Split(';'))
            {
                string[] p = spec.Split(':');
                foreach (string pair in p[1].Split(','))
                {
                    string[] kv = pair.Split('=');
                    var field = typeof(CharacterView).GetField(kv[0], Private);
                    if (field.FieldType == typeof(double)) field.SetValue(view, double.Parse(kv[1], inv));
                    else SetSpring(view, kv[0], double.Parse(kv[1], inv));
                }
                Save(view, Path.Combine(output, p[0] + ".png"), args.Length > 5 ? new Int32Rect(430, 440, 360, 220) : new Int32Rect(440, 260, 420, 300), 1);
            }
            return;
        }
        if (mode == "hold")
        {
            // One press at 0.2 s, then the key stays down: repeats from 0.6 s every 75 ms until 2.2 s.
            var line = new System.Text.StringBuilder();
            double nextRepeat = .6;
            for (int frame = 1; frame <= 180; frame++)
            {
                double t = frame / 60.0;
                bool first = frame == 12, repeat = false;
                if (t >= nextRepeat && t < 2.2) { repeat = true; nextRepeat += .075; }
                view.Tick(t, 1.0 / 60, first || repeat, false, 0, 0, 0, false, false, false, repeat);
                line.Append(GetSpring(view, "lift").ToString("0", inv)).Append(' ');
                Save(view, Path.Combine(output, "h" + frame.ToString("D4") + ".png"), new Int32Rect(560, 440, 740, 460), .5);
            }
            Console.WriteLine(line.ToString());
            return;
        }
        if (mode == "blinks")
        {
            // 60 s: idle, a typing burst from 30 s to 36 s, idle again. Prints when each blink starts.
            double before = 0;
            var line = new System.Text.StringBuilder();
            for (int frame = 1; frame <= 3600; frame++)
            {
                double t = frame / 60.0;
                bool key = t >= 30 && t < 36 && frame % 8 == 0;
                view.Tick(t, 1.0 / 60, key, false, 0, 0, 0, false, false);
                double now = (double)typeof(CharacterView).GetField("blink", Private).GetValue(view);
                if (before == 0 && now > 0) line.Append(t.ToString("0.00", inv)).Append(' ');
                before = now;
            }
            Console.WriteLine(line.ToString());
            return;
        }
        if (mode == "idle")
        {
            // Nothing pressed for 24 s, then one emphatic key; logs the head and saves a frame every 0.5 s.
            using (var log = new StreamWriter(Path.Combine(output, "head.csv")))
            {
                log.WriteLine("t,angle,x,y,purse,blink");
                for (int frame = 1; frame <= 1560; frame++)
                {
                    double t = frame / 60.0;
                    view.Tick(t, 1.0 / 60, frame == 1450, false, 0, 0, 0, false, false, true, false);
                    log.WriteLine(string.Format(inv, "{0:0.000},{1:0.000},{2:0.00},{3:0.00},{4:0.00},{5:0.00}", t,
                        GetSpring(view, "headAngle"), GetSpring(view, "headX"), GetSpring(view, "headY"), GetSpring(view, "purse"),
                        (double)typeof(CharacterView).GetField("blink", Private).GetValue(view)));
                    if (frame % 2 == 0)
                        Save(view, Path.Combine(output, "i" + frame.ToString("D4") + ".png"), new Int32Rect(0, 0, 1468, 1070), .5);
                }
            }
            return;
        }
        if (mode == "mouse")
        {
            // Quick pointer jumps and one click, with the range saved on this PC.
            view.MouseRange = new MouseMotionRange(-65, 115, -82, 8);
            for (int frame = 1; frame <= 150; frame++)
            {
                double t = frame / 60.0;
                double px = t < .3 ? -.7 : (t < .9 ? .7 : (t < 1.5 ? -.6 : .2));
                double py = t < .3 ? 0 : (t < .9 ? -.3 : (t < 1.5 ? .6 : 0));
                bool click = t >= 1.9 && t < 2.1;
                view.Tick(t, 1.0 / 60, false, click, px, py, 0, false, false);
                Save(view, Path.Combine(output, "m" + frame.ToString("D4") + ".png"), new Int32Rect(0, 0, 1468, 1070), .5);
            }
            return;
        }
        if (mode == "scene")
        {
            // scene "seconds;time:command[:args];..." [every] [x,y,w,h] [scale]
            //   k = key, e = Enter, c = click (held 0.1 s), h:seconds = hold a key that long,
            //   p:x:y = pointer on the screen (-1..1), a:x:y = pointer on the artwork (a:off = unknown),
            //   g = greet, set:key=value = a tuning value, reduced:0|1, strength:value
            string[] script = args[3].Split(';');
            int every = args.Length > 4 ? int.Parse(args[4]) : 2;
            var crop = new Int32Rect(0, 0, 1468, 1070);
            if (args.Length > 5) { string[] c = args[5].Split(','); crop = new Int32Rect(int.Parse(c[0]), int.Parse(c[1]), int.Parse(c[2]), int.Parse(c[3])); }
            double size = args.Length > 6 ? double.Parse(args[6], inv) : .5;
            int frameCount = (int)(double.Parse(script[0], inv) * 60);
            double px = 0, py = 0, clickUntil = -1, holdFrom = -1, holdUntil = -1, nextRepeat = 0;
            bool reduced = false;
            int at = 1;
            using (var log = new StreamWriter(Path.Combine(output, "scene.csv")))
            {
                log.WriteLine("t,headY,headAngle,lift,flick,blink,mouth,drop,symbols,ahogeSway,ahogeStretch,ahogeTip");
                for (int frame = 1; frame <= frameCount; frame++)
                {
                    double t = frame / 60.0;
                    bool key = false, enter = false, repeat = false;
                    while (at < script.Length && double.Parse(script[at].Split(':')[0], inv) <= t)
                    {
                        string[] e = script[at++].Split(':');
                        if (e[1] == "k") key = true;
                        else if (e[1] == "e") key = enter = true;
                        else if (e[1] == "c") clickUntil = t + .1;
                        else if (e[1] == "h") { key = true; holdFrom = t; holdUntil = t + double.Parse(e[2], inv); nextRepeat = t + .4; }
                        else if (e[1] == "p") { px = double.Parse(e[2], inv); py = double.Parse(e[3], inv); }
                        else if (e[1] == "a") view.PointerOnArt = e[2] == "off" ? new Point(double.NaN, double.NaN) : new Point(double.Parse(e[2], inv), double.Parse(e[3], inv));
                        else if (e[1] == "g") view.Greet();
                        else if (e[1] == "reduced") reduced = e[2] == "1";
                        else if (e[1] == "strength") view.MotionStrength = double.Parse(e[2], inv);
                        else if (e[1] == "set") { string[] kv = e[2].Split('='); view.Tuning.Find(kv[0]).Set(double.Parse(kv[1], inv)); view.TuningChanged(); }
                    }
                    if (!key && t < holdUntil && t >= nextRepeat) { key = repeat = true; nextRepeat += .075; }
                    view.Tick(t, 1.0 / 60, key, t < clickUntil, px, py, 0, false, reduced, enter, repeat);
                    log.WriteLine(string.Format(inv, "{0:0.000},{1:0.00},{2:0.00},{3:0.0},{4:0.0},{5:0.00},{6:0.00},{7:0.0},{8},{9:0.000},{10:0.000},{11:0.000}", t,
                        GetSpring(view, "headY"), GetSpring(view, "headAngle"), GetSpring(view, "lift"), GetSpring(view, "flick"),
                        (double)typeof(CharacterView).GetField("blink", Private).GetValue(view),
                        (double)typeof(CharacterView).GetField("mouthShown", Private).GetValue(view),
                        GetSpring(view, "thump"), (int)typeof(CharacterView).GetProperty("SymbolCount", Private).GetValue(view, null),
                        GetSpring(view, "ahogeSway"), GetSpring(view, "ahogeStretch"), GetSpring(view, "ahogeTip")));
                    if (frame % every == 0) Save(view, Path.Combine(output, "s" + frame.ToString("D5") + ".png"), crop, size);
                }
            }
            return;
        }
        // sim: events "time:column:row:accent;..." (column "?" = unknown position)
        string[] events = args[3].Split(';');
        double duration = double.Parse(events[0], inv);
        bool frames = Array.IndexOf(args, "--frames") >= 0;
        bool demo = Array.IndexOf(args, "--demo") >= 0;
        using (var log = new StreamWriter(Path.Combine(output, "trace.csv")))
        {
            log.WriteLine("t,x,y,lift,flick,nod");
            int next = 1;
            int total = (int)(duration * 60);
            for (int frame = 1; frame <= total; frame++)
            {
                double t = frame / 60.0;
                bool down = false, accent = false;
                double column = double.NaN, row = double.NaN;
                while (next < events.Length && double.Parse(events[next].Split(':')[0], inv) <= t)
                {
                    string[] e = events[next].Split(':');
                    down = true;
                    if (e[1] != "?") { column = double.Parse(e[1], inv); row = double.Parse(e[2], inv); }
                    accent = e.Length > 3 && e[3] == "1";
                    next++;
                }
                view.Tick(t, 1.0 / 60, down, false, 0, 0, 0, demo, false, accent);
                log.WriteLine(string.Format(inv, "{0:0.0000},{1:0.00},{2:0.00},{3:0.00},{4:0.00},{5:0.000}", t,
                    GetSpring(view, "reachX"), GetSpring(view, "reachY"), GetSpring(view, "lift"),
                    GetSpring(view, "flick"), GetSpring(view, "nod")));
                if (frames)
                    Save(view, Path.Combine(output, "f" + frame.ToString("D4") + ".png"), Array.IndexOf(args, "--full") >= 0 ? new Int32Rect(0, 0, 1468, 1070) : new Int32Rect(560, 440, 740, 460), .5);
            }
        }
    }
}
