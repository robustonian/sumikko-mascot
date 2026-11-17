using System;
using System.Windows;
using SumikkoCompanion;

// Acting: dozing off and waking, the smile after a long phrase, the wave, petting, thinking,
// sweat, and that none of it runs when it is switched off.
internal static class ActingChecks
{
    private const int Zee = 0, Bang = 1, Dot = 2, Spark = 3, Heart = 4, Sweat = 5;
    private const double Frame = 1.0 / 60;

    private sealed class Stage
    {
        public CharacterView View;
        public double Time, PointerX;
        public bool Reduced;
        // What happened while stepping.
        public double WidestMouth, HighestHand, DeepestDrop, LowestDrop;
        public int MostSymbols;
        public bool SawSymbol(int kind) { return seen[kind]; }
        private readonly bool[] seen = new bool[6];

        public void Forget()
        {
            WidestMouth = HighestHand = DeepestDrop = LowestDrop = 0;
            MostSymbols = 0;
            Array.Clear(seen, 0, seen.Length);
        }

        public void Tick(bool key, bool enter, bool mouse)
        {
            Time += Frame;
            View.Tick(Time, Frame, key, mouse, PointerX, 0, 0, false, Reduced, enter);
            WidestMouth = Math.Max(WidestMouth, View.MouthShown);
            HighestHand = Math.Max(HighestHand, -View.KeyboardHandOffset.Y);
            DeepestDrop = Math.Max(DeepestDrop, View.BodyDrop);
            LowestDrop = Math.Min(LowestDrop, View.BodyDrop);
            MostSymbols = Math.Max(MostSymbols, View.SymbolCount);
            for (int kind = 0; kind < 6; kind++) if (View.CountSymbols(kind) > 0) seen[kind] = true;
        }

        public void Wait(double seconds)
        {
            for (int i = 0; i < (int)Math.Round(seconds * 60); i++) Tick(false, false, false);
        }

        // count keys, one every gap seconds.
        public void Type(int count, double gap)
        {
            int every = (int)Math.Round(gap * 60);
            for (int i = 0; i < count * every; i++) Tick(i % every == 0, false, false);
        }

        public void Enter() { Tick(true, true, false); }
    }

    private static Stage NewStage(string assets, double sleepAfter)
    {
        var view = new CharacterView(assets);
        view.Measure(new Size(1469, 1071));
        view.Arrange(new Rect(0, 0, 1469, 1071));
        view.Tuning.ActSleepAfter.Set(sleepAfter);
        var stage = new Stage { View = view };
        stage.Tick(false, false, false);
        return stage;
    }

    internal static void Run(Action<bool, string> assert, string assets)
    {
        // ---- Dozing off ----
        Stage s = NewStage(assets, 20);
        s.Wait(8);
        assert(!s.View.Asleep && s.View.EyesHeld == 0 && s.MostSymbols == 0, "a short quiet spell changes nothing");
        s.Wait(5);
        assert(s.WidestMouth > 1.2 && !s.View.Asleep, "before dozing off the character yawns");
        s.Wait(12);
        assert(s.View.Asleep && s.View.EyesHeld == 1 && s.SawSymbol(Zee), "left alone it falls asleep, eyes shut, with z's rising");
        // Only the jerk of a nodding head needs the full frame rate.
        int calmFrames = 0;
        for (int i = 0; i < 600; i++) { s.Tick(false, false, false); if (!s.View.NeedsFullFrameRate) calmFrames++; }
        assert(calmFrames > 480, "sleeping runs at the idle frame rate");
        s.Forget();
        s.Tick(true, false, false);
        bool woke = !s.View.Asleep;
        s.Wait(.3);
        assert(woke && s.SawSymbol(Bang) && s.View.EyesHeld == 0 && s.View.CountSymbols(Zee) == 0 && s.DeepestDrop > 2,
            "a key wakes it with a start: eyes open, the z's burst, an exclamation mark");
        s.Wait(3);
        assert(s.View.SymbolCount == 0 && s.View.BodyDrop == 0, "the start is over within moments");

        // The pointer alone: a nudge wakes gently, a sweep startles.
        s.Wait(26);
        bool slept = s.View.Asleep;
        s.Forget();
        for (int i = 0; i < 40; i++) { s.PointerX += .0025; s.Tick(false, false, false); }
        s.Wait(.5);
        assert(slept && !s.View.Asleep && !s.SawSymbol(Bang) && s.DeepestDrop == 0, "nudging the mouse wakes it gently");
        s.Wait(27);
        slept = s.View.Asleep;
        s.Forget();
        for (int i = 0; i < 12; i++) { s.PointerX += .06; s.Tick(false, false, false); }
        s.Wait(.3);
        assert(slept && !s.View.Asleep && s.SawSymbol(Bang), "sweeping the mouse startles it awake");
        s.Wait(27);
        slept = s.View.Asleep;
        s.Forget();
        for (int i = 0; i < 300; i++) { s.PointerX += (i % 2 == 0 ? .0004 : -.0004); s.Tick(false, false, false); }
        assert(slept && s.View.Asleep, "the tremble of a mouse at rest does not wake it");

        // Hours asleep stay in bounds.
        s.Forget();
        s.Wait(300);
        assert(s.View.Asleep && s.MostSymbols <= 6 && s.LowestDrop == 0 && s.WidestMouth == 0, "five minutes asleep: a few z's at a time, nothing drifts");

        // ---- Switched off ----
        Stage off = NewStage(assets, 20);
        off.View.Tuning.ActSleep.Set(0);
        off.Wait(40);
        assert(!off.View.Asleep && off.View.EyesHeld == 0 && off.MostSymbols == 0 && off.WidestMouth == 0, "with dozing switched off it stays awake");
        Stage reduced = NewStage(assets, 20);
        reduced.Reduced = true;
        reduced.Wait(40);
        reduced.View.Greet();
        reduced.Type(25, .1);
        reduced.Enter();
        reduced.Wait(1);
        assert(!reduced.View.Asleep && reduced.MostSymbols == 0 && reduced.WidestMouth == 0 && reduced.DeepestDrop == 0 && !reduced.View.Waving,
            "reduced motion plays no scenes");
        s.Reduced = true;
        s.Wait(.5);
        assert(!s.View.Asleep && s.View.SymbolCount == 0 && s.View.EyesHeld == 0, "switching to reduced motion ends a scene at once");
        Stage quiet = NewStage(assets, 20);
        quiet.View.Tuning.ActSymbols.Set(0);
        quiet.Wait(30);
        assert(quiet.View.Asleep && quiet.MostSymbols == 0, "symbols can be switched off without the scenes");

        // ---- A long phrase sent ----
        Stage t = NewStage(assets, 120);
        t.Wait(1);
        t.Enter();
        t.Wait(.6);
        assert(!t.View.Beaming && !t.SawSymbol(Spark) && t.View.EyesHeld == 0, "a lone Enter is just an Enter");
        t.Wait(3);
        t.Forget();
        t.Type(25, .1);
        t.Enter();
        t.Wait(.5);
        assert(t.View.Beaming && t.View.CountSymbols(Spark) == 3 && t.View.EyesHeld == 1 && t.View.MouthShown > .9 && t.HighestHand > 70,
            "Enter after a long phrase: a smile, sparkles and the hand flung up");
        t.Wait(2.5);
        assert(!t.View.Beaming && t.View.EyesHeld == 0 && t.View.MouthShown == 0 && t.View.SymbolCount == 0 && t.View.KeyboardHandAtRest,
            "the smile passes and the hand comes home");
        t.Forget();
        t.Type(25, .1);
        t.Enter();
        t.Wait(.5);
        assert(!t.View.Beaming && !t.SawSymbol(Spark), "it does not celebrate every few seconds");
        Stage plain = NewStage(assets, 120);
        plain.View.Tuning.ActTriumph.Set(0);
        plain.Type(25, .1);
        plain.Enter();
        plain.Wait(.5);
        assert(!plain.View.Beaming && !plain.SawSymbol(Spark), "the celebration can be switched off");

        // ---- The desk shakes under Enter ----
        assert(t.DeepestDrop > 4 && t.LowestDrop == 0, "Enter drops the whole picture for an instant, never upward");
        Stage firm = NewStage(assets, 120);
        firm.View.Tuning.ActThump.Set(0);
        firm.Wait(1);
        firm.Enter();
        firm.Wait(1);
        assert(firm.DeepestDrop == 0, "the shake can be set to nothing");

        // ---- Hello ----
        Stage g = NewStage(assets, 120);
        g.Wait(.5);
        g.View.Greet();
        g.Wait(1.2);
        assert(g.View.Waving && g.HighestHand > 90 && g.View.MouthShown > .9 && g.View.EyesHeld == 1, "greeting: the hand goes up and waves, with a smile");
        g.Wait(3);
        assert(!g.View.Waving && g.View.KeyboardHandAtRest && g.View.EyesHeld == 0 && g.View.MouthShown == 0, "after the wave the hand rests again");
        g.View.Greet();
        g.Wait(.8);
        bool waving = g.View.Waving;
        g.Tick(true, false, false);
        assert(waving && !g.View.Waving, "a key ends the wave at once");
        Stage shy = NewStage(assets, 120);
        shy.View.Tuning.ActGreet.Set(0);
        shy.View.Greet();
        shy.Wait(1.5);
        assert(!shy.View.Waving && shy.HighestHand == 0, "the greeting can be switched off");

        // ---- Petting ----
        Stage p = NewStage(assets, 120);
        p.Wait(1);
        for (int i = 0; i < 60; i++) { p.View.PointerOnArt = new Point(100 + i * 15, 300); p.Tick(false, false, false); }
        assert(!p.View.Petting && p.MostSymbols == 0, "a pointer crossing the head is not petting");
        for (int i = 0; i < 150; i++) { p.View.PointerOnArt = new Point(1100 + 150 * Math.Sin(i * .19), 850); p.Tick(false, false, false); }
        assert(!p.View.Petting, "rubbing somewhere else is not petting");
        for (int i = 0; i < 150; i++) { p.View.PointerOnArt = new Point(500 + 150 * Math.Sin(i * .19), 230); p.Tick(false, false, false); }
        assert(p.View.Petting && p.SawSymbol(Heart) && p.View.EyesHeld == 1 && p.View.MouthShown > .5, "rubbing the head is petting: eyes closed, hearts");
        p.View.PointerOnArt = new Point(1300, 1000);
        p.Wait(2.5);
        assert(!p.View.Petting && p.View.EyesHeld == 0 && p.View.MouthShown == 0 && p.View.SymbolCount == 0, "petting ends when the pointer leaves");
        p.View.Tuning.ActPet.Set(0);
        p.Forget();
        for (int i = 0; i < 150; i++) { p.View.PointerOnArt = new Point(500 + 150 * Math.Sin(i * .19), 230); p.Tick(false, false, false); }
        assert(!p.View.Petting && p.MostSymbols == 0, "petting can be switched off");

        // ---- Thinking ----
        Stage k = NewStage(assets, 120);
        bool thought = false, interrupted = false;
        for (int attempt = 0; attempt < 5 && !thought; attempt++)
        {
            k.Wait(20);
            k.Forget();
            k.Type(10, .12);
            k.Wait(4);
            thought = k.SawSymbol(Dot) && k.HighestHand > 5;
        }
        assert(thought, "a pause after typing brings a thought: dots and drumming fingers");
        for (int attempt = 0; attempt < 5 && !interrupted; attempt++)
        {
            k.Wait(20);
            k.Type(10, .12);
            for (int i = 0; i < 240 && !k.View.Thinking; i++) k.Tick(false, false, false);
            if (!k.View.Thinking) continue;
            k.Wait(1);
            k.Tick(true, false, false);
            k.Wait(.2);
            interrupted = !k.View.Thinking && k.View.CountSymbols(Dot) == 0;
        }
        assert(interrupted, "typing again ends the thought");

        // ---- The ahoge ----
        Stage h = NewStage(assets, 120);
        h.Wait(2);
        double calm = h.View.AhogeBend.Length + Math.Abs(h.View.AhogeTipBend);
        h.Enter();
        double swing = 0, wildest = 0;
        for (int i = 0; i < 40; i++) { h.Tick(false, false, false); swing = Math.Max(swing, h.View.AhogeBend.Length); }
        for (int i = 0; i < 180; i++) h.Tick(false, false, false);
        double settled = h.View.AhogeBend.Length;
        assert(calm < .04 && swing > .08 && settled < .04, "the ahoge barely stirs at rest, whips when the head drops, and settles again");
        for (int i = 0; i < 600; i++)
        {
            h.Tick(i % 5 == 0, i % 45 == 0, i % 50 < 6);
            wildest = Math.Max(wildest, Math.Max(h.View.AhogeBend.Length, Math.Abs(h.View.AhogeTipBend)));
        }
        assert(wildest < .6, "however hard the typing, the ahoge stays in shape");
        Stage flat = NewStage(assets, 120);
        flat.View.Tuning.HairAhoge.Set(0);
        flat.Wait(5);
        double moved = 0;
        for (int i = 0; i < 300; i++)
        {
            flat.Tick(i % 6 == 0, i % 60 == 0, false);
            moved = Math.Max(moved, flat.View.AhogeBend.Length + Math.Abs(flat.View.AhogeTipBend));
        }
        reduced.Type(10, .1);
        assert(moved == 0 && reduced.View.AhogeBend.Length == 0 && reduced.View.AhogeTipBend == 0,
            "the ahoge holds still when its sway is set to nothing, and with reduced motion");

        // ---- Frantic typing ----
        Stage f = NewStage(assets, 120);
        f.Type(25, .25);
        assert(!f.SawSymbol(Sweat), "steady typing raises no sweat");
        f.Type(60, .1);
        assert(f.SawSymbol(Sweat), "frantic typing does");
    }
}
