using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SumikkoCompanion
{
    // Acting: what the character does beyond answering each key and click. It dozes off when
    // nothing happens and starts awake, beams after a long phrase is sent, waves hello,
    // enjoys being petted, drums its fingers while thinking and sweats when the typing gets
    // frantic. Everything here only adds to the springs and faces CharacterView already has;
    // with Scenes off (or reduced motion) none of it runs.
    public sealed partial class CharacterView
    {
        // Off for previews that must hold still (the mouse range dialog).
        public bool Scenes { get; set; }
        // The pointer in artboard coordinates, or NaN when it is not known.
        public Point PointerOnArt { get; set; }

        private readonly Random actRandom = new Random(11);
        private bool actStarted, acting;
        private double lastActivity, lastPointerX, lastPointerY, pointerTravel;

        // What acting asks of the face and head on this frame (zero = nothing).
        private double actEyes, actMouth, mouthShown;
        private double actHeadY, actHeadAngle;
        private double headYFrequency = 8, headYDamping = 1, headAngleFrequency = 5, headAngleDamping = .85;
        // The whole picture drops and bounces when something lands hard.
        private readonly Spring thump = new Spring();
        private bool accentImpact;

        // Dozing: asleep once the last heavy blink stays shut.
        private bool asleep, yawned;
        private double asleepSince, sleepLevel, nextZ, droopStart = -100, nextDroop;
        private double yawnStart = -100, startleStart = -100, gentleWake = -100, stirSince = -1;
        // A tear wells up at the corner of the eye after a yawn (0 = none, 1 = full).
        private double yawnTear;
        // Too sleepy for the idle head tilts and the pursed lips; blinks run slow.
        private bool actQuiet;
        private double drowsyPace = 1;
        private int zCount;

        // A long phrase sent with Enter.
        private int phraseKeys, burstKeys;
        private double lastPress = -100, triumphStart = -100, lastTriumph = -100;
        // Hello.
        private double waveStart = -100, handCalm;
        // Thinking after a burst of typing.
        private double thinkStart = -100, lastThink = -100, thinkDelay = 2.4;
        // Petting: the pointer rubbing back and forth over the head.
        private bool onHead, petting, heartSide;
        private int petDirection;
        private double petExtreme, petReversal = -100, petReversalBefore = -100, petStarted = -100, lastOnHead = -100, nextHeart;
        // Frantic typing.
        private double typingRate, franticSince = -1, nextSweat;

        private const int Zee = 0, Bang = 1, Dot = 2, Spark = 3, Heart = 4, Sweat = 5;
        private struct Symbol
        {
            public int Kind;
            public double Born, Life, X, Y, Size, Seed, VX, VY;
        }
        private readonly List<Symbol> symbols = new List<Symbol>();

        // The head, for telling a pointer on it from one passing by.
        private readonly Point HeadCenter;
        private readonly double HeadRadiusX, HeadRadiusY;
        // Symbols appear in the open air beside the head.
        private readonly Point SymbolAnchor;

        // Says hello with a wave (at start-up and when the companion is shown again).
        public void Greet()
        {
            if (Scenes && tune.ActGreet.Value > 0) waveStart = time + .45;
        }

        internal bool Asleep { get { return asleep; } }
        internal bool Petting { get { return petting; } }
        internal bool Waving { get { return time >= waveStart && time - waveStart < WaveSeconds; } }
        internal bool Beaming { get { return time >= triumphStart && time - triumphStart < 1.2; } }
        internal bool Thinking { get { return time >= thinkStart && time - thinkStart < 2.6; } }
        internal int SymbolCount { get { return symbols.Count; } }
        internal int CountSymbols(int kind)
        {
            int count = 0;
            foreach (Symbol symbol in symbols) if (symbol.Kind == kind) count++;
            return count;
        }
        internal double BodyDrop { get { return Math.Max(0, thump.Value); } }
        internal double EyesHeld { get { return actEyes; } }
        internal double MouthShown { get { return mouthShown; } }

        // True while acting moves fast enough to need the full frame rate.
        private bool ActingFast
        {
            get
            {
                if (thump.Value != 0 || thump.Velocity != 0 || petting || Waving || Beaming) return true;
                if (time - startleStart < .7 || time - thinkStart < 1.4 && time >= thinkStart) return true;
                foreach (Symbol symbol in symbols)
                    if (symbol.Kind != Zee && time >= symbol.Born) return true;
                return false;
            }
        }

        private const double WaveSeconds = 1.9;

        // A new key went down (called after BeginStroke, so the stroke's timing is known).
        private void ActOnKey(bool accent, bool repeat)
        {
            if (!acting) return;
            // Typing ends a wave and a thought at once.
            if (Waving) waveStart = -100;
            if (time >= thinkStart && time - thinkStart < 3) EndThought();
            if (repeat) return;
            if (time - lastPress > 3) phraseKeys = 0;
            if (time - lastPress > 1.2) burstKeys = 0;
            lastPress = time;
            typingRate += 1;
            if (!accent)
            {
                phraseKeys++;
                burstKeys++;
                return;
            }
            if (tune.ActTriumph.Value > 0 && phraseKeys >= tune.ActTriumphKeys.Value && time - lastTriumph > 12)
            {
                // The eyes are already squeezed shut for the landing; they stay shut in a smile.
                triumphStart = time + strokeWind;
                lastTriumph = time;
                Add(Spark, triumphStart + .10, .75, 80, 20, 1);
                Add(Spark, triumphStart + .19, .75, 215, 115, .7);
                Add(Spark, triumphStart + .27, .75, 10, -120, .58);
            }
            phraseKeys = burstKeys = 0;
        }

        private void EndThought()
        {
            thinkStart = -100;
            // The dots pop away instead of lingering over the new keystroke.
            CutShort(Dot, .12);
        }

        // Symbols of one kind vanish within the given time; those not yet shown never appear.
        private void CutShort(int kind, double within)
        {
            for (int i = 0; i < symbols.Count; i++)
            {
                Symbol symbol = symbols[i];
                if (symbol.Kind != kind) continue;
                if (symbol.Born > time) symbol.Life = 0;
                else symbol.Life = Math.Min(symbol.Life, time - symbol.Born + within);
                symbols[i] = symbol;
            }
        }

        private void Add(int kind, double born, double life, double dx, double dy, double size)
        {
            double scale = tune.ActSymbols.Value;
            if (scale <= 0) return;
            symbols.Add(new Symbol { Kind = kind, Born = born, Life = life, X = SymbolAnchor.X + dx, Y = SymbolAnchor.Y + dy,
                Size = size * scale, Seed = actRandom.NextDouble() * 6.283 });
        }

        // The hand's own acting: waving, holding a triumphant pose, drumming. Adjusts what
        // StepKeyboardHand is about to ask of the springs.
        private void ActOnHand(double amount, double age, double strikeEnd, ref bool typing, ref double target,
            ref double frequency, ref double damping, ref double reachX, ref double reachY, ref double reachFrequency,
            ref double wrist)
        {
            if (!acting) return;
            double reach = Clamp(amount, .5, 1.2);
            double wave = time - waveStart;
            if (wave >= 0 && wave < WaveSeconds - .4)
            {
                // Up beside the keyboard, then the wrist swings three times and a bit.
                typing = true;
                // The hand is lifted in an easy arc (a keystroke snaps up; a greeting does not),
                // the fingers hanging behind the wrist on the way, and only starts to wave
                // once it is up. The little bob belongs to the waving, not to the lift.
                double swing = Clamp((wave - .4) / .25, 0, 1) * Clamp((1.5 - wave) / .2, 0, 1);
                target = WaveHand[0] * Clamp(amount, .6, 1.1) + 5 * swing * Math.Sin(wave * 9);
                bool rising = wave < .34;
                frequency = rising ? 10 : 15; damping = rising ? .9 : .72;
                reachX = WaveHand[1] * reach; reachY = WaveHand[2] * reach; reachFrequency = 11;
                double trailing = Clamp(lift.Velocity * tune.KeyFlick.Value, -tune.KeyFlickMax.Value, tune.KeyFlickMax.Value);
                wrist = trailing * (1 - swing) + 21 * swing * Math.Sin((wave - .4) * 2 * Math.PI * 3);
                return;
            }
            double beam = time - triumphStart;
            if (beam >= .13 && beam < .68 && age >= strikeEnd + .07)
            {
                // Follow-through: the hand stays flung up over the key it just hit.
                target = TriumphLift * reach;
                frequency = 26; damping = .5;
                return;
            }
            double drum = time - thinkStart - .45;
            if (!typing && drum >= 0 && drum < .66)
            {
                // Three light taps in place.
                target = drum % .22 < .09 ? 17 * reach : -2;
                frequency = 62; damping = .5;
            }
        }

        private void StepActing(double dt, bool keyDown, bool mouseDown, double pointerX, double pointerY, bool reducedMotion)
        {
            bool on = Scenes && !reducedMotion;
            // A hand held up to wave keeps (nearly) its own size: the swelling that gives a
            // keystroke its punch looks like a blown-up hand when it stays in the air.
            bool held = on && time >= waveStart && time - waveStart < WaveSeconds - .4;
            handCalm += ((held ? 1 : 0) - handCalm) * (1 - Math.Exp(-dt / .12));
            if (handCalm < .001) handCalm = 0;
            actHeadY = actHeadAngle = 0;
            headYFrequency = 8; headYDamping = 1; headAngleFrequency = 5; headAngleDamping = .85;
            if (!on)
            {
                // Nothing lingers when acting is switched off mid-scene.
                if (acting)
                {
                    symbols.Clear();
                    asleep = petting = false;
                    waveStart = triumphStart = thinkStart = startleStart = yawnStart = -100;
                    sleepLevel = 0;
                    actQuiet = false;
                    drowsyPace = 1;
                    stirSince = -1;
                }
                acting = false;
                actEyes = Math.Max(0, actEyes - dt / .11);
                actMouth = Math.Max(0, actMouth - dt / .09);
                thump.Step(0, dt, 24, .4);
                if (thump.Value < 0 && Math.Abs(thump.Velocity) < 1) thump.Constrain(0, 0);
                mouthShown = Math.Max(mouth, actMouth);
                lastActivity = time;
                return;
            }
            acting = true;

            // Anything the user does counts as company: a key, a button, a real move of the
            // pointer (not the tremble of a mouse at rest), a voice.
            double moved = Math.Abs(pointerX - lastPointerX) + Math.Abs(pointerY - lastPointerY);
            lastPointerX = pointerX; lastPointerY = pointerY;
            pointerTravel = pointerTravel * Math.Exp(-dt / .5) + (actStarted ? moved : 0);
            StepPetting();
            bool active = keyDown || mouseDown || pointerTravel > .03 || mouth > .05 || petting || !actStarted;
            actStarted = true;
            if (active && asleep)
            {
                // A key, a click or a voice wakes with a start. The pointer alone is given a
                // moment to show what it is: a nudge wakes gently, a sweep startles.
                bool abrupt = keyDown || mouseDown || mouth > .05;
                if (!abrupt && stirSince < 0) stirSince = time;
                if (abrupt || time - stirSince >= .14)
                {
                    asleep = false;
                    stirSince = -1;
                    if (time - asleepSince <= 1.2 || (!abrupt && pointerTravel < .16))
                    {
                        gentleWake = time;
                        CutShort(Zee, .5);
                        nextBlink = time + .75;
                        blinksToFollow = 1;
                    }
                    else Startle();
                }
                else active = false;
            }
            else if (asleep && stirSince >= 0 && time - stirSince >= .14) stirSince = -1;
            if (active)
            {
                lastActivity = time;
                yawned = false;
            }
            double idle = time - lastActivity;
            actQuiet = false;
            drowsyPace = 1;

            bool eyesShut = false;
            double eyesClose = .09, eyesOpen = .11, mouthTarget = 0;
            bool mouthDirect = false;

            // ---- Dozing off ----
            double sleepAfter = tune.ActSleepAfter.Value;
            bool mayDoze = tune.ActSleep.Value > 0;
            double yawnAt = sleepAfter >= 40 ? sleepAfter - 22 : sleepAfter * .45;
            if (mayDoze && !yawned && idle >= yawnAt && idle < yawnAt + 1)
            {
                yawned = true;
                yawnStart = time;
            }
            if (mayDoze && idle >= yawnAt - 8) drowsyPace = 1.7;
            double yawn = time - yawnStart;
            if (yawn >= 0 && yawn < 2.6)
            {
                actQuiet = true;
                // A deep breath in with the head tipping back, a long hold, and out.
                if (yawn > .12 && yawn < 2.25) { eyesShut = true; eyesClose = .2; eyesOpen = .2; }
                double open = yawn < .25 ? 0 : (yawn < 1.0 ? 1 - Math.Pow(1 - (yawn - .25) / .75, 2) :
                    (yawn < 1.7 ? 1 : 1 - Smooth((yawn - 1.7) / .55)));
                mouthTarget = 1.5 * open + (yawn > 1.0 && yawn < 1.7 ? .04 * Math.Sin(yawn * 31) : 0);
                mouthDirect = true;
                actHeadAngle += -4.5 * Smooth(yawn / .9) * (1 - Smooth((yawn - 1.6) / .8));
                actHeadY += -4 * Smooth(yawn / .9) * (1 - Smooth((yawn - 1.6) / .5)) + 5 * Smooth((yawn - 1.7) / .4) * (1 - Smooth((yawn - 2.1) / .5));
                headYFrequency = 6; headAngleFrequency = 6;
                if (yawn + dt >= 2.6) { BlinkSoon(.25); blinksToFollow = 1; }
                yawnTear = Smooth((yawn - 1.5) / .4);
            }
            // Three heavy blinks, each longer and lower than the last; the third one stays shut.
            double doze = idle - (sleepAfter - 6);
            bool dozing = mayDoze && doze >= 0;
            if (dozing)
            {
                actQuiet = true;
                bool closed = doze < .5 || (doze >= 2.0 && doze < 3.0) || doze >= 4.4;
                double sink = doze < .5 ? 7 : (doze < 2.0 ? 2 : (doze < 3.0 ? 12 : (doze < 4.4 ? 5 : 15)));
                double lean = doze < .5 ? 1.2 : (doze < 2.0 ? .4 : (doze < 3.0 ? 2.4 : (doze < 4.4 ? 1 : 3.4)));
                if (closed) { eyesShut = true; eyesClose = .19; eyesOpen = .15; }
                actHeadY += sink;
                actHeadAngle += lean;
                // Sinking is slow; catching oneself is quick.
                headYFrequency = closed ? 4.2 : 9;
                headAngleFrequency = closed ? 3.6 : 7;
                if (doze >= 4.4 && !asleep)
                {
                    asleep = true;
                    asleepSince = time;
                    nextZ = time + 1.1;
                    zCount = 0;
                    nextDroop = time + 7 + 6 * actRandom.NextDouble();
                    headTilt = 0;
                }
            }
            sleepLevel += ((asleep ? 1 : 0) - sleepLevel) * (1 - Math.Exp(-dt / (asleep ? 1.5 : .12)));
            if (sleepLevel < .0005) sleepLevel = 0;
            if (asleep)
            {
                if (time >= nextDroop)
                {
                    droopStart = time;
                    nextDroop = time + 9 + 9 * actRandom.NextDouble();
                }
                // Nodding: the head sags further and further, then jerks back up.
                double droop = time - droopStart;
                if (droop < 2.5)
                {
                    actHeadY += 15 * Smooth(droop / 2.5);
                    actHeadAngle += 3.2 * Smooth(droop / 2.5);
                    headYFrequency = 5; headAngleFrequency = 5;
                }
                else if (droop < 2.85)
                {
                    actHeadY -= 10;
                    actHeadAngle -= 2.4;
                    headYFrequency = 24; headYDamping = .5;
                    headAngleFrequency = 18; headAngleDamping = .5;
                }
                else if (droop < 4.4) { headYFrequency = 4.5; headAngleFrequency = 4.5; }
                if (time >= nextZ)
                {
                    // z, Z, Z: three in a row, each bigger, then a pause.
                    int step = zCount++ % 3;
                    Add(Zee, time, 2.7, 6, 34, .5 + .2 * step);
                    nextZ = time + (step == 2 ? 2.6 : .8);
                }
            }

            if (!(yawn >= 0 && yawn < 2.6)) yawnTear = Math.Max(0, yawnTear - dt / .9);

            // ---- Woken with a start ----
            double start = time - startleStart;
            if (start >= 0 && start < .7)
            {
                eyesOpen = .03;
                headYFrequency = 34; headYDamping = .42;
                headAngleFrequency = 22; headAngleDamping = .4;
                if (start < .12) actHeadY -= 9;
                if (start < .14) actHeadAngle -= 3.6;
            }
            else if (time - gentleWake < .6) eyesOpen = .32;

            // ---- A long phrase sent ----
            double beam = time - triumphStart;
            if (beam >= 0 && beam < 1.2)
            {
                if (beam < 1.08) eyesShut = true;
                mouthTarget = Math.Max(mouthTarget, beam >= .09 && beam < 1.0 ? 1 : 0);
                if (beam > .12 && beam < .95)
                {
                    actHeadAngle -= 3.4;
                    headAngleFrequency = 11; headAngleDamping = .55;
                    actHeadY -= 3;
                }
            }

            // ---- Hello ----
            double wave = time - waveStart;
            if (wave >= .2 && wave < WaveSeconds - .3)
            {
                eyesShut = true;
                mouthTarget = Math.Max(mouthTarget, 1);
                actHeadAngle -= 2.8;
                headAngleFrequency = 9; headAngleDamping = .6;
            }

            // ---- Being petted ----
            if (petting)
            {
                double pet = time - petStarted;
                eyesShut = true;
                mouthTarget = Math.Max(mouthTarget, .8);
                // The head gives under the hand and rocks along with it.
                actHeadY += 5;
                actHeadAngle += 3.6 * Clamp((PointerOnArt.X - HeadCenter.X) / HeadRadiusX, -1, 1);
                headAngleFrequency = 10; headAngleDamping = .7;
                if (pet >= .15 && time >= nextHeart)
                {
                    heartSide = !heartSide;
                    Add(Heart, time, 1.5, (heartSide ? 40 : -75) + 60 * actRandom.NextDouble(), 15 - 40 * actRandom.NextDouble(),
                        .72 + .28 * actRandom.NextDouble());
                    nextHeart = time + .42 + .2 * actRandom.NextDouble();
                }
            }

            // ---- Thinking ----
            if (tune.ActThink.Value > 0 && burstKeys >= 6 && time - lastPress >= thinkDelay)
            {
                burstKeys = 0;
                thinkDelay = 2.1 + 1.1 * actRandom.NextDouble();
                if (time - lastThink > 18 && actRandom.NextDouble() < .7)
                {
                    thinkStart = lastThink = time;
                    headTilt = (actRandom.NextDouble() < .5 ? -1 : 1) * (1.9 + .8 * actRandom.NextDouble()) * tune.HeadTiltSize.Value;
                    nextTilt = time + 2.5;
                    BlinkSoon(.1);
                    for (int i = 0; i < 3; i++) Add(Dot, time + .3 + .26 * i, 2.1 - .26 * i, 52 + 58 * i, 20 - 8 * i, 1);
                }
            }

            if (time >= thinkStart && time - thinkStart < .7) { headAngleFrequency = 8; headAngleDamping = .7; }

            // ---- Frantic typing ----
            typingRate *= Math.Exp(-dt);
            if (tune.ActSweat.Value > 0 && typingRate > 7)
            {
                if (franticSince < 0) { franticSince = time; nextSweat = time + 2.4; }
                if (time >= nextSweat)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        double angle = (-62 + 44 * i + 16 * actRandom.NextDouble()) * Math.PI / 180;
                        int at = symbols.Count;
                        Add(Sweat, time + .07 * i, .55, -30 + 18 * i, 40 - 40 * i, 1.05 + .25 * actRandom.NextDouble());
                        if (symbols.Count > at)
                        {
                            Symbol drop = symbols[at];
                            drop.VX = 290 * Math.Cos(angle); drop.VY = 290 * Math.Sin(angle);
                            symbols[at] = drop;
                        }
                    }
                    nextSweat = time + .55 + .35 * actRandom.NextDouble();
                }
            }
            else if (typingRate < 5) franticSince = -1;

            // ---- The face ----
            if (eyesShut) actEyes = Math.Min(1, actEyes + dt / eyesClose);
            else actEyes = Math.Max(0, actEyes - dt / eyesOpen);
            if (mouthDirect) actMouth += (mouthTarget - actMouth) * (1 - Math.Exp(-dt / .03));
            else actMouth += (mouthTarget - actMouth) * (1 - Math.Exp(-dt / (mouthTarget > actMouth ? .05 : .09)));
            if (actMouth < .006 && mouthTarget == 0) actMouth = 0;
            mouthShown = Math.Max(mouth, actMouth);

            // ---- The body ----
            double bodyDrop = accentImpact ? tune.ActThump.Value * (Beaming || time < triumphStart ? 1.35 : 1) : 0;
            if (start >= 0 && start < .07) bodyDrop = Math.Max(bodyDrop, 9);
            // Down with the blow, then a slower bounce back.
            thump.Step(bodyDrop, dt, bodyDrop > 0 ? 46 : 26, .42);

            for (int i = symbols.Count - 1; i >= 0; i--)
                if (time >= symbols[i].Born + symbols[i].Life) symbols.RemoveAt(i);
        }

        private void Startle()
        {
            startleStart = time;
            Add(Bang, time + .02, .85, 62, -44, 1);
            // The sleep bubbles burst.
            CutShort(Zee, .1);
            // Wide awake: a double blink a moment later, lips pressed together.
            nextBlink = time + .5;
            blinksToFollow = 1;
            purseUntil = time + .75;
        }

        // Rubbing means the pointer turning back on itself over the head, twice within a
        // moment; a pointer merely crossing the head is not petting.
        private void StepPetting()
        {
            Point pointer = PointerOnArt;
            bool known = !double.IsNaN(pointer.X) && !double.IsNaN(pointer.Y) && tune.ActPet.Value > 0;
            double nx = (pointer.X - HeadCenter.X) / HeadRadiusX, ny = (pointer.Y - HeadCenter.Y) / HeadRadiusY;
            bool inside = known && nx * nx + ny * ny < 1;
            if (inside)
            {
                if (!onHead)
                {
                    petDirection = 0;
                    petExtreme = pointer.X;
                    // Noticed: a blink as the pointer arrives.
                    if (time - lastOnHead > 3 && !asleep) BlinkSoon(.08);
                }
                const double turn = 36;
                bool reversed = false;
                if (petDirection >= 0 && pointer.X > petExtreme) petExtreme = pointer.X;
                if (petDirection <= 0 && pointer.X < petExtreme && petDirection != 0) petExtreme = pointer.X;
                if (petDirection == 0)
                {
                    if (Math.Abs(pointer.X - petExtreme) > turn) { petDirection = pointer.X > petExtreme ? 1 : -1; petExtreme = pointer.X; }
                }
                else if (petDirection > 0 && pointer.X < petExtreme - turn) { petDirection = -1; petExtreme = pointer.X; reversed = true; }
                else if (petDirection < 0 && pointer.X > petExtreme + turn) { petDirection = 1; petExtreme = pointer.X; reversed = true; }
                if (reversed)
                {
                    petReversalBefore = petReversal;
                    petReversal = time;
                    if (!petting && time - petReversalBefore < 1.3)
                    {
                        petting = true;
                        petStarted = time;
                        nextHeart = time + .15;
                    }
                }
                lastOnHead = time;
            }
            onHead = inside;
            if (petting && (!inside || time - petReversal > .9))
            {
                petting = false;
                BlinkSoon(.3);
            }
        }

        // ---- Drawing the symbols ----

        private readonly Brush Ink;
        private static readonly Brush SparkFill = MakeBrush(255, 211, 66),
            HeartFill = MakeBrush(240, 104, 110), SweatFill = MakeBrush(170, 213, 240);
        // Drawn with the artwork's own outline colour and weight. The shapes are sized for
        // the screen: 3 artboard pixels are about 1 pixel there.
        private readonly Pen ZeePen, SymbolPen;
        private static readonly Geometry ZeeShape = Sized("M -19,-21 L 19,-21 L -19,21 L 21,21", 2.2);
        private static readonly Geometry BangShape = Sized("M -13,-46 Q 0,-52 13,-46 L 6,12 Q 0,17 -6,12 Z", 2.1);
        private static readonly Geometry SparkShape = Sized(
            "M 0,-42 Q 4,-4 42,0 Q 4,4 0,42 Q -4,4 -42,0 Q -4,-4 0,-42 Z", 1.6);
        private static readonly Geometry HeartShape = Sized(
            "M 0,30 C -34,6 -40,-12 -30,-24 C -20,-36 -5,-31 0,-17 C 5,-31 20,-36 30,-24 C 40,-12 34,6 0,30 Z", 1.5);
        private static readonly Geometry SweatShape = Sized(
            "M 0,-24 C 9,-8 15,1 15,10 C 15,19 8,25 0,25 C -8,25 -15,19 -15,10 C -15,1 -9,-8 0,-24 Z", 1.45);

        private static Geometry Sized(string path, double scale)
        {
            Geometry shape = Geometry.Parse(path).Clone();
            shape.Transform = new ScaleTransform(scale, scale);
            return Frozen(shape);
        }

        private static Brush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // Pops in a little too big and settles, as hand-drawn effects do.
        private static double Pop(double age, double time)
        {
            if (age < time * .6) return .3 + .95 * Smooth(age / (time * .6));
            if (age < time) return 1.25 - .25 * Smooth((age - time * .6) / (time * .4));
            return 1;
        }

        private void DrawSymbols(DrawingContext dc)
        {
            foreach (Symbol symbol in symbols)
            {
                double age = time - symbol.Born;
                if (age < 0 || age >= symbol.Life) continue;
                double life = age / symbol.Life, left = symbol.Life - age;
                double x = symbol.X, y = symbol.Y, scale = symbol.Size, turn = 0, opacity = 1;
                switch (symbol.Kind)
                {
                    case Zee:
                        // Drifts up and away, swaying, growing as it goes.
                        // Quick away from the head, then slowing, so one follows another without touching.
                        double gone = 1 - Math.Pow(1 - life, 1.7);
                        x += 132 * gone + 10 * Math.Sin(age * 2.6 + symbol.Seed) * life;
                        y -= 136 * gone;
                        scale *= .62 + .5 * life;
                        turn = -9 + 7 * Math.Sin(age * 2.1 + symbol.Seed);
                        opacity = Math.Min(1, age / .3) * Math.Min(1, left / .8);
                        break;
                    case Bang:
                        scale *= Pop(age, .16);
                        x += age < .3 ? 3.5 * Math.Sin(age * 60) : 0;
                        turn = 11;
                        opacity = Math.Min(1, left / .18);
                        break;
                    case Dot:
                        scale *= Pop(age, .14) * Math.Min(1, left / .12);
                        y -= 3 * Math.Sin(Math.Min(age, 1.2) * 5);
                        break;
                    case Spark:
                        scale *= Pop(age, .17) * (life < .55 ? 1 : 1 - Smooth((life - .55) / .45));
                        turn = 18 + 50 * life;
                        break;
                    case Heart:
                        x += 15 * Math.Sin(age * 3.4 + symbol.Seed);
                        y -= 100 * (1 - Math.Pow(1 - life, 1.6));
                        scale *= Pop(age, .2);
                        turn = 9 * Math.Sin(age * 3.4 + symbol.Seed + 1);
                        opacity = Math.Min(1, left / .45);
                        break;
                    default:
                        // A drop flung off the head, falling as it flies.
                        x += symbol.VX * age;
                        y += symbol.VY * age + 420 * age * age;
                        turn = Math.Atan2(symbol.VY + 840 * age, symbol.VX) * 180 / Math.PI - 90;
                        scale *= Pop(age, .08);
                        opacity = Math.Min(1, left / .2);
                        break;
                }
                if (scale <= 0 || opacity <= 0) continue;
                var place = Matrix.Identity;
                place.Scale(scale, scale);
                place.Rotate(turn);
                place.Translate(x, y);
                dc.PushTransform(new MatrixTransform(place));
                if (opacity < 1) dc.PushOpacity(opacity);
                switch (symbol.Kind)
                {
                    case Zee: dc.DrawGeometry(null, ZeePen, ZeeShape); break;
                    case Bang:
                        dc.DrawGeometry(Ink, null, BangShape);
                        dc.DrawEllipse(Ink, null, new Point(0, 76), 21, 21);
                        break;
                    case Dot: dc.DrawEllipse(Ink, null, new Point(0, 0), 17, 17); break;
                    case Spark: dc.DrawGeometry(SparkFill, SymbolPen, SparkShape); break;
                    case Heart: dc.DrawGeometry(HeartFill, SymbolPen, HeartShape); break;
                    default: dc.DrawGeometry(SweatFill, SymbolPen, SweatShape); break;
                }
                if (opacity < 1) dc.Pop();
                dc.Pop();
            }
        }
    }
}
