using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace SumikkoCompanion
{
    // Everything that belongs to one character's artwork rather than to the motion: where
    // the eyes and mouth are, where the sleeves bend, where symbols appear, and so on. All
    // coordinates are artboard pixels (1469 x 1071).
    //
    // The values built in here are those of the sample character in assets/. A character
    // folder may hold a character.txt that replaces any of them:
    //
    //     # comment
    //     name = 表示名
    //     keyHinge = 714, 727
    //     keyHandEdge = 941,650, 936,664,
    //         933,673, 926,674.5        (a line without "=" continues the list)
    //     tune.hair.ahoge = 0           (the built-in value of a motion setting)
    //
    // Anything not named keeps the built-in value. Mistakes are collected in Problems and
    // the built-in value is kept for that key.
    public sealed class CharacterProfile
    {
        public const string FileName = "character.txt";
        public static readonly string[] ImageFiles = { "base.png", "keyboard-arm.png", "mouse-arm.png", "head.png", "face-expression.png" };
        // Instead of face-expression.png (closed eyes and open mouth on one head) a character
        // may bring the two as separate heads; each is used when it is there.
        public const string WholeFaceFile = "face-expression.png", ClosedEyesFile = "eyes-closed.png", OpenMouthFile = "mouth-open.png";

        // character.txt may name the two images itself (closedEyesImage = ..., openMouthImage = ...).
        public string ClosedEyesImage { get; private set; }
        public string OpenMouthImage { get; private set; }

        public string ClosedEyesSource(string directory)
        {
            return String.IsNullOrEmpty(ClosedEyesImage) ? ClosedEyesIn(directory) : ClosedEyesImage;
        }

        public string OpenMouthSource(string directory)
        {
            return String.IsNullOrEmpty(OpenMouthImage) ? OpenMouthIn(directory) : OpenMouthImage;
        }

        public static string ClosedEyesIn(string directory)
        {
            return File.Exists(Path.Combine(directory, ClosedEyesFile)) ? ClosedEyesFile : WholeFaceFile;
        }

        public static string OpenMouthIn(string directory)
        {
            return File.Exists(Path.Combine(directory, OpenMouthFile)) ? OpenMouthFile : WholeFaceFile;
        }

        private const int AnyNumbers = -1, PointList = 0;

        private sealed class Entry
        {
            public string Key;
            // AnyNumbers, PointList (x,y pairs) or the exact number of values.
            public int Count;
            public int MinPoints;
            public bool MayBeEmpty;
            public double[] Value;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, double>> tuning = new List<KeyValuePair<string, double>>();
        private readonly List<string> problems = new List<string>();

        // Shown in the menu; empty when the file gives none.
        public string Name { get; private set; }
        public IList<string> Problems { get { return problems.AsReadOnly(); } }

        public CharacterProfile()
        {
            Name = "";
            // ---- Head and face ----
            Points("leftEye", 3, false, 509,351, 665,351, 673,480, 530,486);
            Points("rightEye", 3, false, 714,303, 800,303, 801,432, 717,440);
            // The lid while it closes: top of the open eye, how far the top travels, bottom of
            // the opening, how far the bottom rises; then the lid line's start, control and end
            // points, each as x and a height relative to the travelling top.
            Exact("nearLid", 371,63, 483,35, 523,18, 583,-12, 660,1);
            Exact("farLid", 320,74, 435,25, 723,27, 751,-4, 790,-6);
            Exact("lidWidth", 9, 8);
            // A second way to draw the closing lid, for eyes whose closed lash line curves the
            // other way than the open one (both empty = the way above). The line of the upper
            // lid as a curve (start, control, end) with the eye open and with it closed, and the
            // curve of the lower lid with the eye open (it ends on the closed line too). The lid
            // line bends from the one into the other, and the closed artwork shows only where a
            // lid has passed, so no part of the closed lashes appears before the lid gets there.
            Points("nearLidCurve", 6, true);
            Points("nearLidLower", 3, true);
            Points("farLidCurve", 6, true);
            Points("farLidLower", 3, true);
            // The open mouth: centre and radii of the patch taken from face-expression.png,
            // and the point it is stretched from.
            Exact("mouthRegion", 709,477, 36,32);
            Exact("mouthPivot", 709,462);
            // Pursed lips: centre of the mouth, radii of the narrowed copy, radii of the skin laid over the drawn mouth.
            Exact("mouthCenter", 710,466);
            Exact("pursedMouth", 31,19);
            Exact("mouthCover", 40,27);
            Exact("skinColor", 250,221,209);
            Exact("yawnTear", 543,487);
            Exact("headPivot", 601,552);
            // How far the head may rise above its place in the artwork (0 when the neck is cut out of head.png).
            Exact("headRise", 0);
            // The part of head.png that stays with the body (empty when head.png has no neck).
            Points("neck", 3, true, 553,549, 565,547.5, 575,546, 585,543.5, 589,541.5, 602,543, 604,550, 612,553, 622,556,
                622,560, 620,566, 617,574, 614,583, 605,588, 595,588, 583,584, 572,576, 561,566, 556,555);
            // The free-standing lock of hair. The first two points of the base piece are the
            // root line (left end, right end); the first and last points of the tip piece lie
            // on the line the tip bends about. Both empty when there is no such lock.
            Points("ahogeBase", 3, true, 358,72, 436,61, 437.5,56, 437.5,0, 318,0, 318,66.6, 324,67, 340,67.5, 348,69, 354,70.5);
            Points("ahogeTip", 3, true, 318,0, 224,0, 224,90, 240,84, 252,78, 260,73, 276,67.5, 292,65.5, 308,66, 318,66.6);

            // ---- Keyboard arm ----
            Exact("keyHinge", 714,727);
            Exact("keyCuffAxis", -.335,.942);
            Exact("keyForearm", 160);
            Exact("keyPalm", 945.7,734.7);
            Exact("keyKnuckle", 915,735);
            Points("keyHandEdge", 2, false,
                941,650, 936,664, 933,673, 926,674.5, 920,677, 915,680, 909,684, 904,688, 900,692, 897,696, 894,700,
                892,704, 890,708, 887,712, 886,716, 885,720, 884,724, 883,728, 883,732, 882,736, 883,740,
                883,744, 884,748, 886,752, 888,756, 892,765, 896,775, 901,790);
            // Left, top, right, bottom of the area the arm's pieces are cut from.
            Exact("keyArmBox", 540,480, 1075,830);
            Points("collar", 3, true, 470,525, 640,525, 654,548, 671,556, 684,564, 696,572, 705,580,
                713,588, 720,596, 727,604, 732,612, 737,620, 742,628, 747,636, 751,644, 755,652,
                758,660, 761,668, 764,676, 767,684, 770,692, 772,700, 774,708, 776,716, 777,724,
                779,732, 780,740, 783,748, 787,756, 790,764, 792,772, 797,800, 700,806,
                561,792, 481,661);
            // Where the hand strikes: from/to along the rows and across them, each with the
            // direction one unit moves the hand; then the limits of the sideways reach.
            Exact("keyRows", -40,75, -.936,.351);
            Exact("keyDepth", -15,60, .983,.183);
            Exact("keyReachX", -72,96);
            // The hand in the air: how high and where (sideways, toward the body) it waves hello,
            // and how high it stays flung up after a long phrase is sent.
            Exact("waveHand", 124, 34,-6);
            Exact("triumphLift", 98);

            // ---- Mouse arm ----
            Exact("mouseShoulderY", 580);
            Exact("mouseWristY", 840);
            // A missing piece of outline at the shoulder, drawn in; and the part of the sleeve
            // cap that is cut away. Both empty when the artwork's outline is complete.
            Points("mouseShoulderLine", 2, true, 371,607, 380,600, 390,595, 402,590, 414,585.5, 426,581, 436,577.5, 443,573, 449,569);
            Points("mouseShoulderCut", 3, true, 380,600, 390,595, 402,590, 414,585.5, 426,581, 436,577.5,
                446,576.5, 460,567.5, 480,557.5, 520,545, 520,520, 355,520,
                355,612, 363.5,609.5, 367.5,605, 372.5,601, 377.5,597);
            Exact("clickOrigin", 600,940);
            Add("clickRays", AnyNumbers, 0, true, -4, 21, 46);

            // ---- Acting and the rest ----
            // Centre and radii of the head (for petting); where symbols appear.
            Exact("headArea", 520,300, 350,300);
            Exact("symbolAnchor", 880,215);
            // Left, top and edge of the square of head.png used as the notification-area icon.
            Exact("trayIcon", 200,0, 680);
            // How far the mouse hand travels: left, right, top, bottom.
            Exact("mouseRange", -120,120, -45,5);
            Exact("outlineColor", 33,24,19);
        }

        private void Exact(string key, params double[] value)
        {
            Add(key, value.Length, 0, false, value);
        }

        private void Points(string key, int minPoints, bool mayBeEmpty, params double[] value)
        {
            Add(key, PointList, minPoints, mayBeEmpty, value);
        }

        private void Add(string key, int count, int minPoints, bool mayBeEmpty, params double[] value)
        {
            var entry = new Entry { Key = key, Count = count, MinPoints = minPoints, MayBeEmpty = mayBeEmpty, Value = value };
            entries.Add(entry);
            byKey.Add(key, entry);
        }

        // The character in this folder: the built-in values, changed by its character.txt if it has one.
        public static CharacterProfile Load(string directory)
        {
            var profile = new CharacterProfile();
            string path = Path.Combine(directory, FileName);
            try
            {
                if (File.Exists(path)) profile.Read(File.ReadAllLines(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                profile.problems.Add(FileName + " を読み込めませんでした: " + e.Message);
            }
            return profile;
        }

        public void Read(IEnumerable<string> lines)
        {
            string key = null;
            var numbers = new List<double>();
            bool broken = false;
            foreach (string raw in lines)
            {
                string line = raw;
                int comment = line.IndexOf('#');
                if (comment >= 0) line = line.Substring(0, comment);
                line = line.Trim();
                if (line.Length == 0) continue;
                int equals = line.IndexOf('=');
                string rest = line;
                if (equals >= 0)
                {
                    Finish(key, numbers, broken);
                    numbers.Clear();
                    broken = false;
                    key = line.Substring(0, equals).Trim();
                    rest = line.Substring(equals + 1).Trim();
                    if (key == "name" || key == "closedEyesImage" || key == "openMouthImage")
                    {
                        // File names only: an image always comes from the folder of the character.
                        if (key == "name") Name = rest;
                        else if (rest.Length == 0 || rest != Path.GetFileName(rest)) problems.Add(key + ": 画像のファイル名だけを書きます。");
                        else if (key == "closedEyesImage") ClosedEyesImage = rest;
                        else OpenMouthImage = rest;
                        key = null;
                        continue;
                    }
                }
                else if (key == null)
                {
                    problems.Add("「" + line + "」は「名前 = 値」の形になっていません。");
                    continue;
                }
                foreach (string part in rest.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    double number;
                    if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
                        !double.IsNaN(number) && !double.IsInfinity(number)) numbers.Add(number);
                    else if (!broken)
                    {
                        broken = true;
                        problems.Add(key + ": 「" + part + "」は数値として読めません。");
                    }
                }
            }
            Finish(key, numbers, broken);
        }

        private void Finish(string key, List<double> numbers, bool broken)
        {
            if (key == null || broken) return;
            if (key.StartsWith("tune.", StringComparison.Ordinal))
            {
                string name = key.Substring(5);
                if (numbers.Count != 1) problems.Add(key + ": 数値を 1 個指定します。");
                else if (new MotionTuning().Find(name) == null) problems.Add(key + ": その名前の動きの設定はありません。");
                else tuning.Add(new KeyValuePair<string, double>(name, numbers[0]));
                return;
            }
            Entry entry;
            if (!byKey.TryGetValue(key, out entry))
            {
                problems.Add(key + ": 知らない名前です（つづりを確かめてください）。");
                return;
            }
            int count = numbers.Count;
            if (entry.Count > 0 && count != entry.Count)
            {
                problems.Add(key + ": 数値を " + entry.Count + " 個指定します（" + count + " 個ありました）。");
                return;
            }
            if (entry.Count == PointList)
            {
                if (count % 2 != 0)
                {
                    problems.Add(key + ": x と y の組で指定します（数値が奇数個です）。");
                    return;
                }
                if (count == 0 ? !entry.MayBeEmpty : count / 2 < entry.MinPoints)
                {
                    problems.Add(key + ": 点を " + entry.MinPoints + " 個以上指定します。");
                    return;
                }
            }
            entry.Value = numbers.ToArray();
        }

        // Every value in a fixed order and form, for comparing two profiles.
        public string Describe()
        {
            var text = new StringBuilder();
            foreach (Entry entry in entries)
            {
                text.Append(entry.Key).Append(" =");
                foreach (double number in entry.Value) text.Append(' ').Append(number.ToString("R", CultureInfo.InvariantCulture));
                text.Append('\n');
            }
            foreach (KeyValuePair<string, double> pair in tuning)
                text.Append("tune.").Append(pair.Key).Append(" = ").Append(pair.Value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            return text.ToString();
        }

        // The character's own built-in values of the motion settings (e.g. no sway for a
        // character without a free-standing lock of hair).
        public void ApplyTuning(MotionTuning target)
        {
            foreach (KeyValuePair<string, double> pair in tuning) target.SetDefault(pair.Key, pair.Value);
        }

        public double[] Numbers(string key)
        {
            return (double[])byKey[key].Value.Clone();
        }

        public double Number(string key)
        {
            return byKey[key].Value[0];
        }

        public Point Point(string key)
        {
            double[] value = byKey[key].Value;
            return new Point(value[0], value[1]);
        }

        public Color Color(string key)
        {
            double[] value = byKey[key].Value;
            return System.Windows.Media.Color.FromRgb(Channel(value[0]), Channel(value[1]), Channel(value[2]));
        }

        private static byte Channel(double value)
        {
            return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
        }

        public MouseMotionRange MouseRange
        {
            get
            {
                double[] value = byKey["mouseRange"].Value;
                return new MouseMotionRange(value[0], value[1], value[2], value[3]);
            }
        }
    }

    public sealed class CharacterEntry
    {
        // "" for the character in assets/, otherwise the name of its folder under characters/.
        public string Id, Name, Directory;
    }

    // The characters that can be chosen: the one in assets/, then every folder under
    // characters/ that holds the five images.
    public static class CharacterCatalog
    {
        public const string FolderName = "characters";

        public static List<CharacterEntry> Find(string projectRoot)
        {
            var found = new List<CharacterEntry>();
            string builtIn = Path.Combine(projectRoot, "assets");
            found.Add(new CharacterEntry { Id = "", Directory = builtIn, Name = NameOf(builtIn, "見本") });
            try
            {
                string folder = Path.Combine(projectRoot, FolderName);
                if (!System.IO.Directory.Exists(folder)) return found;
                string[] directories = System.IO.Directory.GetDirectories(folder);
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                foreach (string directory in directories)
                {
                    string id = Path.GetFileName(directory);
                    // The id is written to settings.txt as part of a "key=value" line.
                    if (id.Length == 0 || id.IndexOf('=') >= 0 || !Complete(directory)) continue;
                    found.Add(new CharacterEntry { Id = id, Directory = directory, Name = NameOf(directory, id) });
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return found;
        }

        public static bool Complete(string directory)
        {
            foreach (string image in CharacterProfile.ImageFiles)
                if (image != CharacterProfile.WholeFaceFile && !File.Exists(Path.Combine(directory, image))) return false;
            CharacterProfile profile = CharacterProfile.Load(directory);
            return File.Exists(Path.Combine(directory, profile.ClosedEyesSource(directory))) &&
                File.Exists(Path.Combine(directory, profile.OpenMouthSource(directory)));
        }

        private static string NameOf(string directory, string fallback)
        {
            string name = CharacterProfile.Load(directory).Name;
            return string.IsNullOrEmpty(name) ? fallback : name;
        }
    }
}
