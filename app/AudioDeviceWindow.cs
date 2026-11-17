using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SumikkoCompanion
{
    // Choose -> try -> save: both devices can be chosen whatever the source (the choice is kept
    // for later even while lip sync is off), each can be tried with a live meter that shows
    // whether the mouth would move, and the sensitivity is set against that meter.
    public sealed class AudioDeviceWindow : Window
    {
        internal sealed class DeviceChoice
        {
            public string Id { get; private set; }
            public string Name { get; private set; }
            public string Label { get; private set; }
            public bool Available { get; private set; }

            internal DeviceChoice(string id, string name, string label, bool available)
            { Id=id; Name=name; Label=label; Available=available; }

            public override string ToString() { return Label; }
        }

        // The meter maps levels with a square root, like the mouth itself, so quiet sound shows.
        private const double MeterFullScale = .3;
        private readonly ComboBox source, input, output;
        private readonly Button testInput, testOutput;
        private readonly TextBlock status, meterStatus, gainValue, floorValue;
        private readonly Slider gain, floor;
        private readonly Border levelTrack, levelFill, mouthTrack, mouthFill;
        private readonly Rectangle floorMarker;
        private readonly DispatcherTimer meterTimer;
        private string inputError, outputError;
        private NativeSignals tester;
        private AudioSource testing = AudioSource.Off;
        private double shownLevel, shownMouth;

        public AudioSource SelectedSource { get { return (AudioSource)source.SelectedIndex; } }
        public string InputId { get { return Choice(input).Id; } }
        public string InputName { get { return Choice(input).Name; } }
        public string OutputId { get { return Choice(output).Id; } }
        public string OutputName { get { return Choice(output).Name; } }
        public double LipSyncGain { get { return Math.Pow(2,gain.Value); } }
        public double LipSyncFloor { get { return floor.Value/100; } }

        public AudioDeviceWindow(AudioSource selectedSource, string inputId, string inputName,
            string outputId, string outputName, double lipSyncGain, double lipSyncFloor)
        {
            Title="リップシンクのオーディオデバイス";
            Width=650; Height=760; MinWidth=470; MinHeight=440;
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=Brush(250,247,240);
            FontFamily=new FontFamily("Yu Gothic UI"); FontSize=14;

            var root=new DockPanel { Margin=new Thickness(24),LastChildFill=true };
            Content=root;
            var actions=new DockPanel { LastChildFill=false,Margin=new Thickness(0,18,0,0) };
            DockPanel.SetDock(actions,Dock.Bottom); root.Children.Add(actions);
            var save=Button("保存して適用",true); save.IsDefault=true;
            save.Click+=delegate { DialogResult=true; };
            DockPanel.SetDock(save,Dock.Right); actions.Children.Add(save);
            var cancel=Button("キャンセル",false); cancel.IsCancel=true;
            cancel.Click+=delegate { DialogResult=false; };
            DockPanel.SetDock(cancel,Dock.Right); actions.Children.Add(cancel);
            var refresh=Button("一覧を更新",false);
            refresh.Click+=delegate { StopTest(); RefreshDevices(InputId,InputName,OutputId,OutputName); };
            DockPanel.SetDock(refresh,Dock.Left); actions.Children.Add(refresh);

            var body=new StackPanel();
            root.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
            body.Children.Add(Text("どの音で口を動かす？",24,FontWeights.SemiBold));
            var explanation=Text("マイク・仮想オーディオ、またはPCの再生デバイスを選べます。「試す」で、選んだデバイスの音量と口の開き方を確かめられます。",13,FontWeights.Normal);
            explanation.Margin=new Thickness(0,8,0,18); body.Children.Add(explanation);

            body.Children.Add(Text("口パクに使う音声",14,FontWeights.SemiBold));
            source=Combo("口パクに使う音声");
            source.Items.Add("オフ");
            source.Items.Add("マイク・仮想入力");
            source.Items.Add("PC再生");
            source.SelectedIndex=(int)selectedSource;
            body.Children.Add(source);

            body.Children.Add(Text("入力デバイス（マイク・仮想オーディオ）",14,FontWeights.SemiBold));
            input=Combo("入力デバイス（マイク・仮想オーディオ）");
            testInput=Button("試す",false);
            body.Children.Add(DeviceRow(input,testInput));
            var inputHint=Text("仮想ケーブルの音を使う場合は、その録音側のデバイスを選びます。",12,FontWeights.Normal);
            inputHint.Margin=new Thickness(0,-6,0,16); body.Children.Add(inputHint);

            body.Children.Add(Text("再生デバイス（PCの音）",14,FontWeights.SemiBold));
            output=Combo("再生デバイス（PCの音）");
            testOutput=Button("試す",false);
            body.Children.Add(DeviceRow(output,testOutput));
            status=Text("",13,FontWeights.Normal);
            status.Foreground=Brush(118,71,54); body.Children.Add(status);

            var heading=Text("口の動き方",14,FontWeights.SemiBold);
            heading.Margin=new Thickness(0,20,0,6); body.Children.Add(heading);
            floorMarker=new Rectangle { Width=3,Fill=Brush(118,71,54),HorizontalAlignment=HorizontalAlignment.Left };
            levelFill=new Border { Background=Brush(156,170,128),HorizontalAlignment=HorizontalAlignment.Left,Width=0 };
            levelTrack=Meter(levelFill,floorMarker);
            body.Children.Add(MeterRow("音量",levelTrack));
            mouthFill=new Border { Background=Brush(198,117,89),HorizontalAlignment=HorizontalAlignment.Left,Width=0 };
            mouthTrack=Meter(mouthFill,null);
            body.Children.Add(MeterRow("口の開き",mouthTrack));
            meterStatus=Text("",12,FontWeights.Normal);
            meterStatus.Margin=new Thickness(0,2,0,10); body.Children.Add(meterStatus);

            gain=new Slider { Minimum=-2,Maximum=2,TickFrequency=.25,IsSnapToTickEnabled=true,SmallChange=.25,LargeChange=.5,
                Value=Math.Log(Clamp(lipSyncGain,CharacterView.MinLipSyncGain,CharacterView.MaxLipSyncGain),2) };
            gainValue=Text("",13,FontWeights.Normal);
            body.Children.Add(SliderRow("感度",gain,gainValue,"音量を何倍にして口を動かすか。反応しないときは上げます。"));
            floor=new Slider { Minimum=0,Maximum=CharacterView.MaxLipSyncFloor*100,TickFrequency=.1,IsSnapToTickEnabled=true,
                SmallChange=.1,LargeChange=1,Value=Clamp(lipSyncFloor,0,CharacterView.MaxLipSyncFloor)*100 };
            floorValue=Text("",13,FontWeights.Normal);
            body.Children.Add(SliderRow("小さい音を無視",floor,floorValue,"これより小さい音では口を開けません。ずっと口が開くときは上げます。"));
            var defaults=Button("口の動き方を初期値に戻す",false);
            defaults.HorizontalAlignment=HorizontalAlignment.Left; defaults.Margin=new Thickness(0,4,0,0);
            defaults.Click+=delegate
            {
                gain.Value=Math.Log(CharacterView.DefaultLipSyncGain,2);
                floor.Value=CharacterView.DefaultLipSyncFloor*100;
            };
            body.Children.Add(defaults);

            var footer=Text("デバイスの選択と口の動き方は保存されます。次回起動時の口パクはオフです。",12,FontWeights.Normal);
            footer.Margin=new Thickness(0,16,0,0); body.Children.Add(footer);

            meterTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(50) };
            meterTimer.Tick+=delegate { UpdateMeter(); };
            testInput.Click+=delegate { ToggleTest(AudioSource.Microphone); };
            testOutput.Click+=delegate { ToggleTest(AudioSource.Playback); };
            source.SelectionChanged+=delegate { UpdateStatus(); };
            input.SelectionChanged+=delegate { UpdateStatus(); if(testing==AudioSource.Microphone)StartTest(testing); };
            output.SelectionChanged+=delegate { UpdateStatus(); if(testing==AudioSource.Playback)StartTest(testing); };
            gain.ValueChanged+=delegate { UpdateSliderLabels(); };
            floor.ValueChanged+=delegate { UpdateSliderLabels(); };
            levelTrack.SizeChanged+=delegate { ShowMeter(shownLevel,shownMouth); };
            RefreshDevices(inputId,inputName,outputId,outputName);
            UpdateSliderLabels();
            StopTest();
            Loaded+=delegate { FitToScreen(); };
            Closed+=delegate { StopTest(); };
        }

        private void RefreshDevices(string inputId,string inputName,string outputId,string outputName)
        {
            Populate(input,AudioSource.Microphone,inputId,inputName,out inputError);
            Populate(output,AudioSource.Playback,outputId,outputName,out outputError);
            UpdateStatus();
        }

        private static void Populate(ComboBox combo, AudioSource source, string id, string name, out string error)
        {
            AudioDeviceInfo[] devices;
            error=null;
            try { devices=NativeSignals.GetAudioDevices(source); }
            catch(Exception) { devices=new AudioDeviceInfo[0]; error="デバイス一覧を取得できません。「一覧を更新」で再試行してください。"; }
            List<DeviceChoice> choices=BuildChoices(devices,id,name);
            combo.DisplayMemberPath="Label";
            combo.ItemsSource=choices;
            // Match the stable ID, never the list position or friendly name.
            foreach(DeviceChoice item in choices)
                if(string.Equals(item.Id,id??"",StringComparison.Ordinal)) { combo.SelectedItem=item; break; }
        }

        internal static List<DeviceChoice> BuildChoices(AudioDeviceInfo[] devices,string selectedId,string selectedName)
        {
            var result=new List<DeviceChoice>();
            result.Add(new DeviceChoice("","Windowsの既定","Windowsの既定",true));
            var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(AudioDeviceInfo item in devices)
            {
                int count; counts.TryGetValue(item.Name,out count); counts[item.Name]=count+1;
            }
            var positions=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            bool found=string.IsNullOrEmpty(selectedId);
            foreach(AudioDeviceInfo item in devices)
            {
                int position; positions.TryGetValue(item.Name,out position); positions[item.Name]=++position;
                // Namesakes also show the end of their endpoint ID, which does not change when the
                // list order does, so the same device can be found again.
                string label=item.Name+(counts[item.Name]>1?"（"+position+"・ID "+ShortId(item.Id)+"）":"");
                result.Add(new DeviceChoice(item.Id,item.Name,label,true));
                if(string.Equals(item.Id,selectedId,StringComparison.Ordinal))found=true;
            }
            if(!found)
            {
                string name=string.IsNullOrEmpty(selectedName)?"保存済みデバイス":selectedName;
                result.Add(new DeviceChoice(selectedId,name,"［未接続］"+name,false));
            }
            return result;
        }

        // The last four letters or digits of an endpoint ID (its GUID part), e.g. "89AB".
        internal static string ShortId(string id)
        {
            var letters=new StringBuilder();
            foreach(char c in id??"") if(char.IsLetterOrDigit(c))letters.Append(char.ToUpperInvariant(c));
            return letters.Length<=4 ? letters.ToString() : letters.ToString(letters.Length-4,4);
        }

        private static DeviceChoice Choice(ComboBox combo)
        { return combo.SelectedItem as DeviceChoice ?? new DeviceChoice("","Windowsの既定","Windowsの既定",true); }

        private void UpdateStatus()
        {
            if(status==null)return;
            string error=SelectedSource==AudioSource.Microphone?inputError:outputError;
            DeviceChoice device=Choice(SelectedSource==AudioSource.Microphone?input:output);
            if(SelectedSource==AudioSource.Off)status.Text="保存すると口パクを停止します。選んだデバイスは保存され、次に音声を選んだときに使います。";
            else if(!string.IsNullOrEmpty(error))status.Text=error;
            else if(!device.Available)status.Text="選択したデバイスは未接続です。再接続するか、別のデバイスを選んでください。";
            else status.Text="「保存して適用」で、"+(SelectedSource==AudioSource.Microphone?"入力":"再生")+"デバイスの音声に切り替わります。";
        }

        private void ToggleTest(AudioSource kind)
        {
            if(testing==kind) StopTest();
            else StartTest(kind);
        }

        // The test opens its own capture; what the companion itself uses changes only on save.
        private void StartTest(AudioSource kind)
        {
            StopTest();
            testing=kind;
            tester=new NativeSignals();
            tester.SetAudioSource(kind,kind==AudioSource.Microphone?InputId:OutputId);
            testInput.Content=kind==AudioSource.Microphone?"停止":"試す";
            testOutput.Content=kind==AudioSource.Playback?"停止":"試す";
            UpdateMeter();
            meterTimer.Start();
        }

        private void StopTest()
        {
            if(meterTimer!=null)meterTimer.Stop();
            if(tester!=null) { tester.Dispose(); tester=null; }
            testing=AudioSource.Off;
            shownLevel=shownMouth=0;
            if(testInput!=null)testInput.Content="試す";
            if(testOutput!=null)testOutput.Content="試す";
            if(meterStatus!=null)
            {
                meterStatus.Text="「試す」を押すと、そのデバイスの音量と口の開き方をここに表示します（保存はしません）。";
                ShowMeter(0,0);
            }
        }

        private void UpdateMeter()
        {
            if(tester==null)return;
            tester.PollAudioOnly();
            double level=tester.AudioLevel;
            // Peaks fall back gently so a short sound stays readable.
            shownLevel=Math.Max(level*LipSyncGain,shownLevel*.85);
            shownMouth=Math.Max(CharacterView.MouthOpening(level,LipSyncGain,LipSyncFloor),shownMouth*.85);
            ShowMeter(shownLevel,shownMouth);
            string state=tester.AudioStatus??"";
            string what=testing==AudioSource.Microphone?"入力デバイス":"再生デバイス";
            if(state.IndexOf("ready",StringComparison.OrdinalIgnoreCase)<0)
                meterStatus.Text=what+"から音を取得できません（"+state+"）。接続や選択を確認してください。";
            else if(shownMouth>0)
                meterStatus.Text=what+"を試しています。口が動いています。";
            else
                meterStatus.Text=what+"を試しています。"+(shownLevel>0?"音はありますが、口が開くほどではありません。":"音が届いていません。");
        }

        private void ShowMeter(double level,double mouth)
        {
            if(levelTrack==null)return;
            double width=Math.Max(0,levelTrack.ActualWidth-2);
            levelFill.Width=width*MeterPosition(level);
            floorMarker.Margin=new Thickness(Math.Max(0,width*MeterPosition(LipSyncFloor)-1),0,0,0);
            mouthFill.Width=Math.Max(0,mouthTrack.ActualWidth-2)*Clamp(mouth,0,1);
        }

        private static double MeterPosition(double level)
        {
            return Math.Min(1,Math.Sqrt(Math.Max(0,level)/MeterFullScale));
        }

        private void UpdateSliderLabels()
        {
            gainValue.Text="×"+LipSyncGain.ToString("0.0#");
            floorValue.Text=(LipSyncFloor*100).ToString("0.0")+"%";
            ShowMeter(shownLevel,shownMouth);
        }

        private void FitToScreen()
        {
            var dpi=VisualTreeHelper.GetDpi(this);
            var area=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            double w=area.Width/dpi.DpiScaleX-24,h=area.Height/dpi.DpiScaleY-24;
            MinWidth=Math.Min(MinWidth,w); MinHeight=Math.Min(MinHeight,h);
            Width=Math.Min(Width,w); Height=Math.Min(Height,h);
            Left=Math.Max(area.Left/dpi.DpiScaleX+12,Math.Min(Left,area.Right/dpi.DpiScaleX-Width-12));
            Top=Math.Max(area.Top/dpi.DpiScaleY+12,Math.Min(Top,area.Bottom/dpi.DpiScaleY-Height-12));
        }

        private static Grid DeviceRow(ComboBox combo,Button test)
        {
            var row=new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            row.Children.Add(combo);
            test.Margin=new Thickness(8,6,0,16); test.MinWidth=72;
            Grid.SetColumn(test,1); row.Children.Add(test);
            return row;
        }

        private static Border Meter(Border fill,Rectangle marker)
        {
            var inside=new Grid();
            inside.Children.Add(fill);
            if(marker!=null)inside.Children.Add(marker);
            return new Border { Height=16,Background=Brush(239,233,221),BorderBrush=Brush(210,199,184),
                BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Child=inside };
        }

        private static Grid MeterRow(string label,Border meter)
        {
            var row=new Grid { Margin=new Thickness(0,3,0,3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(84) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var text=Text(label,13,FontWeights.Normal); text.VerticalAlignment=VerticalAlignment.Center;
            row.Children.Add(text);
            Grid.SetColumn(meter,1); row.Children.Add(meter);
            AutomationProperties.SetName(meter,label);
            return row;
        }

        private static StackPanel SliderRow(string label,Slider slider,TextBlock value,string hint)
        {
            var panel=new StackPanel { Margin=new Thickness(0,6,0,0) };
            var row=new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(64) });
            var text=Text(label,13,FontWeights.Normal); text.VerticalAlignment=VerticalAlignment.Center;
            row.Children.Add(text);
            slider.Margin=new Thickness(4,4,8,4); AutomationProperties.SetName(slider,label);
            Grid.SetColumn(slider,1); row.Children.Add(slider);
            value.VerticalAlignment=VerticalAlignment.Center; value.TextAlignment=TextAlignment.Right;
            Grid.SetColumn(value,2); row.Children.Add(value);
            panel.Children.Add(row);
            var note=Text(hint,12,FontWeights.Normal); note.Foreground=Brush(103,100,90);
            panel.Children.Add(note);
            return panel;
        }

        private static double Clamp(double value,double min,double max)
        {
            if(double.IsNaN(value)||double.IsInfinity(value))return min;
            return Math.Max(min,Math.Min(max,value));
        }

        private static ComboBox Combo(string name)
        {
            var box=new ComboBox { MinHeight=36,Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,6,0,16),MaxDropDownHeight=280 };
            AutomationProperties.SetName(box,name); return box;
        }
        private static TextBlock Text(string text,double size,FontWeight weight)
        { return new TextBlock { Text=text,FontSize=size,FontWeight=weight,TextWrapping=TextWrapping.Wrap,Foreground=Brush(53,52,47) }; }
        private static SolidColorBrush Brush(byte r,byte g,byte b)
        { return new SolidColorBrush(Color.FromRgb(r,g,b)); }
        private static Button Button(string text,bool primary)
        {
            return new Button { Content=text,MinHeight=36,MinWidth=96,Padding=new Thickness(12,5,12,5),Margin=new Thickness(4,0,0,0),
                Background=primary?Brush(198,117,89):Brush(255,255,255),Foreground=primary?Brush(255,255,255):Brush(53,52,47),
                BorderBrush=Brush(210,199,184),BorderThickness=new Thickness(1) };
        }
    }
}
