using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SumikkoCompanion
{
    // One adjustable number of the motion: what it is called on the tuning panel, the range
    // the panel offers, and the value built into the application.
    public sealed class TuningValue
    {
        public string Key { get; private set; }
        public string Group { get; private set; }
        public string Label { get; private set; }
        public string Hint { get; private set; }
        // An on/off choice (0 or 1) rather than an amount.
        public bool IsToggle { get; internal set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Default { get; internal set; }
        // Read by CharacterView on every frame.
        public double Value;

        internal TuningValue(string key, string group, string label, double min, double max, double value, string hint)
        {
            Key = key; Group = group; Label = label; Hint = hint;
            Min = min; Max = max; Default = Value = value;
        }

        public bool Changed { get { return Value != Default; } }

        // The slider moves in 200 steps of a round size, so values read as short numbers.
        public double Step
        {
            get
            {
                if (IsToggle) return 1;
                double raw = (Max - Min) / 200;
                double scale = Math.Pow(10, Math.Floor(Math.Log10(raw)));
                double lead = raw / scale;
                return (lead < 1.5 ? 1 : (lead < 3.5 ? 2 : (lead < 7.5 ? 5 : 10))) * scale;
            }
        }

        public string Format(double value)
        {
            int decimals = Math.Max(0, (int)Math.Ceiling(-Math.Log10(Step) - .0001));
            return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        // As Format, with on/off choices in words.
        public string Describe(double value)
        {
            return IsToggle ? (value > 0 ? "オン" : "オフ") : Format(value);
        }

        // Any finite number inside the range; the built-in value is always allowed exactly.
        public void Set(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            Value = IsToggle ? (value >= .5 ? 1 : 0) : Math.Max(Min, Math.Min(Max, value));
        }
    }

    // Every number of the motion that can be adjusted while the application runs. The values
    // built in here are the motion as shipped: with nothing changed, the character moves
    // exactly as it did when these were constants in CharacterView.
    public sealed class MotionTuning
    {
        public const string Keyboard = "キーボードの手", Mouse = "マウスの手", Head = "頭", Face = "まばたき・口", Acting = "しぐさ";
        public static readonly string[] Groups = { Acting, Keyboard, Mouse, Head, Face };

        private readonly List<TuningValue> values = new List<TuningValue>();
        public IList<TuningValue> Values { get { return values.AsReadOnly(); } }

        // Keyboard hand (artboard pixels unless stated; 3 artboard px is about 1 px on screen).
        public readonly TuningValue KeyHeight, KeyHeightSlow, KeyHopLift, KeyMaxHeight, KeyAccent, KeyHover, KeyPress,
            KeyRebound, KeyApart, KeySwell, KeySplat, KeyFlick, KeyFlickMax, KeyHoldHeight, KeyLinger;
        public readonly TuningValue MouseFollow, MouseDamping, MouseLean, MouseLeanMax, MouseSquash, MouseClickSink,
            MouseDrift, MouseClickLines;
        public readonly TuningValue HeadDip, HeadDipTilt, HeadAccent, HeadClickDip, HeadPointerLean, HeadSway, HeadBreath,
            HeadTiltSize, HeadTiltEvery, HairAhoge;
        public readonly TuningValue BlinkEvery, BlinkDouble, BlinkSlowness, PurseTyping, PurseEvery;
        // Acting: which scenes play, and how.
        public readonly TuningValue ActGreet, ActSleep, ActSleepAfter, ActTriumph, ActTriumphKeys, ActPet, ActThink,
            ActSweat, ActSymbols, ActThump;

        public MotionTuning()
        {
            ActGreet = Toggle("act.greet", "あいさつ", "起動した時と、隠した状態から戻った時に、笑顔で手を振ります。");
            ActSleep = Toggle("act.sleep", "居眠り", "しばらく何もしないと、あくびをして眠ります。キーやクリックでびくっと起き、マウスをそっと動かすと静かに起きます。");
            ActSleepAfter = Add("act.sleepAfter", Acting, "居眠りまでの時間（秒）", 20, 900, 120, "何もしない時間がこれだけ続くと眠ります。あくびはその少し前に出ます。");
            ActTriumph = Toggle("act.triumph", "やりきった", "長く打ったあとの Enter で、手を掲げて笑顔になります。");
            ActTriumphKeys = Add("act.triumphKeys", Acting, "やりきったに必要な打鍵数", 5, 80, 20, "Enter の前に続けてこれだけ打っていると「やりきった」になります。");
            ActPet = Toggle("act.pet", "なでなで", "頭の上でポインターを左右に往復させると、目を細めて喜びます。");
            ActThink = Toggle("act.think", "考え中", "打つ手が止まると、ときどき首をかしげて指でトントンします。");
            ActSweat = Toggle("act.sweat", "必死の汗", "とても速い打鍵が続くと、汗が飛びます。");
            ActSymbols = Add("act.symbols", Acting, "記号の大きさ", 0, 2, 1, "zzz・！・ハートなどの記号の大きさの倍率（0 で記号を出しません）。");
            ActThump = Add("act.thump", Acting, "Enter で机が揺れる大きさ", 0, 30, 10, "Enter を叩いた瞬間に、絵全体が沈んで跳ね返る距離（0 で揺れません）。");

            KeyHeight = Add("key.height", Keyboard, "振り上げの高さ", 0, 120, 44, "続けて打つ時に手を振り上げる高さ。");
            KeyHeightSlow = Add("key.heightSlow", Keyboard, "ゆっくり打つ時の上乗せ", 0, 100, 34, "間を空けて1つだけ打つ時に、さらに高く振り上げる分。");
            KeyHopLift = Add("key.hopLift", Keyboard, "遠くへ跳ぶ時の上乗せ", 0, 1, .25, "次に叩く場所が遠いほど、高い弧を描く度合い。");
            KeyMaxHeight = Add("key.maxHeight", Keyboard, "高さの上限", 60, 180, 120, "どれだけ重なっても、これより高くは振り上げません。");
            KeyAccent = Add("key.accent", Keyboard, "Enter の振りの倍率", 1, 2.5, 1.5, "Enter を叩く時に、振り上げを何倍にするか。");
            KeyHover = Add("key.hover", Keyboard, "打つ合間に浮かせる高さ", 0, 60, 24, "次の打鍵を待つあいだ、手をキーから浮かせておく高さ。");
            KeyPress = Add("key.press", Keyboard, "押し込みの深さ", 0, 20, 6, "叩いた瞬間にキーへ沈み込む深さ。");
            KeyRebound = Add("key.rebound", Keyboard, "跳ね返りの強さ", 0, 1.2, .55, "叩いた直後に手が跳ね上がる勢い。");
            KeyApart = Add("key.apart", Keyboard, "叩く場所を離す距離", 0, 120, 60, "次に叩く場所を、直前の場所から最低これだけ離します。");
            KeySwell = Add("key.swell", Keyboard, "振り上げた手の拡大", 0, .8, .3, "手が上がるほど大きく見せる度合い（0 で拡大なし）。");
            KeySplat = Add("key.splat", Keyboard, "着地の横つぶれ", 0, .5, .16, "叩きつけた瞬間に手が横へつぶれる度合い。");
            KeyFlick = Add("key.flick", Keyboard, "手首のしなり", 0, .03, .011, "手の上下の速さに対して、手首が遅れて返る度合い。");
            KeyFlickMax = Add("key.flickMax", Keyboard, "手首のしなりの上限（度）", 0, 26, 19, "手首が返る角度の上限。");
            KeyHoldHeight = Add("key.holdHeight", Keyboard, "長押し連打の高さ", 10, 80, 30, "キーを押し続けた時の、細かい連打の高さ。");
            KeyLinger = Add("key.linger", Keyboard, "手を戻すまでの待ち（秒）", .2, 2, .65, "最後の打鍵から、手が元の位置へ帰り始めるまでの時間。");

            MouseFollow = Add("mouse.follow", Mouse, "追いかける速さ", 6, 45, 22, "ポインターの動きに手が追いつく速さ。");
            MouseDamping = Add("mouse.damping", Mouse, "行き過ぎの少なさ", .3, 1.2, .62, "小さいほど勢い余って行き過ぎ、1 以上では行き過ぎません。");
            MouseLean = Add("mouse.lean", Mouse, "横移動での傾きやすさ", 0, .0004, .00012, "素早く横へ動かした時に、マウスと手が遅れて傾く度合い。");
            MouseLeanMax = Add("mouse.leanMax", Mouse, "傾きの上限", 0, .25, .11, "マウスと手が傾く大きさの上限。");
            MouseSquash = Add("mouse.squash", Mouse, "クリックのつぶれ", 0, .3, .11, "クリックで手元が縦につぶれる度合い。");
            MouseClickSink = Add("mouse.clickSink", Mouse, "クリックの沈み", 0, 30, 14, "クリックで手元が下がる距離。");
            MouseDrift = Add("mouse.drift", Mouse, "待機中の揺れ", 0, 4, 1, "動かしていない間の、手元のゆっくりした揺れの倍率。");
            MouseClickLines = Add("mouse.clickLines", Mouse, "クリックの線の長さ", 0, 2.5, 1, "クリックで飛び出す線の長さの倍率（0 で線を出しません）。");

            HeadDip = Add("head.dip", Head, "打鍵での沈み", 0, 15, 4.8, "キーを叩くたびに頭が沈む距離。");
            HeadDipTilt = Add("head.dipTilt", Head, "打鍵での傾き（度）", 0, 3, .8, "キーを叩くたびに頭が傾く角度。");
            HeadAccent = Add("head.accent", Head, "Enter の衝撃の倍率", 1, 3, 1.8, "Enter の着地の強さ。頭の沈みと手のつぶれの両方に効きます。");
            HeadClickDip = Add("head.clickDip", Head, "クリックでの沈み", 0, 10, 3, "クリックで頭が沈む距離。");
            HeadPointerLean = Add("head.pointerLean", Head, "ポインター側への傾き（度）", 0, 3, .9, "ポインターが画面の端にある時、そちらへ頭を傾ける角度。");
            HeadSway = Add("head.sway", Head, "待機中の揺れ", 0, 3, 1, "何もしていない間の、頭のゆっくりした揺れの倍率。");
            HeadBreath = Add("head.breath", Head, "呼吸の上下", 0, 8, 3.2, "呼吸に合わせて頭が上下する幅。");
            HeadTiltSize = Add("head.tiltSize", Head, "首かしげの大きさ", 0, 3, 1, "ときどき首をかしげる角度の倍率。");
            HeadTiltEvery = Add("head.tiltEvery", Head, "首かしげの間隔", .2, 3, 1, "首をかしげる間隔の倍率（小さいほど頻繁）。");

            HairAhoge = Add("hair.ahoge", Head, "アホ毛の揺れ", 0, 2.5, 1, "頭の動きに遅れて、アホ毛がしなって揺れる大きさ（0 で揺れません）。");

            BlinkEvery = Add("blink.every", Face, "まばたきの間隔", .3, 3, 1, "まばたきの間隔の倍率（小さいほど頻繁）。");
            BlinkDouble = Add("blink.double", Face, "2回続く割合", 0, .8, .2, "まばたきが2回続けて起きる割合。");
            BlinkSlowness = Add("blink.slowness", Face, "まばたきの遅さ", .5, 2.5, 1, "まぶたが閉じて開くまでの時間の倍率。");
            PurseTyping = Add("mouth.purseTyping", Face, "打鍵中の口のすぼめ", 0, 1, .7, "キーを打っている間、口元をすぼめる強さ。");
            PurseEvery = Add("mouth.purseEvery", Face, "待機中に口をすぼめる間隔", .3, 3, 1, "何もしていない間に口元をすぼめる間隔の倍率。");
        }

        private TuningValue Add(string key, string group, string label, double min, double max, double value, string hint)
        {
            var added = new TuningValue(key, group, label, min, max, value, hint);
            values.Add(added);
            return added;
        }

        private TuningValue Toggle(string key, string label, string hint)
        {
            TuningValue added = Add(key, Acting, label, 0, 1, 1, hint);
            added.IsToggle = true;
            return added;
        }

        // A character's own built-in value (see CharacterProfile): what "reset" returns to.
        public void SetDefault(string key, double number)
        {
            TuningValue value = Find(key);
            if (value == null) return;
            value.Set(number);
            value.Default = value.Value;
        }

        public TuningValue Find(string key)
        {
            foreach (TuningValue value in values) if (value.Key == key) return value;
            return null;
        }

        public bool AnyChanged
        {
            get
            {
                foreach (TuningValue value in values) if (value.Changed) return true;
                return false;
            }
        }

        public void ResetAll()
        {
            foreach (TuningValue value in values) value.Value = value.Default;
        }

        // The file holds only what differs from the built-in values, one "key=value" per line,
        // so it stays valid when the built-in values change in a later version.
        public string ToText()
        {
            var text = new StringBuilder();
            foreach (TuningValue value in values)
                if (value.Changed)
                    text.Append(value.Key).Append('=').Append(value.Value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            return text.ToString();
        }

        // Unknown keys and unreadable numbers are skipped; everything not named goes back to
        // its built-in value.
        public void FromText(string text)
        {
            ResetAll();
            foreach (string line in (text ?? "").Split('\n'))
            {
                int equals = line.IndexOf('=');
                if (equals <= 0 || line.TrimStart().StartsWith("#")) continue;
                TuningValue value = Find(line.Substring(0, equals).Trim());
                double number;
                if (value != null && double.TryParse(line.Substring(equals + 1).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out number))
                    value.Set(number);
            }
        }

        // What was changed, for pasting into a message: the new value, the built-in one and the name.
        public string Summary()
        {
            var text = new StringBuilder("隅っこキャラ 動きの調整（初期値から変えた値）\n");
            int changed = 0;
            foreach (TuningValue value in values)
            {
                if (!value.Changed) continue;
                changed++;
                text.Append(value.Key).Append(" = ").Append(value.Describe(value.Value)).Append("  （初期値 ")
                    .Append(value.Describe(value.Default)).Append("、").Append(value.Group).Append("／").Append(value.Label).Append("）\n");
            }
            if (changed == 0) text.Append("（変更なし）\n");
            return text.ToString();
        }

        // A missing file means nothing was tuned. Returns false (and leaves the built-in
        // values) when the file exists but cannot be read.
        public bool Load(string path, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(path)) { ResetAll(); return true; }
                FromText(File.ReadAllText(path, Encoding.UTF8));
                return true;
            }
            catch (Exception e)
            {
                ResetAll();
                error = e.Message;
                return false;
            }
        }

        // Written to a temporary file first, so a failed save keeps the previous one. With
        // nothing changed there is no file at all.
        public bool Save(string path, out string error)
        {
            error = null;
            try
            {
                if (!AnyChanged)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return true;
                }
                File.WriteAllText(path + ".tmp", ToText(), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                try { File.Delete(path + ".tmp"); } catch (Exception) { }
                return false;
            }
        }
    }
}
