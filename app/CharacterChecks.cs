using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using SumikkoCompanion;

// Characters: the per-character measurements (character.txt), the list of characters that
// can be chosen, and changing the character while the companion runs.
internal static class CharacterChecks
{
    internal static void Run(Action<bool, string> assert, string root)
    {
        string assets = Path.Combine(root, "assets");
        CharacterProfile builtIn = new CharacterProfile(), sample = CharacterProfile.Load(assets);
        assert(sample.Problems.Count == 0, "the sample character.txt reads without a problem" +
            (sample.Problems.Count > 0 ? " (" + sample.Problems[0] + ")" : ""));
        assert(sample.Describe() == builtIn.Describe(), "the sample character.txt holds exactly the built-in values");

        var edited = new CharacterProfile();
        edited.Read(new[] {
            "# a comment", "name = テスト  ", "keyHinge = 700, 710   # moved",
            "leftEye = 1,2, 3,4,", "    5,6", "neck =", "clickRays = 10 20 30 40",
            "tune.hair.ahoge = 0" });
        assert(edited.Problems.Count == 0 && edited.Name == "テスト" && edited.Point("keyHinge") == new Point(700, 710) &&
            edited.Numbers("leftEye").Length == 6 && edited.Numbers("neck").Length == 0 && edited.Numbers("clickRays").Length == 4 &&
            edited.Point("keyPalm") == builtIn.Point("keyPalm"),
            "character.txt replaces the values it names (comments, continued lists, empty lists) and keeps the rest");
        var wrong = new CharacterProfile();
        wrong.Read(new[] { "stray", "keyHinge = 1,2,3", "leftEye = 1,2,3", "leftEye =", "keyPalm = 1,x", "nosuch = 1", "tune.nosuch = 1" });
        assert(wrong.Problems.Count == 7 && wrong.Describe() == builtIn.Describe(),
            "a mistake in character.txt is reported and leaves the built-in value in place");

        // A character's own built-in motion values, and artwork without the optional parts.
        var plain = new CharacterProfile();
        plain.Read(new[] { "neck =", "ahogeBase =", "ahogeTip =", "collar =", "mouseShoulderLine =", "mouseShoulderCut =",
            "headRise = 6", "tune.hair.ahoge = 0", "mouseRange = -60,60,-20,5" });
        var view = new CharacterView(assets, plain);
        view.Measure(new Size(1469, 1071));
        view.Arrange(new Rect(0, 0, 1469, 1071));
        bool still = true;
        for (int i = 0; i <= 240; i++)
        {
            view.Tick(i / 60.0, 1.0 / 60, i % 9 == 1, i == 100, Math.Sin(i * .1), 0, 0, false, false, i == 60);
            if (view.AhogeBend.Length != 0 || view.AhogeTipBend != 0) still = false;
        }
        view.UpdateLayout();
        assert(plain.Problems.Count == 0 && still && view.Tuning.HairAhoge.Value == 0 && !view.Tuning.HairAhoge.Changed &&
            !view.Tuning.AnyChanged && view.MouseRange.Left == -60 && view.MouseRange.Bottom == 5,
            "a character without a neck, lock of hair or shoulder patch runs, with its own built-in tuning and mouse range");

        // Every character that ships in characters/ must read cleanly and start.
        foreach (CharacterEntry shipped in CharacterCatalog.Find(root))
        {
            if (shipped.Id.Length == 0) continue;
            CharacterProfile own = CharacterProfile.Load(shipped.Directory);
            var shown = new CharacterView(shipped.Directory, own);
            shown.Measure(new Size(1469, 1071));
            shown.Arrange(new Rect(0, 0, 1469, 1071));
            for (int i = 0; i <= 120; i++) shown.Tick(i / 60.0, 1.0 / 60, i % 7 == 1, i == 50, Math.Sin(i * .1), 0, .1, false, false, i == 30);
            shown.UpdateLayout();
            assert(own.Problems.Count == 0, "characters/" + shipped.Id + ": character.txt reads without a problem and the character runs" +
                (own.Problems.Count > 0 ? " (" + own.Problems[0] + ")" : ""));
        }

        string folder = Path.Combine(Path.GetTempPath(), "sumikko-characters-" + Guid.NewGuid().ToString("N"));
        try
        {
            // A project folder of its own: the sample, a second character and an incomplete one.
            string project = Path.Combine(folder, "project"), second = Path.Combine(project, "characters", "tester");
            Directory.CreateDirectory(Path.Combine(project, "assets"));
            Directory.CreateDirectory(second);
            Directory.CreateDirectory(Path.Combine(project, "characters", "incomplete"));
            foreach (string image in CharacterProfile.ImageFiles)
            {
                File.Copy(Path.Combine(assets, image), Path.Combine(project, "assets", image));
                File.Copy(Path.Combine(assets, image), Path.Combine(second, image));
            }
            File.Copy(Path.Combine(assets, "head.png"), Path.Combine(project, "characters", "incomplete", "head.png"));
            File.WriteAllLines(Path.Combine(second, CharacterProfile.FileName),
                new[] { "name = 二人目", "mouseRange = -60,60,-20,5", "symbolAnchor = 900,200", "tune.hair.ahoge = 0" }, new UTF8Encoding(false));
            List<CharacterEntry> found = CharacterCatalog.Find(project);
            assert(found.Count == 2 && found[0].Id == "" && found[0].Name == "見本" && found[1].Id == "tester" && found[1].Name == "二人目",
                "the characters offered are the sample and every complete folder under characters");

            // Closed eyes and open mouth as two heads instead of face-expression.png.
            string split = Path.Combine(project, "characters", "split");
            Directory.CreateDirectory(split);
            foreach (string image in CharacterProfile.ImageFiles)
                if (image != CharacterProfile.WholeFaceFile) File.Copy(Path.Combine(assets, image), Path.Combine(split, image));
            File.Copy(Path.Combine(assets, CharacterProfile.WholeFaceFile), Path.Combine(split, CharacterProfile.ClosedEyesFile));
            bool halfway = CharacterCatalog.Complete(split);
            File.Copy(Path.Combine(assets, CharacterProfile.WholeFaceFile), Path.Combine(split, CharacterProfile.OpenMouthFile));
            var splitView = new CharacterView(split);
            splitView.Tick(0, 1.0 / 60, false, false, 0, 0, .2, false, false);
            assert(!halfway && CharacterCatalog.Complete(split) && CharacterProfile.ClosedEyesIn(split) == CharacterProfile.ClosedEyesFile &&
                CharacterProfile.OpenMouthIn(split) == CharacterProfile.OpenMouthFile && CharacterProfile.ClosedEyesIn(assets) == CharacterProfile.WholeFaceFile,
                "a character may bring its closed eyes and open mouth as two images instead of one");
            Directory.Delete(split, true);

            string settings = Path.Combine(folder, "settings.txt");
            var firstRange = new MouseMotionRange(-17, 118, -70, 8);
            new SettingsStore(settings).Save(new CompanionSettings { Left = 100, Top = 100, MouseRange = firstRange });
            string untouched = File.ReadAllText(settings);
            var window = new CompanionWindow(false, false, settings, project);
            window.SaveSettings();
            assert(window.CharacterId == "" && File.ReadAllText(settings) == untouched,
                "with the first character the settings file is written as it always was");
            List<CompanionWindow.MenuEntry> menu = window.MenuEntries();
            CompanionWindow.MenuEntry choices = menu.Find(delegate(CompanionWindow.MenuEntry entry) { return entry.Label == "キャラクター"; });
            assert(choices != null && choices.Children[0].Label == "見本" && choices.Children[0].Checked == true &&
                choices.Children[1].Label == "二人目" && choices.Children[1].Checked == false,
                "the menu lists the characters and marks the one on screen");

            CharacterView before = window.Character;
            assert(!window.SwitchCharacter("nobody") && !window.SwitchCharacter("incomplete") && window.Character == before && window.CharacterId == "",
                "choosing a character that is not there changes nothing");
            assert(window.SwitchCharacter("tester") && window.CharacterId == "tester" && window.Character != before &&
                window.Character.Profile.Point("symbolAnchor") == new Point(900, 200) && window.Tuning.HairAhoge.Value == 0,
                "choosing another character shows it, with its own measurements");
            CompanionSettings saved = new SettingsStore(settings).Load();
            assert(saved.Character == "tester" && Same(saved.MouseRange, firstRange) && Same(saved.CharacterRanges["tester"], new MouseMotionRange(-60, 60, -20, 5)) &&
                Same(window.Character.MouseRange, new MouseMotionRange(-60, 60, -20, 5)),
                "the choice is saved, and each character has its own mouse range");
            assert(Path.GetFileName(window.TuningFile) == "tuning-tester.txt" && Path.GetDirectoryName(window.TuningFile) == folder,
                "each character keeps its own tuning file beside the settings");
            window.Tuning.KeyHeight.Set(60);
            assert(window.SaveTuning() == null && File.Exists(Path.Combine(folder, "tuning-tester.txt")) && !File.Exists(Path.Combine(folder, "tuning.txt")),
                "tuning one character leaves the other's alone");

            var own = new MouseMotionRange(-30, 30, -10, 0);
            window.BeginMouseRangeEdit();
            window.PreviewMouseRange(own);
            window.EndMouseRangeEdit(own);
            assert(window.SwitchCharacter("") && Same(window.Character.MouseRange, firstRange) && window.Tuning.KeyHeight.Value == 44 &&
                window.Tuning.HairAhoge.Value == 1,
                "going back to the first character brings back its range and tuning");
            saved = new SettingsStore(settings).Load();
            assert(saved.Character == "" && Same(saved.CharacterRanges["tester"], own), "the range set for the other character is kept while it is away");
            assert(window.SwitchCharacter("tester") && Same(window.Character.MouseRange, own) && window.Tuning.KeyHeight.Value == 60,
                "and comes back with it, as does its tuning");

            window = new CompanionWindow(false, false, settings, project);
            assert(window.CharacterId == "tester" && Same(window.CurrentSettings().CharacterRanges["tester"], own) &&
                Same(window.CurrentSettings().MouseRange, firstRange), "the companion starts with the character chosen last");
            Directory.Delete(second, true);
            window = new CompanionWindow(false, false, settings, project);
            assert(window.CharacterId == "", "a chosen character whose folder is gone gives way to the first one");
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static bool Same(MouseMotionRange a, MouseMotionRange b)
    {
        return a != null && b != null && a.Left == b.Left && a.Right == b.Right && a.Top == b.Top && a.Bottom == b.Bottom;
    }
}
