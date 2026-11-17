using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SumikkoCompanion;

internal static class RegressionTests
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        CalibrationChecks.Run(Assert);
        AudioDeviceChecks.Run(Assert);
        using(var signals = new NativeSignals())
        {
            // Times are in seconds; each poll is one 60 fps frame unless stated.
            double now=0,frame=1.0/60;
            signals.PollKeyboard(k=>false,now+=frame);
            Assert(!signals.KeyPressed,"idle has no press");
            signals.PollKeyboard(k=>k==65,now+=frame);
            Assert(signals.KeyPressed && !signals.KeyRepeated,"new key produces a pulse");
            double pressedAt=now;
            int early=0,repeats=0;
            for(int i=0;i<120;i++)
            {
                signals.PollKeyboard(k=>k==65,now+=frame);
                if(!signals.KeyPressed)continue;
                if(now-pressedAt<NativeSignals.RepeatDelay-.001 || !signals.KeyRepeated)early++;
                repeats++;
            }
            Assert(early==0,"a held key waits before it starts repeating");
            // Two seconds held, minus the delay, at one pulse per repeat interval.
            Assert(repeats>=18 && repeats<=22,"a held key then hammers at the repeat rate");
            signals.PollKeyboard(k=>k==65||k==66,now+=frame);
            Assert(signals.KeyPressed && !signals.KeyRepeated,"overlapping second key still produces a new pulse");
            signals.PollKeyboard(k=>false,now+=frame);
            int afterRelease=0;
            for(int i=0;i<120;i++)
            {
                signals.PollKeyboard(k=>k==16||k==17||k==18||k>=240,now+=frame);
                if(signals.KeyPressed)afterRelease++;
            }
            Assert(afterRelease==0,"modifiers and IME reserved states neither tap nor repeat");
            signals.PollKeyboard(k=>k==65,now+=frame);
            Assert(signals.KeyPressed,"released key can be pressed again");
            using(var fresh=new NativeSignals())
            {
                int startup=0;
                for(int i=0;i<120;i++)
                {
                    fresh.PollKeyboard(k=>k==8,i*frame);
                    if(fresh.KeyPressed)startup++;
                }
                Assert(startup==0,"a key already held when the app starts stays quiet");
            }
            ResumeInputChecks();
            Assert(signals.AudioLevel==0,"audio starts off");
            // A stale saved endpoint must never silently capture the default microphone.
            signals.SetAudioSource(AudioSource.Microphone,"sumikko-nonexistent-test-endpoint");
            signals.Poll();
            Assert(signals.AudioLevel==0 && signals.AudioStatus.IndexOf("ready",StringComparison.OrdinalIgnoreCase)<0,
                "missing input endpoint does not fall back to a live microphone");
            signals.SetAudioSource(AudioSource.Playback,"sumikko-nonexistent-test-endpoint");
            signals.Poll();
            Assert(signals.AudioLevel==0 && signals.AudioStatus.IndexOf("ready",StringComparison.OrdinalIgnoreCase)<0,
                "missing playback endpoint does not fall back to default playback");
            signals.SetAudioSource(AudioSource.Off);
        }
        MicrophoneCleanupChecks();
        var view = new CharacterView(Path.Combine(Path.GetFullPath(args[0]),"assets"));
        // These checks are about the plain motion; acting has its own (ActingChecks).
        view.Scenes = false;
        view.Measure(new Size(1469,1071));
        view.Arrange(new Rect(0,0,1469,1071));
        view.Tick(0,1.0/60,false,false,0,0,0,false,false);
        byte[] resting = KeyboardPixels(view);
        for(int i=1;i<=4;i++) view.Tick(i/60.0,1.0/60,i==1,false,0,0,0,false,false);
        byte[] pressed = KeyboardPixels(view);
        Assert(!Equal(resting,pressed),"one press visibly moves keyboard hand");
        for(int i=5;i<=180;i++) view.Tick(i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(view.KeyboardHandAtRest,"keyboard hand settles to exact rest");
        Assert(Equal(resting,KeyboardPixels(view)),"keyboard hand returns to idle rendering");
        for(int i=181;i<=360;i++) view.Tick(i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(Equal(resting,KeyboardPixels(view)),"idle keyboard hand stays motionless");
        KeyboardHandChecks(Path.Combine(Path.GetFullPath(args[0]),"assets"));
        // Across-the-desk composition: the character's local mouse axes oppose
        // the operator's screen axes. Exercise the actual animation, not only a formula.
        Point left = SettledMouse(view,-.5,0);
        Point right = SettledMouse(view,.5,0);
        Point up = SettledMouse(view,0,-.5);
        Point down = SettledMouse(view,0,.5);
        Assert(right.X < left.X,"mouse horizontal direction matches character perspective");
        Assert(down.Y < up.Y,"mouse vertical direction matches character perspective");
        var range=new MouseMotionRange(-30,70,-60,-10);
        Assert(range.Map(1,1)==new Point(-30,-60) && range.Map(-1,-1)==new Point(70,-10),
            "custom endpoints preserve inverted directions");
        Assert(range.Map(4,-8)==new Point(-30,-10),"out-of-screen input clamps to range endpoints");
        view.MouseRange=range;
        for(int i=0;i<240;i++)
        {
            view.Tick(20+i/60.0,1.0/60,false,true,i%30<15?-1:1,i%40<20?-1:1,0,false,false);
            Point p=view.MouseOffset;
            if(p.X<range.Left || p.X>range.Right || p.Y<range.Top || p.Y>range.Bottom)
                throw new Exception("Animated mouse escaped range");
        }
        Assert(true,"spring motion and clicking remain inside configured range");
        view.MouseRange=new MouseMotionRange(-5,5,-25,-15);
        view.Tick(25,1.0/60,false,true,-1,-1,0,false,false);
        Point small=view.MouseOffset;
        Assert(small.X>=-5 && small.X<=5 && small.Y>=-25 && small.Y<=-15,
            "shrinking range immediately constrains existing momentum");
        view.MouseRange=new MouseMotionRange(12,12,-20,-20);
        view.Tick(26,1.0/60,false,true,1,1,0,false,false);
        Assert(view.MouseOffset==new Point(12,-20),"equal endpoints lock both axes including clicks");
        // Idle life of the mouse hand: a few pixels of drift, none while calibrating.
        view.MouseRange=MouseMotionRange.Default;
        SettledMouse(view,0,0);
        double driftLeft=double.MaxValue,driftRight=double.MinValue,driftTop=double.MaxValue,driftBottom=double.MinValue;
        for(int i=0;i<1200;i++)
        {
            view.Tick(50+i/60.0,1.0/60,false,false,0,0,0,false,false);
            Point p=view.MouseOffset;
            driftLeft=Math.Min(driftLeft,p.X); driftRight=Math.Max(driftRight,p.X);
            driftTop=Math.Min(driftTop,p.Y); driftBottom=Math.Max(driftBottom,p.Y);
        }
        Assert(driftRight-driftLeft>4 && driftRight-driftLeft<14,"an idle mouse hand drifts a few pixels sideways");
        Assert(driftBottom-driftTop>2 && driftBottom-driftTop<9,"an idle mouse hand drifts slightly up and down");
        view.IdleMouseMotion=false;
        view.Tick(71,1.0/60,false,false,0,0,0,false,false);
        Point held=view.MouseOffset;
        bool steady=true;
        for(int i=0;i<300;i++)
        {
            view.Tick(71.1+i/60.0,1.0/60,false,false,0,0,0,false,false);
            if(view.MouseOffset!=held)steady=false;
        }
        Assert(steady,"the mouse hand holds still while its range is calibrated");
        // The shoulder outline runs unbroken from the sleeve to the torso wherever the hand is,
        // and the sleeve's soft cap no longer shows outside it.
        bool joined=true,trimmed=true;
        double[] reaches={0,0, -17,8, 118,-70, 118,8};
        for(int i=0;i<reaches.Length;i+=2)
        {
            view.MouseRange=new MouseMotionRange(reaches[i],reaches[i],reaches[i+1],reaches[i+1]);
            view.Tick(80+i,1.0/60,false,false,0,0,0,false,false);
            // Two points on the sleeve's side of the outline move with the sleeve; the hair's
            // own dark tips may pass over the rest of it, which still reads as an unbroken line.
            double shear=reaches[i]/260, stretch=1+reaches[i+1]/260;
            if(Luminance(view,385+17.5*shear,580+17.5*stretch)>70 || Luminance(view,395+13*shear,580+13*stretch)>70)joined=false;
            double upper=Luminance(view,414,585.5), middle=Luminance(view,404,589.5);
            if(i==0 && (upper<0 || upper>70 || middle<0 || middle>70))joined=false;
            if(Luminance(view,376,594)>=0)trimmed=false;
        }
        Assert(joined,"the near shoulder's outline joins the sleeve to the torso");
        Assert(trimmed,"nothing of the sleeve shows outside the shoulder outline");
        view.IdleMouseMotion=true;
        // Mouse hand follow-through: it trails a quick move, flattens on a click, then is still.
        view.MouseRange=MouseMotionRange.Default;
        SettledMouse(view,-.8,0);
        double trailing=0,leading=0;
        for(int i=0;i<90;i++)
        {
            view.Tick(27+i/60.0,1.0/60,false,false,.8,0,0,false,false);
            trailing=Math.Max(trailing,view.MouseHandLean);
            leading=Math.Min(leading,view.MouseHandLean);
        }
        Assert(trailing>.03,"the mouse trails behind a quick sideways move");
        Assert(leading<-.002,"the mouse swings back after the move stops");
        for(int i=0;i<90;i++) view.Tick(29+i/60.0,1.0/60,false,false,.8,0,0,false,false);
        Assert(view.MouseHandLean==0 && view.MouseHandSquash==0,"a still mouse hand keeps the original artwork");
        double squashed=0,popped=0,burst=-1,burstEnd=-1;
        Assert(view.MouseClickPop<0,"no click marks appear before a click");
        for(int i=0;i<60;i++)
        {
            view.Tick(31+i/60.0,1.0/60,false,i<20,.8,0,0,false,false);
            squashed=Math.Max(squashed,view.MouseHandSquash);
            if(i==1)burst=view.MouseClickPop;
            if(i==19)burstEnd=view.MouseClickPop;
            popped=Math.Min(popped,view.MouseHandSquash);
        }
        Assert(squashed>.07 && squashed<.16,"a click visibly flattens the mouse hand");
        Assert(burst>=0 && burst<.3,"pressing the button bursts click marks from the mouse");
        Assert(burstEnd<0,"click marks are gone a moment later, even while the button is held");
        Assert(popped<0,"releasing the button pops the hand back past rest");
        for(int i=0;i<120;i++) view.Tick(32+i/60.0,1.0/60,false,false,.8,0,0,false,false);
        Assert(view.MouseHandLean==0 && view.MouseHandSquash==0,"the mouse hand settles exactly after a click");
        double calm=0;
        SettledMouse(view,-.8,0);
        for(int i=0;i<90;i++)
        {
            view.Tick(40+i/60.0,1.0/60,false,false,.8,0,0,false,true);
            calm=Math.Max(calm,Math.Abs(view.MouseHandLean));
        }
        Assert(calm<trailing*.5,"reduced motion calms the mouse follow-through");
        var invalid=new MouseMotionRange(double.NaN,double.PositiveInfinity,1000,-1000);
        Assert(invalid.Left==-120 && invalid.Right==120 && invalid.Top==-160 && invalid.Bottom==8,
            "invalid and reversed stored endpoints normalize safely");
        double smallMouth=CharacterView.LipSyncTestLevel(.4);
        double mediumMouth=CharacterView.LipSyncTestLevel(1.2);
        double largeMouth=CharacterView.LipSyncTestLevel(2.0);
        Assert(smallMouth>0 && smallMouth<mediumMouth && mediumMouth<largeMouth,
            "mouth test supplies progressively larger openings");
        Assert(CharacterView.LipSyncTestLevel(3.2)==0 && CharacterView.LipSyncTestLevel(double.NaN)==0,
            "mouth test includes silence and rejects invalid time");
        var mouthField=typeof(CharacterView).GetField("mouth",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        double maxOpening=0;
        for(int i=0;i<252;i++)
        {
            view.Tick(30+i/60.0,1.0/60,false,false,0,0,CharacterView.LipSyncTestLevel(i/60.0),false,false);
            maxOpening=Math.Max(maxOpening,(double)mouthField.GetValue(view));
        }
        Assert(maxOpening>.9,"synthetic mouth test reaches the full audio-driven opening");
        Assert((double)mouthField.GetValue(view)==0,"mouth closes completely during test silence");
        Assert(view.KeyboardHandAtRest,"mouth-only test does not synthesize keyboard taps");
        LipSyncChecks(view);
        RedrawChecks(Path.Combine(Path.GetFullPath(args[0]),"assets"));
        SettingsChecks();
        TuningChecks.Run(Assert,Path.Combine(Path.GetFullPath(args[0]),"assets"));
        ActingChecks.Run(Assert,Path.Combine(Path.GetFullPath(args[0]),"assets"));
        CharacterChecks.Run(Assert,Path.GetFullPath(args[0]));
    }

    // After the companion was hidden: nothing held at the moment it returns counts as new.
    private static void ResumeInputChecks()
    {
        using(var signals=new NativeSignals())
        {
            signals.PollKeyboard(k=>false,0);
            signals.PollKeyboard(k=>k==65,.1);
            Assert(signals.KeyPressed,"a key goes down before the companion is hidden");
            signals.ResetInputState();
            signals.PollKeyboard(k=>k==65||k==66,.2);
            int fired=signals.KeyPressed?1:0;
            for(int i=1;i<=90;i++)
            {
                signals.PollKeyboard(k=>k==65||k==66,.2+i/60.0);
                if(signals.KeyPressed)fired++;
            }
            Assert(fired==0,"keys held when the companion returns neither tap nor repeat");
            signals.PollKeyboard(k=>false,2);
            signals.PollKeyboard(k=>k==67,2.1);
            Assert(signals.KeyPressed,"a key pressed after the return is a press");
            signals.PollButtons(k=>false);
            signals.PollButtons(k=>k==1);
            Assert(signals.MousePressed,"a held mouse button is reported");
            signals.ResetInputState();
            signals.PollButtons(k=>k==1);
            Assert(!signals.MousePressed,"the double-click that brings the companion back is not a click on its mouse");
            signals.PollButtons(k=>false);
            signals.PollButtons(k=>k==2);
            Assert(signals.MousePressed,"the next click counts again");
        }
    }

    private static void LipSyncChecks(CharacterView view)
    {
        double before=Math.Sqrt((.1-.009)/.21);
        Assert(Math.Abs(CharacterView.MouthOpening(.1,CharacterView.DefaultLipSyncGain,CharacterView.DefaultLipSyncFloor)-before)<1e-12,
            "the default lip sync sensitivity opens the mouth as before");
        Assert(CharacterView.MouthOpening(.008,1,.009)==0 && CharacterView.MouthOpening(.03,1,.009)>0,
            "sound below the floor keeps the mouth shut");
        Assert(CharacterView.MouthOpening(.03,2,.009)>CharacterView.MouthOpening(.03,1,.009),
            "a higher sensitivity opens the mouth wider");
        Assert(CharacterView.MouthOpening(.03,1,.05)==0,"a higher floor ignores more sound");
        Assert(CharacterView.MouthOpening(.5,100,-3)==CharacterView.MouthOpening(.5,CharacterView.MaxLipSyncGain,0),
            "out-of-range sensitivity values are clamped");
        var mouthField=typeof(CharacterView).GetField("mouth",BindingFlags.NonPublic|BindingFlags.Instance);
        view.LipSyncFloor=CharacterView.MaxLipSyncFloor;
        double opened=0;
        for(int i=0;i<60;i++)
        {
            view.Tick(60+i/60.0,1.0/60,false,false,0,0,.08,false,false);
            opened=Math.Max(opened,(double)mouthField.GetValue(view));
        }
        Assert(opened==0,"the character's mouth follows its own floor setting");
        view.LipSyncFloor=CharacterView.DefaultLipSyncFloor;
        for(int i=0;i<60;i++) view.Tick(61+i/60.0,1.0/60,false,false,0,0,0,false,false);
    }

    // Only frames that look different are drawn again, and what needs a smooth frame rate says so.
    private static void RedrawChecks(string assets)
    {
        var view=NewView(assets,1);
        for(int i=0;i<600;i++) view.Tick(1+i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(!view.NeedsFullFrameRate,"a character at rest can run at the idle frame rate");
        int before=view.Redraws;
        for(int i=0;i<120;i++) view.Tick(11+i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(view.Redraws-before>=115,"the idle sways are redrawn every frame");
        view.Tick(13,1.0/60,true,false,0,0,0,false,false);
        Assert(view.NeedsFullFrameRate,"a key press asks for the full frame rate");
        for(int i=1;i<=180;i++) view.Tick(13+i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(!view.NeedsFullFrameRate,"after the stroke settles the idle rate is enough again");
        view.Tick(17,1.0/60,false,true,0,0,0,false,false);
        Assert(view.NeedsFullFrameRate,"a click asks for the full frame rate");
        for(int i=1;i<=180;i++) view.Tick(17+i/60.0,1.0/60,false,false,0,0,0,false,false);
        for(int i=0;i<10;i++) view.Tick(21+i/60.0,1.0/60,false,false,0,0,.15,false,false);
        Assert(view.NeedsFullFrameRate,"a moving mouth asks for the full frame rate");
        for(int i=0;i<120;i++) view.Tick(22+i/60.0,1.0/60,false,false,0,0,0,false,false);
        for(int i=0;i<30;i++) view.Tick(24+i/60.0,1.0/60,false,false,i<15?-.8:.8,0,0,false,false);
        Assert(view.NeedsFullFrameRate,"a hand chasing the pointer asks for the full frame rate");

        // Reduced motion stops the sways: once settled, only blinks and the odd pursed mouth are drawn.
        view=NewView(assets,1);
        for(int i=0;i<600;i++) view.Tick(1+i/60.0,1.0/60,false,false,0,0,0,false,true);
        before=view.Redraws;
        int still=0;
        for(int i=0;i<1200;i++)
        {
            int drawnBefore=view.Redraws;
            view.Tick(11+i/60.0,1.0/60,false,false,0,0,0,false,true);
            if(view.Redraws==drawnBefore)still++;
        }
        Assert(still>600,"a settled character with reduced motion skips the frames that would look the same");
        Assert(view.Redraws>before,"blinks are still drawn with reduced motion");
        byte[] settled=KeyboardPixels(view);
        int pressedAt=view.Redraws;
        view.Tick(31,1.0/60,true,false,0,0,0,false,true);
        Assert(view.Redraws==pressedAt+1,"the frame of a key press is always drawn");
        for(int i=1;i<=240;i++) view.Tick(31+i/60.0,1.0/60,false,false,0,0,0,false,true);
        Assert(Equal(settled,KeyboardPixels(view)),"after a skipped stretch the picture is the settled one again");
    }

    // A microphone driver that will not give its buffers back holds up audio only.
    private static void MicrophoneCleanupChecks()
    {
        using(var signals=new NativeSignals())
        {
            signals.PollKeyboard(k=>false,0);
            signals.PollKeyboard(k=>k==65,1.0/60);
            Assert(signals.KeyPressed,"a key press is reported before the microphone stalls");
            SetMicrophoneStuck(signals,true);
            signals.SetAudioSource(AudioSource.Off);
            Assert(signals.AudioStatus=="Microphone cleanup pending","switching audio off waits for a microphone that is still held");
            typeof(NativeSignals).GetProperty("MouseX").GetSetMethod(true).Invoke(signals,new object[] { -54321.0 });
            int attempts=signals.CleanupAttempts;
            bool pulseCleared=false;
            for(int i=0;i<10;i++)
            {
                signals.Poll();
                if(!signals.KeyPressed)pulseCleared=true;
            }
            Assert(pulseCleared,"a stalled microphone does not leave the last key press firing every frame");
            Assert(signals.MouseX!=-54321.0,"the cursor keeps updating while the microphone is being released");
            Assert(signals.AudioLevel==0 && signals.AudioStatus=="Microphone cleanup pending",
                "audio stays off and no device is reopened until the driver lets go");
            Assert(signals.CleanupAttempts-attempts<=1,"a driver that holds on is asked again a few times a second, not every frame");
            SetMicrophoneStuck(signals,false);
            var waited=System.Diagnostics.Stopwatch.StartNew();
            while(signals.AudioStatus!="Off" && waited.ElapsedMilliseconds<2000)
            {
                signals.Poll();
                System.Threading.Thread.Sleep(20);
            }
            Assert(signals.AudioStatus=="Off","audio settles once the driver releases its buffers");
        }
    }

    // A prepared buffer without an open device can never be unprepared, so StopMicrophone()
    // keeps reporting cleanup pending, as with a driver that will not let go. No driver is called.
    private static void SetMicrophoneStuck(NativeSignals signals,bool stuck)
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        Type type=typeof(NativeSignals);
        Type bufferType=type.GetNestedType("WaveBuffer",BindingFlags.NonPublic);
        if(!stuck)
        {
            var held=(Array)type.GetField("microphoneBuffers",flags).GetValue(signals);
            bufferType.GetField("Prepared").SetValue(held.GetValue(0),false);
            return;
        }
        object buffer=Activator.CreateInstance(bufferType,true);
        bufferType.GetField("Prepared").SetValue(buffer,true);
        Array buffers=Array.CreateInstance(bufferType,1);
        buffers.SetValue(buffer,0);
        type.GetField("microphoneBuffers",flags).SetValue(signals,buffers);
        type.GetField("microphoneCleanupPending",flags).SetValue(signals,true);
    }

    private static void SettingsChecks()
    {
        string folder=Path.Combine(Path.GetTempPath(),"SumikkoCompanionChecks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path=Path.Combine(folder,"settings.txt");
            var store=new SettingsStore(path);
            Assert(store.Load()==null && store.LoadResult==SettingsLoadResult.Missing && store.CanSave,
                "a missing settings file is a first run, not a failure");
            var saved=new CompanionSettings { Size=560,Left=100.5,Top=-20,ClickThrough=true,ReducedMotion=true,MotionStrength=1.4,
                MouseRange=new MouseMotionRange(-17,118,-70,8),InputDeviceId="{0.0.1.00000000}.{a=b}",
                InputDeviceName="マイク（USB）=1",OutputDeviceId="out",OutputDeviceName="スピーカー",
                LipSyncGain=Math.Pow(2,1.25),LipSyncFloor=.031 };
            Assert(store.Save(saved) && File.Exists(path) && !File.Exists(path+".tmp"),"the first save creates the settings file");
            Assert(SameSettings(saved,new SettingsStore(path).Load(),true),"saved settings are restored exactly, including Unicode device names");
            saved.Size=320;
            Assert(store.Save(saved) && new SettingsStore(path).Load().Size==320 && !File.Exists(path+".tmp"),"a later save replaces the file");

            // Failed writes: the previous file stays exactly as it was.
            string before=File.ReadAllText(path);
            saved.Size=440;
            using(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert(!store.Save(saved) && store.SaveFailed && store.LastError!=null,"a save that cannot replace the file reports it");
            Assert(File.ReadAllText(path)==before && !File.Exists(path+".tmp"),"a failed save keeps the previous settings intact");
            Directory.CreateDirectory(path+".tmp");
            Assert(!store.Save(saved) && File.ReadAllText(path)==before,"a save that cannot start writing keeps the previous settings");
            Directory.Delete(path+".tmp");
            Assert(store.Save(saved) && !store.SaveFailed && store.LastError==null,"saving works again once the file is free");

            // Failed reads: nothing is saved over a file this run could not see.
            string kept=File.ReadAllText(path);
            var unread=new SettingsStore(path);
            using(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Write|FileShare.Delete))
            {
                Assert(unread.Load()==null && unread.LoadResult==SettingsLoadResult.Failed && unread.LastError!=null,
                    "an unreadable settings file is reported as a failure, not as missing");
                Assert(!unread.CanSave && !unread.Save(new CompanionSettings { Left=0,Top=0 }),"nothing is saved over a file that could not be read");
            }
            Assert(!unread.Save(new CompanionSettings { Left=0,Top=0 }) && File.ReadAllText(path)==kept,
                "the unread file stays untouched after it becomes readable");
            unread.AllowOverwrite();
            Assert(unread.Save(new CompanionSettings { Size=320,Left=0,Top=0 }) && new SettingsStore(path).Load().Size==320,
                "the user can still choose to overwrite it");

            CompanionSettings odd=CompanionSettings.Parse(new[] { "size=999","left=NaN","top=1e9","motionStrength=2",
                "inputDeviceId=***","no separator","clickThrough=yes","lipSyncGain=9","lipSyncFloor=-1" });
            Assert(odd.Size==440 && double.IsNaN(odd.Left) && double.IsNaN(odd.Top) && odd.MotionStrength==1 &&
                odd.InputDeviceId=="" && !odd.ClickThrough && odd.LipSyncGain==CharacterView.DefaultLipSyncGain &&
                odd.LipSyncFloor==CharacterView.DefaultLipSyncFloor,"invalid stored values fall back to the defaults");

            // Saving what the file already holds writes nothing (clicks, cancelled dialogs, start-up).
            string countedPath=Path.Combine(folder,"counted.txt");
            var counted=new SettingsStore(countedPath);
            var same=new CompanionSettings { Left=1,Top=2 };
            Assert(counted.Save(same) && counted.Save(same) && counted.Writes==1,"saving unchanged settings again writes nothing");
            same.Size=320;
            Assert(counted.Save(same) && counted.Writes==2,"a changed setting is written");
            File.Delete(countedPath);
            Assert(counted.Save(same) && counted.Writes==3 && File.Exists(countedPath),"a settings file removed meanwhile is written again");
            File.SetLastWriteTimeUtc(countedPath,DateTime.UtcNow.AddMinutes(-5));
            Assert(counted.Save(same) && counted.Writes==4,"a settings file changed by something else is written again");
            var reread=new SettingsStore(countedPath);
            Assert(reread.Save(reread.Load()) && reread.Writes==0,"saving exactly what was just loaded writes nothing");
            using(new FileStream(countedPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert(!counted.Save(new CompanionSettings { Left=1,Top=2,Size=560 }),"a save can still fail");
            Assert(counted.Save(same) && counted.Writes==5 && !counted.SaveFailed,"after a failed save the next one writes the file again");
            MouseMotionRange derived=CompanionSettings.Parse(new[] { "motionStrength=0.4" }).MouseRange;
            Assert(SameRange(derived,new MouseMotionRange(-48,48,-18,5)),"a file without a mouse range derives it from the motion strength");

            // The main window itself, as started from a settings file (never shown).
            string windowFile=Path.Combine(folder,"window-settings.txt");
            var user=new CompanionSettings { Size=560,Left=100,Top=100,MotionStrength=1.4,
                MouseRange=new MouseMotionRange(-17,118,-70,8),InputDeviceId="mic",InputDeviceName="USB" };
            new SettingsStore(windowFile).Save(user);
            var window=new CompanionWindow(false,false,windowFile);
            Assert(SameSettings(user,new SettingsStore(windowFile).Load(),false),"starting the companion keeps the saved settings");
            MenuChecks(window);
            AudioPolicyChecks(window);
            string userFile=File.ReadAllText(windowFile);
            using(new FileStream(windowFile,FileMode.Open,FileAccess.Read,FileShare.Write|FileShare.Delete))
                window=new CompanionWindow(false,false,windowFile);
            window.SaveSettings(); // e.g. the companion is dragged after the file became readable again
            Assert(File.ReadAllText(windowFile)==userFile,"a start whose settings read failed never overwrites them");
            List<string> labels=MenuLabels(window.MenuEntries());
            Assert(labels.Contains("設定を読み込み直す") && labels.Contains("現在の設定で上書き保存"),
                "after a failed read the menus offer to read the file again or to overwrite it");
            Assert(window.ReloadSettings() && SameSettings(user,window.CurrentSettings(),false),
                "reading the file again applies the saved settings");
            Assert(!MenuLabels(window.MenuEntries()).Contains("設定を読み込み直す"),"the settings notice goes away once the file is read");
            using(new FileStream(windowFile,FileMode.Open,FileAccess.Read,FileShare.Write|FileShare.Delete))
                window=new CompanionWindow(false,false,windowFile);
            window.OverwriteSettings();
            Assert(new SettingsStore(windowFile).Load().Size==440,"choosing to overwrite saves the settings now in use");

            new SettingsStore(windowFile).Save(user);
            window=new CompanionWindow(false,false,windowFile);
            var preview=new MouseMotionRange(-5,5,-10,0);
            window.BeginMouseRangeEdit();
            window.PreviewMouseRange(preview);
            window.SaveSettings(); // another action saves while the range dialog is open
            Assert(SameRange(new SettingsStore(windowFile).Load().MouseRange,user.MouseRange),"a range still being previewed is never saved");
            window.EndMouseRangeEdit(null);
            Assert(SameRange(new SettingsStore(windowFile).Load().MouseRange,user.MouseRange) &&
                SameRange(window.CurrentSettings().MouseRange,user.MouseRange),"cancelling the range dialog restores the previous range");
            window.BeginMouseRangeEdit();
            window.PreviewMouseRange(preview);
            window.EndMouseRangeEdit(preview);
            Assert(SameRange(new SettingsStore(windowFile).Load().MouseRange,preview),"confirming the range dialog saves the new range");
        }
        finally
        {
            try { Directory.Delete(folder,true); } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
    }

    private static bool SameSettings(CompanionSettings a,CompanionSettings b,bool position)
    {
        return b!=null && a.Size==b.Size && a.ClickThrough==b.ClickThrough && a.ReducedMotion==b.ReducedMotion &&
            a.MotionStrength==b.MotionStrength && SameRange(a.MouseRange,b.MouseRange) &&
            a.InputDeviceId==b.InputDeviceId && a.InputDeviceName==b.InputDeviceName &&
            a.OutputDeviceId==b.OutputDeviceId && a.OutputDeviceName==b.OutputDeviceName &&
            a.LipSyncGain==b.LipSyncGain && a.LipSyncFloor==b.LipSyncFloor &&
            (!position || (a.Left==b.Left && a.Top==b.Top));
    }

    private static List<string> MenuLabels(List<CompanionWindow.MenuEntry> entries)
    {
        var labels=new List<string>();
        foreach(CompanionWindow.MenuEntry entry in entries)
        {
            if(entry.Separator)continue;
            labels.Add(entry.Label);
            if(entry.Children!=null)labels.AddRange(MenuLabels(entry.Children));
        }
        return labels;
    }

    private static CompanionWindow.MenuEntry FindEntry(List<CompanionWindow.MenuEntry> entries,string label)
    {
        foreach(CompanionWindow.MenuEntry entry in entries)
        {
            if(entry.Label==label)return entry;
            if(entry.Children!=null)
            {
                CompanionWindow.MenuEntry found=FindEntry(entry.Children,label);
                if(found!=null)return found;
            }
        }
        return null;
    }

    // Both menus are built from this one list, so the tray can change every setting too.
    private static void MenuChecks(CompanionWindow window)
    {
        List<CompanionWindow.MenuEntry> entries=window.MenuEntries();
        List<string> labels=MenuLabels(entries);
        string[] required={ "オフ","マイク・仮想入力","PC 再生","オーディオデバイス設定…","口パクテスト（音声不要）",
            "控えめ","大きく（標準）","とても大きく","動きを抑える","マウスの移動範囲…","動きの細かい調整…","デモ",
            "小 320","中 440","大 560","画面内に戻す","一時的に隠す","終了" };
        bool all=true;
        foreach(string label in required) if(!labels.Contains(label))all=false;
        Assert(all,"the shared menu offers every setting, in the tray as well as on the character");
        Assert(FindEntry(entries,"大 560").Checked==true && FindEntry(entries,"中 440").Checked==false &&
            FindEntry(entries,"とても大きく").Checked==true && FindEntry(entries,"オフ").Checked==true,
            "the menus show the current choices");
        CompanionWindow.MenuEntry through=FindEntry(entries,"クリック透過（Ctrl+Alt+C は使用不可）");
        Assert(through!=null && through.ToolTip!=null,"a shortcut that could not be registered is shown as unavailable, with what to do instead");
        Assert(!labels.Contains("設定を読み込み直す") && !labels.Contains("もう一度保存する"),"no settings notice while settings load and save");
    }

    // Only the audio that moves the mouth is acquired. The saved input device ("mic") does not
    // exist, so choosing the microphone here opens nothing.
    private static void AudioPolicyChecks(CompanionWindow window)
    {
        Assert(CompanionWindow.AudioToAcquire(AudioSource.Playback,false,false,false)==AudioSource.Playback &&
            CompanionWindow.AudioToAcquire(AudioSource.Playback,true,false,false)==AudioSource.Off &&
            CompanionWindow.AudioToAcquire(AudioSource.Playback,false,true,false)==AudioSource.Off &&
            CompanionWindow.AudioToAcquire(AudioSource.Playback,false,false,true)==AudioSource.Off,
            "no audio is acquired while hidden, during the mouth test or during the demo");
        window.SelectAudio(AudioSource.Microphone);
        Assert(window.AcquiredAudio==AudioSource.Microphone,"choosing the microphone acquires it");
        window.SetDemo(true);
        Assert(window.AcquiredAudio==AudioSource.Off,"the demo stops the audio it does not use");
        window.SetDemo(false);
        Assert(window.AcquiredAudio==AudioSource.Microphone,"ending the demo resumes the chosen audio");
        window.SetLipSyncTest(true);
        Assert(window.AcquiredAudio==AudioSource.Off,"the mouth test stops the audio");
        window.SetDemo(true);
        Assert(window.AcquiredAudio==AudioSource.Off,"going from the mouth test to the demo does not start the microphone");
        window.SetDemo(false);
        Assert(window.AcquiredAudio==AudioSource.Microphone,"the chosen audio comes back after both");
        window.SelectAudio(AudioSource.Off);
        Assert(window.AcquiredAudio==AudioSource.Off,"choosing off releases the audio");
    }

    private static bool SameRange(MouseMotionRange a,MouseMotionRange b)
    {
        return Math.Abs(a.Left-b.Left)<1e-9 && Math.Abs(a.Right-b.Right)<1e-9 &&
            Math.Abs(a.Top-b.Top)<1e-9 && Math.Abs(a.Bottom-b.Bottom)<1e-9;
    }

    // What the keyboard hand reached while typing: extremes of its offset from rest.
    private sealed class Stroke
    {
        public double Highest, Lowest, Left, Right, ClosestLandings=double.MaxValue;
        public Point Landed;
    }

    // Presses at frame 0, then every interval frames (0 = only once).
    private static Stroke Type(CharacterView view,double start,int frames,int interval,bool accent,bool reduced)
    {
        var stroke=new Stroke();
        Point previous=view.KeyboardHandTarget;
        for(int i=0;i<frames;i++)
        {
            bool press=interval>0 ? i%interval==0 : i==0;
            view.Tick(start+i/60.0,1.0/60,press,false,0,0,0,false,reduced,accent);
            if(press)
            {
                Point target=view.KeyboardHandTarget;
                stroke.ClosestLandings=Math.Min(stroke.ClosestLandings,(target-previous).Length);
                previous=target;
            }
            Point hand=view.KeyboardHandOffset;
            stroke.Highest=Math.Max(stroke.Highest,-hand.Y);
            stroke.Lowest=Math.Max(stroke.Lowest,hand.Y);
            stroke.Left=Math.Min(stroke.Left,hand.X);
            stroke.Right=Math.Max(stroke.Right,hand.X);
            if(i==20)stroke.Landed=hand;
        }
        return stroke;
    }

    private static CharacterView NewView(string assets,double strength)
    {
        var view=new CharacterView(assets);
        view.Scenes=false;
        view.Measure(new Size(1469,1071));
        view.Arrange(new Rect(0,0,1469,1071));
        view.MotionStrength=strength;
        view.Tick(0,1.0/60,false,false,0,0,0,false,false);
        return view;
    }

    private static void KeyboardHandChecks(string assets)
    {
        using(var signals=new NativeSignals())
        {
            signals.PollKeyboard(k=>false,0);
            signals.PollKeyboard(k=>k==13,.1);
            Assert(signals.KeyPressed && signals.KeyEmphatic,"Enter reports an emphatic press");
            signals.PollKeyboard(k=>k==13,.2);
            signals.PollKeyboard(k=>k==13,1);
            Assert(signals.KeyPressed && signals.KeyRepeated && !signals.KeyEmphatic,"held Enter is emphatic only when it goes down");
            signals.PollKeyboard(k=>k==13||k==81,1.1);
            Assert(signals.KeyPressed && !signals.KeyEmphatic,"an ordinary key is not emphatic");
        }

        // From rest the hand winds up, comes down onto the keys, and ends where it started.
        var view=NewView(assets,1);
        Stroke single=Type(view,1,180,0,false,false);
        Assert(single.Highest>50,"a single press swings the hand high above the keys");
        Assert(single.Lowest>1 && single.Lowest<50,"the hand presses into the keys without sinking through them");
        Assert(single.Landed.Y>-single.Highest*.6,"the stroke has landed a third of a second after the press");
        Assert(single.ClosestLandings>50,"the first stroke lands away from the resting spot");
        Assert(view.KeyboardHandAtRest,"the hand returns to the resting artwork after one press");

        // Same random stroke in each view, so only the option under test differs.
        view=NewView(assets,.4);
        Stroke gentle=Type(view,1,180,0,false,false);
        view=NewView(assets,.4);
        Stroke accent=Type(view,1,180,0,true,false);
        Assert(accent.Highest>gentle.Highest*1.25,"an emphatic key gets a bigger swing");
        Assert(gentle.Highest<single.Highest*.6,"the gentle setting keeps strokes small");
        view=NewView(assets,1);
        Stroke reduced=Type(view,1,180,0,false,true);
        Assert(reduced.Highest>3 && reduced.Highest<single.Highest*.5,"reduced motion keeps a small stroke");

        // Rapid typing: every stroke goes somewhere new and the hand stays up between keys.
        view=NewView(assets,1);
        Stroke burst=Type(view,1,240,6,false,false);
        Assert(burst.ClosestLandings>50,"successive strokes never land on the same spot");
        Assert(burst.Right-burst.Left>110,"a burst ranges widely across the keyboard");
        Assert(!view.KeyboardHandAtRest,"the hand stays in play during a burst");
        for(int i=0;i<180;i++) view.Tick(6+i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(view.KeyboardHandAtRest,"the hand glides home after a burst");

        // A held key: quick short strokes on the spot where the first press landed.
        view=NewView(assets,1);
        Type(view,1,24,0,false,false);
        Point spot=view.KeyboardHandTarget;
        double wander=0,tallest=0;
        int landings=0;
        bool up=false;
        for(int i=0;i<120;i++)
        {
            view.Tick(1.4+i/60.0,1.0/60,i%5==0,false,0,0,0,false,false,false,true);
            wander=Math.Max(wander,(view.KeyboardHandTarget-spot).Length);
            double height=-view.KeyboardHandOffset.Y+view.KeyboardHandTarget.Y;
            tallest=Math.Max(tallest,height);
            if(height>12)up=true;
            else if(up && height<4){ up=false; landings++; }
        }
        Assert(wander<45,"a held key keeps hammering near the same spot");
        Assert(landings>=12,"a held key lands a rapid run of strokes");
        Assert(tallest<single.Highest,"held-key strokes are shorter than a deliberate press");
        for(int i=0;i<180;i++) view.Tick(3.4+i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(view.KeyboardHandAtRest,"the hand settles after the held key is released");

        // Hammering at full strength stays inside what the sleeve artwork can reach.
        view=NewView(assets,1.4);
        bool bounded=true;
        for(int i=0;i<1200;i++)
        {
            view.Tick(1+i/60.0,1.0/60,i%5==0,false,0,0,0,false,false,i%35==0);
            Point hand=view.KeyboardHandOffset;
            if(hand.X<-85 || hand.X>105 || hand.Y<-145 || hand.Y>70)bounded=false;
        }
        Assert(bounded,"hammering keeps the hand within the sleeve's reach");
        for(int i=0;i<180;i++) view.Tick(21+i/60.0,1.0/60,false,false,0,0,0,false,false);
        Assert(view.KeyboardHandAtRest,"the hand comes to rest after hammering stops");
        LongHoldChecks(assets);
    }

    // A key held for minutes, with the repeat timing of NativeSignals: every stroke stays a
    // few pixels from where the key first landed, on keys a press could reach, at every
    // motion setting; and the hand still goes home when the key is let go.
    private static void LongHoldChecks(string assets)
    {
        double[] strengths={ 1.4,1,.4,1 };
        bool[] reducedModes={ false,false,false,true };
        string[] labels={ "very large","standard","gentle","reduced motion" };
        for(int c=0;c<strengths.Length;c++)
        {
            var view=NewView(assets,strengths[c]);
            Held held=HoldKey(view,300,reducedModes[c]);
            double spread=Math.Min(1,(reducedModes[c] ? .32 : 1)*strengths[c]);
            // Strokes may land up to 7 px across and 4 px along from the spot, scaled with the motion.
            Assert(held.Wander<=Math.Sqrt(7*7+4*4)*spread+1e-9,labels[c]+": a key held for five minutes keeps hammering where it landed");
            Assert(held.Outside<1e-9,labels[c]+": held-key strokes stay on keys a deliberate press can reach");
            Assert(held.Rested,labels[c]+": the hand goes home after a five-minute hold");
            if(strengths[c]==1.4 && !reducedModes[c])
                Assert(held.WithinSleeve,"a five-minute hold keeps the hand within the sleeve's reach");
        }
        // Random numbers stuck at either end of their range push every choice to an edge.
        double fullOutside=0,reducedOutside=0;
        bool fullWithinSleeve=true;
        for(int seed=1;seed<=6;seed++)
        {
            var view=NewView(assets,1.4);
            SetStrokeRandom(view,new EdgeRandom(seed));
            Held held=HoldKey(view,60,false);
            fullOutside=Math.Max(fullOutside,held.Outside);
            fullWithinSleeve&=held.WithinSleeve;
            view=NewView(assets,1);
            SetStrokeRandom(view,new EdgeRandom(seed));
            reducedOutside=Math.Max(reducedOutside,HoldKey(view,60,true).Outside);
        }
        Assert(fullOutside<1e-9 && fullWithinSleeve,"edge-biased held strokes stay on the keys and within the sleeve's reach");
        Assert(reducedOutside<1e-9,"edge-biased held strokes stay in the smaller reduced-motion area");
    }

    private sealed class Held
    {
        public double Wander, Outside;
        public bool WithinSleeve=true, Rested;
    }

    private static Held HoldKey(CharacterView view,double seconds,bool reduced)
    {
        var held=new Held();
        double amount=(reduced ? .32 : 1)*Math.Max(.2,Math.Min(1.6,view.MotionStrength));
        Rect area=PressArea(amount);
        double now=0,frame=1.0/60;
        Point spot=view.KeyboardHandTarget;
        using(var signals=new NativeSignals())
        {
            signals.PollKeyboard(k=>false,now);
            for(int i=0;i<seconds*60;i++)
            {
                signals.PollKeyboard(k=>k==65,now+=frame);
                view.Tick(1+now,frame,signals.KeyPressed,false,0,0,0,false,reduced,signals.KeyEmphatic,signals.KeyRepeated);
                Point target=view.KeyboardHandTarget;
                if(signals.KeyPressed && !signals.KeyRepeated)spot=target;
                held.Wander=Math.Max(held.Wander,(target-spot).Length);
                held.Outside=Math.Max(held.Outside,Math.Max(Math.Max(area.Left-target.X,target.X-area.Right),
                    Math.Max(area.Top-target.Y,target.Y-area.Bottom)));
                Point hand=view.KeyboardHandOffset;
                if(hand.X<-85 || hand.X>105 || hand.Y<-145 || hand.Y>70)held.WithinSleeve=false;
            }
            for(int i=0;i<180;i++)
            {
                signals.PollKeyboard(k=>false,now+=frame);
                view.Tick(1+now,frame,signals.KeyPressed,false,0,0,0,false,reduced,signals.KeyEmphatic,signals.KeyRepeated);
            }
        }
        held.Rested=view.KeyboardHandAtRest;
        return held;
    }

    // Every spot a deliberate press can pick on the keyboard drawing for this motion amount:
    // 115 px along the rows and 75 px across them, mapped onto the artboard and capped where
    // the sleeve drawing would be squashed (see CharacterView.BeginStroke).
    private static Rect PressArea(double amount)
    {
        double s=Math.Max(0,Math.Min(1,amount));
        return new Rect(new Point(Math.Max(-72,-84.945*s),-16.785*s),new Point(Math.Min(96,96.42*s),37.305*s));
    }

    private static void SetStrokeRandom(CharacterView view,Random random)
    {
        typeof(CharacterView).GetField("strokeRandom",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,random);
    }

    // Only ever returns either end of the [0, 1) range, picked at random.
    private sealed class EdgeRandom : Random
    {
        private readonly Random pick;
        public EdgeRandom(int seed) { pick=new Random(seed); }
        public override double NextDouble() { return pick.Next(2)==0 ? 0 : .9999999; }
    }

    private static Point SettledMouse(CharacterView view, double x, double y)
    {
        for(int i=0;i<120;i++) view.Tick(10+i/60.0,1.0/60,false,false,x,y,0,false,false);
        return view.MouseOffset;
    }

    // Brightness (0..255) of one rendered artboard pixel, or -1 where nothing is drawn.
    private static double Luminance(CharacterView view,double x,double y)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1469,1071,96,96,PixelFormats.Pbgra32);
        bitmap.Render(view);
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)x,(int)y,1,1),pixel,4,0);
        return pixel[3]<128 ? -1 : (pixel[0]+pixel[1]+pixel[2])/3.0;
    }

    private static byte[] KeyboardPixels(CharacterView view)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1469,1071,96,96,PixelFormats.Pbgra32);
        bitmap.Render(view);
        var area = new Int32Rect(860,620,220,170);
        byte[] pixels = new byte[area.Width*area.Height*4];
        bitmap.CopyPixels(area,pixels,area.Width*4,0);
        return pixels;
    }
    private static bool Equal(byte[] a,byte[] b)
    {
        // WPF premultiplied-alpha composition can round a channel by one between renders.
        for(int i=0;i<a.Length;i++) if(Math.Abs(a[i]-b[i])>1)return false;
        return true;
    }
}
