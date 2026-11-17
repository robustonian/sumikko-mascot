using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using SumikkoCompanion;

// The adjustable numbers of the motion: their definitions, the file they are kept in, the
// panel that changes them, and their effect on the character.
internal static class TuningChecks
{
    internal static void Run(Action<bool, string> assert, string assets)
    {
        var tuning = new MotionTuning();
        var keys = new HashSet<string>();
        bool sound = true;
        foreach (TuningValue value in tuning.Values)
        {
            if (!keys.Add(value.Key) || value.Min >= value.Max || value.Default < value.Min || value.Default > value.Max ||
                !(value.Step > 0) || Array.IndexOf(MotionTuning.Groups, value.Group) < 0 ||
                string.IsNullOrEmpty(value.Label) || string.IsNullOrEmpty(value.Hint) || value.Changed)
                sound = false;
            // The value shown for the built-in number must read back as that number.
            if (double.Parse(value.Format(value.Default), System.Globalization.CultureInfo.InvariantCulture) != value.Default) sound = false;
        }
        assert(sound && tuning.Values.Count >= 30, "every tuning value has a unique key, a range holding its built-in value and a readable name");
        assert(tuning.ToText() == "" && !tuning.AnyChanged, "untouched tuning stores nothing");

        tuning.KeyHeight.Set(60);
        tuning.MouseLean.Set(.0002);
        tuning.HeadDip.Set(999);
        tuning.BlinkEvery.Set(double.NaN);
        assert(tuning.HeadDip.Value == tuning.HeadDip.Max && tuning.BlinkEvery.Value == 1, "a value is kept inside its range and never becomes NaN");
        string text = tuning.ToText();
        assert(text == "key.height=60\nmouse.lean=0.0002\nhead.dip=15\n", "only the changed values are stored");
        var restored = new MotionTuning();
        restored.KeySplat.Set(.4);
        restored.FromText(text);
        assert(restored.KeyHeight.Value == 60 && restored.MouseLean.Value == .0002 && restored.HeadDip.Value == 15 &&
            restored.KeySplat.Value == restored.KeySplat.Default, "stored values are restored exactly and the rest return to the built-in ones");
        restored.FromText("# note\nno.such.key=3\nkey.height=abc\nkey.hover=1e9\n=4\nmouse.follow = 30 \r\n");
        assert(restored.KeyHeight.Value == 44 && restored.KeyHover.Value == restored.KeyHover.Max && restored.MouseFollow.Value == 30,
            "unknown keys and unreadable numbers in the tuning file are skipped");
        assert(tuning.Summary().Contains("key.height = 60") && tuning.Summary().Contains("初期値 44") &&
            new MotionTuning().Summary().Contains("変更なし"), "the summary lists each changed value with its built-in one");

        string folder = Path.Combine(Path.GetTempPath(), "SumikkoCompanionTuning-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "tuning.txt");
            string error;
            assert(new MotionTuning().Save(path, out error) && !File.Exists(path), "saving untouched tuning creates no file");
            assert(tuning.Save(path, out error) && File.ReadAllText(path) == text && !File.Exists(path + ".tmp"), "changed tuning is saved");
            var loaded = new MotionTuning();
            assert(loaded.Load(path, out error) && loaded.ToText() == text, "saved tuning is loaded again");
            tuning.KeyHeight.Set(70);
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                assert(!tuning.Save(path, out error) && error != null, "a tuning save that cannot replace the file reports it");
            assert(File.ReadAllText(path) == text && !File.Exists(path + ".tmp"), "a failed tuning save keeps the previous file");
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                assert(!loaded.Load(path, out error) && error != null && !loaded.AnyChanged,
                    "a tuning file that cannot be read is reported and leaves the built-in values");
            tuning.ResetAll();
            assert(tuning.Save(path, out error) && !File.Exists(path), "putting everything back removes the tuning file");
            assert(loaded.Load(path, out error) && !loaded.AnyChanged, "a missing tuning file means the built-in values");

            MotionChecks(assert, assets);
            PanelChecks(assert);
            WindowChecks(assert, folder);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static CharacterView NewView(string assets)
    {
        var view = new CharacterView(assets);
        view.Scenes = false;
        view.Measure(new Size(1469, 1071));
        view.Arrange(new Rect(0, 0, 1469, 1071));
        view.Tick(0, 1.0 / 60, false, false, 0, 0, 0, false, false);
        return view;
    }

    // The highest point of one isolated stroke, and the lowest the head-driving impact got.
    private static double StrokeHeight(CharacterView view)
    {
        double highest = 0;
        for (int i = 0; i < 60; i++)
        {
            view.Tick(1 + i / 60.0, 1.0 / 60, i == 0, false, 0, 0, 0, false, false);
            highest = Math.Max(highest, -view.KeyboardHandOffset.Y);
        }
        return highest;
    }

    private static void MotionChecks(Action<bool, string> assert, string assets)
    {
        double normal = StrokeHeight(NewView(assets));
        var tall = NewView(assets);
        tall.Tuning.KeyHeight.Set(100);
        tall.Tuning.KeyHeightSlow.Set(80);
        tall.Tuning.KeyMaxHeight.Set(180);
        var low = NewView(assets);
        low.Tuning.KeyHeight.Set(0);
        low.Tuning.KeyHeightSlow.Set(0);
        low.Tuning.KeyHopLift.Set(0);
        double raised = StrokeHeight(tall), lowered = StrokeHeight(low);
        assert(raised > normal * 1.5 && lowered < normal * .5, "the stroke height follows its tuning values");

        // Two identical characters at rest with reduced motion (only blinks are drawn). On a
        // frame the first one does not draw, the second is told its tuning changed.
        CharacterView control = NewView(assets), still = NewView(assets);
        bool redrawn = false;
        for (int i = 1; i <= 600 && !redrawn; i++)
        {
            int controlBefore = control.Redraws, before = still.Redraws;
            control.Tick(i / 60.0, 1.0 / 60, false, false, 0, 0, 0, false, true);
            bool quiet = i > 120 && control.Redraws == controlBefore;
            if (quiet) still.TuningChanged();
            still.Tick(i / 60.0, 1.0 / 60, false, false, 0, 0, 0, false, true);
            redrawn = quiet && still.Redraws == before + 1;
            if (quiet && !redrawn) break;
        }
        assert(redrawn, "a tuning change redraws a character at rest once");

        var calm = NewView(assets);
        calm.Tuning.MouseSquash.Set(0);
        calm.Tuning.MouseClickSink.Set(0);
        calm.Tuning.MouseClickLines.Set(0);
        calm.Tuning.HeadClickDip.Set(0);
        bool finite = true;
        for (int i = 1; i <= 60; i++)
        {
            calm.Tick(i / 60.0, 1.0 / 60, false, i > 10 && i < 30, 0, 0, 0, false, false);
            Point hand = calm.MouseOffset;
            if (double.IsNaN(hand.X) || double.IsNaN(hand.Y) || double.IsNaN(calm.MouseHandSquash) || calm.MouseHandSquash != 0) finite = false;
        }
        assert(finite, "click effects tuned down to zero simply do nothing");
    }

    private static void PanelChecks(Action<bool, string> assert)
    {
        var tuning = new MotionTuning();
        int changes = 0, saves = 0;
        string failure = null;
        bool demoOn = false;
        var panel = new TuningWindow(tuning, delegate { changes++; }, delegate { saves++; return failure; }, false,
            delegate(bool on) { demoOn = on; });
        panel.SetValue("key.height", 61.3);
        assert(tuning.KeyHeight.Value == 61.5 && changes == 1 && panel.Unsaved, "the panel rounds a value to the slider's step and applies it at once");
        panel.SetValue("key.height", 61.4);
        assert(changes == 1, "a move that lands on the same value is not a change");
        panel.SaveNow();
        assert(saves == 1 && !panel.Unsaved && panel.StatusText.Contains("保存しました"), "the panel saves a change and says so");
        panel.SaveNow();
        assert(saves == 1, "nothing is saved again without a change");
        panel.SetValue("key.height", 44.2);
        assert(tuning.KeyHeight.Value == 44 && !tuning.KeyHeight.Changed, "a slider back at the built-in value is exactly that value");
        failure = "disk full";
        panel.SetValue("mouse.lean", .0003);
        panel.SaveNow();
        assert(panel.Unsaved && panel.StatusText.Contains("disk full"), "a failed tuning save is shown and tried again later");
        failure = null;
        panel.SaveNow();
        assert(!panel.Unsaved && saves == 3, "the retry saves the change");
        panel.SetValue("head.dip", -5);
        assert(tuning.HeadDip.Value == 0, "the panel cannot push a value out of its range");
        panel.ResetAll();
        panel.SaveNow();
        assert(!tuning.AnyChanged && panel.StatusText.Contains("初期値"), "everything can be put back at once");
        panel.ShowDemo(true);
        assert(demoOn, "the panel can run the demo while values are tried");
        panel.Close();
    }

    private static void WindowChecks(Action<bool, string> assert, string folder)
    {
        string settings = Path.Combine(folder, "window-settings.txt");
        string tuningFile = Path.Combine(folder, "tuning.txt");
        new SettingsStore(settings).Save(new CompanionSettings { Left = 100, Top = 100 });
        File.WriteAllText(tuningFile, "key.height=80\n");
        var window = new CompanionWindow(false, false, settings);
        assert(window.Tuning.KeyHeight.Value == 80, "the companion starts with the saved tuning");
        TuningWindow panel = window.NewTuningWindow();
        panel.SetValue("head.dip", 9);
        panel.SaveNow();
        assert(File.ReadAllText(tuningFile) == "key.height=80\nhead.dip=9\n", "a change on the panel is saved beside the settings");
        panel.Close();

        using (new FileStream(tuningFile, FileMode.Open, FileAccess.Read, FileShare.None))
            window = new CompanionWindow(false, false, settings);
        assert(!window.Tuning.AnyChanged && window.SaveTuning() != null && File.ReadAllText(tuningFile) == "key.height=80\nhead.dip=9\n",
            "tuning that could not be read at start-up is never overwritten");
    }
}
