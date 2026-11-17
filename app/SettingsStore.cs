using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

namespace SumikkoCompanion
{
    // What the companion remembers between runs: one key=value pair per line in settings.txt.
    internal sealed class CompanionSettings
    {
        public int Size = 440;
        public double Left = double.NaN, Top = double.NaN;
        public bool ClickThrough, ReducedMotion;
        public double MotionStrength = 1;
        public MouseMotionRange MouseRange = MouseMotionRange.Default;
        public string InputDeviceId = "", InputDeviceName = "", OutputDeviceId = "", OutputDeviceName = "";
        public double LipSyncGain = CharacterView.DefaultLipSyncGain, LipSyncFloor = CharacterView.DefaultLipSyncFloor;
        // Which character is shown: "" for the one in assets/, otherwise its folder under
        // characters/. MouseRange above belongs to the one in assets/; every other character
        // keeps its own here.
        public string Character = "";
        public Dictionary<string, MouseMotionRange> CharacterRanges = new Dictionary<string, MouseMotionRange>(StringComparer.Ordinal);
        private const string CharacterRangeKey = "mouseRange@";

        // Unknown keys and invalid values are ignored, leaving the default in place.
        public static CompanionSettings Parse(IEnumerable<string> lines)
        {
            var result = new CompanionSettings();
            double mouseLeft = -120, mouseRight = 120, mouseTop = -45, mouseBottom = 5;
            bool hasMouseRange = false;
            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                int parsed;
                double number;
                bool isNumber = double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out number);
                switch (parts[0])
                {
                    case "size":
                        if (int.TryParse(parts[1], out parsed) && (parsed == 320 || parsed == 440 || parsed == 560)) result.Size = parsed;
                        break;
                    case "left": if (isNumber && IsValidCoordinate(number)) result.Left = number; break;
                    case "top": if (isNumber && IsValidCoordinate(number)) result.Top = number; break;
                    case "clickThrough": result.ClickThrough = parts[1] == "true"; break;
                    case "reducedMotion": result.ReducedMotion = parts[1] == "true"; break;
                    case "motionStrength":
                        if (isNumber && (number == .4 || number == 1 || number == 1.4)) result.MotionStrength = number;
                        break;
                    case "inputDeviceId": result.InputDeviceId = Decode(parts[1]); break;
                    case "inputDeviceName": result.InputDeviceName = Decode(parts[1]); break;
                    case "outputDeviceId": result.OutputDeviceId = Decode(parts[1]); break;
                    case "outputDeviceName": result.OutputDeviceName = Decode(parts[1]); break;
                    case "lipSyncGain":
                        if (isNumber && number >= CharacterView.MinLipSyncGain && number <= CharacterView.MaxLipSyncGain) result.LipSyncGain = number;
                        break;
                    case "lipSyncFloor":
                        if (isNumber && number >= 0 && number <= CharacterView.MaxLipSyncFloor) result.LipSyncFloor = number;
                        break;
                    case "mouseRangeLeft": if (isNumber && IsValidCoordinate(number)) { mouseLeft = number; hasMouseRange = true; } break;
                    case "mouseRangeRight": if (isNumber && IsValidCoordinate(number)) { mouseRight = number; hasMouseRange = true; } break;
                    case "mouseRangeTop": if (isNumber && IsValidCoordinate(number)) { mouseTop = number; hasMouseRange = true; } break;
                    case "mouseRangeBottom": if (isNumber && IsValidCoordinate(number)) { mouseBottom = number; hasMouseRange = true; } break;
                    case "character": result.Character = parts[1]; break;
                    default:
                        if (!parts[0].StartsWith(CharacterRangeKey, StringComparison.Ordinal)) break;
                        // mouseRange@<character>=left,right,top,bottom
                        string[] edges = parts[1].Split(',');
                        var edge = new double[4];
                        bool valid = edges.Length == 4 && parts[0].Length > CharacterRangeKey.Length;
                        for (int i = 0; valid && i < 4; i++)
                            valid = double.TryParse(edges[i], NumberStyles.Float, CultureInfo.InvariantCulture, out edge[i]) && IsValidCoordinate(edge[i]);
                        if (valid) result.CharacterRanges[parts[0].Substring(CharacterRangeKey.Length)] = new MouseMotionRange(edge[0], edge[1], edge[2], edge[3]);
                        break;
                }
            }
            // Files written before the range could be calibrated derive it from the motion strength.
            result.MouseRange = hasMouseRange ? new MouseMotionRange(mouseLeft, mouseRight, mouseTop, mouseBottom) :
                new MouseMotionRange(-120 * result.MotionStrength, 120 * result.MotionStrength, -45 * result.MotionStrength, 5);
            return result;
        }

        public string[] ToLines()
        {
            MouseMotionRange range = MouseRange ?? MouseMotionRange.Default;
            var lines = new List<string> {
                "size=" + Size.ToString(CultureInfo.InvariantCulture),
                "left=" + Left.ToString(CultureInfo.InvariantCulture),
                "top=" + Top.ToString(CultureInfo.InvariantCulture),
                "clickThrough=" + (ClickThrough ? "true" : "false"),
                "reducedMotion=" + (ReducedMotion ? "true" : "false"),
                "motionStrength=" + MotionStrength.ToString(CultureInfo.InvariantCulture),
                "mouseRangeLeft=" + range.Left.ToString(CultureInfo.InvariantCulture),
                "mouseRangeRight=" + range.Right.ToString(CultureInfo.InvariantCulture),
                "mouseRangeTop=" + range.Top.ToString(CultureInfo.InvariantCulture),
                "mouseRangeBottom=" + range.Bottom.ToString(CultureInfo.InvariantCulture),
                // Base64 keeps endpoint IDs and arbitrary Unicode device names on one line.
                "inputDeviceId=" + Encode(InputDeviceId),
                "inputDeviceName=" + Encode(InputDeviceName),
                "outputDeviceId=" + Encode(OutputDeviceId),
                "outputDeviceName=" + Encode(OutputDeviceName),
                "lipSyncGain=" + LipSyncGain.ToString("R", CultureInfo.InvariantCulture),
                "lipSyncFloor=" + LipSyncFloor.ToString("R", CultureInfo.InvariantCulture)
            };
            // Written only once another character has been chosen, so a file that never left
            // the first character stays as it always was.
            if (!String.IsNullOrEmpty(Character)) lines.Add("character=" + Character);
            var names = new List<string>(CharacterRanges.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string name in names)
            {
                MouseMotionRange own = CharacterRanges[name];
                lines.Add(CharacterRangeKey + name + "=" + own.Left.ToString("R", CultureInfo.InvariantCulture) + "," +
                    own.Right.ToString("R", CultureInfo.InvariantCulture) + "," + own.Top.ToString("R", CultureInfo.InvariantCulture) + "," +
                    own.Bottom.ToString("R", CultureInfo.InvariantCulture));
            }
            return lines.ToArray();
        }

        private static bool IsValidCoordinate(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) < 100000;
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        private static string Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch (FormatException) { return ""; }
        }
    }

    internal enum SettingsLoadResult { Missing, Loaded, Failed }

    // Reads and writes settings.txt without ever losing the saved copy:
    // - a save writes the complete file beside the old one and only then swaps it in;
    // - after a failed read nothing is saved, because the file may hold settings this run
    //   never saw, until the user explicitly asks to overwrite it.
    internal sealed class SettingsStore
    {
        private readonly string path;
        private bool overwriteAllowed;
        // What the file holds as far as this run knows, and when it was last written; a save
        // with the same content is skipped while the file is untouched since.
        private string knownContent;
        private DateTime knownWriteTimeUtc;
        // How many times the file was actually written (for the checks).
        internal int Writes { get; private set; }

        public SettingsLoadResult LoadResult { get; private set; }
        public bool SaveFailed { get; private set; }
        // The most recent read or write error, for display; null when both worked.
        public string LastError { get; private set; }
        public bool CanSave { get { return LoadResult != SettingsLoadResult.Failed || overwriteAllowed; } }

        public SettingsStore(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentException("A settings path is required.", "path");
            this.path = path;
            LoadResult = SettingsLoadResult.Missing;
        }

        // Null when there is nothing to apply: no file yet (Missing) or an unreadable one (Failed).
        public CompanionSettings Load()
        {
            try
            {
                DateTime written = File.GetLastWriteTimeUtc(path);
                string[] lines = File.ReadAllLines(path);
                LoadResult = SettingsLoadResult.Loaded;
                LastError = null;
                Remember(lines, written);
                return CompanionSettings.Parse(lines);
            }
            // Decided by the read itself, not File.Exists, which also says "no" when access is denied.
            catch (FileNotFoundException) { LoadResult = SettingsLoadResult.Missing; }
            catch (DirectoryNotFoundException) { LoadResult = SettingsLoadResult.Missing; }
            catch (IOException ex) { LoadFailed(ex); }
            catch (UnauthorizedAccessException ex) { LoadFailed(ex); }
            catch (SecurityException ex) { LoadFailed(ex); }
            return null;
        }

        public void AllowOverwrite()
        {
            overwriteAllowed = true;
        }

        public bool Save(CompanionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (!CanSave) return false;
            string[] lines = settings.ToLines();
            string temporary = path + ".tmp";
            try
            {
                // Clicking or re-confirming without a change rewrites nothing.
                if (knownContent != null && knownContent == string.Join("\n", lines) &&
                    File.Exists(path) && File.GetLastWriteTimeUtc(path) == knownWriteTimeUtc)
                {
                    SaveFailed = false;
                    LastError = null;
                    return true;
                }
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                    4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    foreach (string line in lines) writer.WriteLine(line);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                Writes++;
                SaveFailed = false;
                LastError = null;
                Remember(lines, File.GetLastWriteTimeUtc(path));
                return true;
            }
            catch (IOException ex) { return SaveFailedWith(ex, temporary); }
            catch (UnauthorizedAccessException ex) { return SaveFailedWith(ex, temporary); }
            catch (SecurityException ex) { return SaveFailedWith(ex, temporary); }
        }

        private void Remember(string[] lines, DateTime writtenUtc)
        {
            knownContent = string.Join("\n", lines);
            knownWriteTimeUtc = writtenUtc;
        }

        private void LoadFailed(Exception error)
        {
            LoadResult = SettingsLoadResult.Failed;
            LastError = error.Message;
            knownContent = null;
        }

        private bool SaveFailedWith(Exception error, string temporary)
        {
            SaveFailed = true;
            LastError = error.Message;
            // The file may or may not have been replaced; the next save writes it again.
            knownContent = null;
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }
    }
}
