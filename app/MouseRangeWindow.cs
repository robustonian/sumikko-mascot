using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SumikkoCompanion
{
    public sealed class MouseRangeWindow : Window
    {
        private static readonly string[] Directions = { "上", "下", "左", "右" };
        private static readonly string[] Arrows = { "↑", "↓", "←", "→" };
        private readonly MouseRangeCalibration calibration;
        private readonly Action<MouseMotionRange> previewChanged;
        private readonly CharacterView character;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly TextBlock heading, instructions, status, distanceLabel;
        private readonly TextBlock[] stepLabels = new TextBlock[4];
        private readonly Slider distance;
        private readonly Button back, next;
        // The range when the dialog opened, for "back to how it was" on one direction.
        private readonly MouseMotionRange opened;
        private readonly Grid root;
        private readonly ScrollViewer scroll;
        private readonly Ellipse handMarker;
        private readonly Rectangle rangeOutline;
        private bool updating;
        private double lastFrame;

        public MouseMotionRange SelectedRange { get { return calibration.Range; } }
        public Point PreviewPointer { get { return calibration.PreviewPointer; } }

        public MouseRangeWindow(string assetsDirectory, MouseMotionRange initial,
            Action<MouseMotionRange> previewChanged)
        {
            calibration = new MouseRangeCalibration(initial);
            opened = initial;
            this.previewChanged = previewChanged;
            character = new CharacterView(assetsDirectory);
            character.IdleMouseMotion = false;
            character.Scenes = false;
            Title = "マウスの移動範囲 — 4方向の調整";
            Width = 710; Height = 840; MinWidth = 560; MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brush(250,247,240);
            FontFamily = new FontFamily("Yu Gothic UI");

            root = new Grid { Margin = new Thickness(26,20,26,20) };
            for(int i=0;i<6;i++) root.RowDefinitions.Add(new RowDefinition {
                Height = i==3 ? new GridLength(1,GridUnitType.Star) : GridLength.Auto });
            // On a short screen the controls keep their size and the whole page scrolls,
            // instead of the preview shrinking to nothing.
            scroll = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            scroll.SizeChanged += delegate { FitContent(); };
            Content = scroll;
            var steps = new Grid { Margin=new Thickness(0,0,0,16) };
            for(int i=0;i<4;i++)
            {
                steps.ColumnDefinitions.Add(new ColumnDefinition());
                stepLabels[i]=new TextBlock { Text=(i+1)+"  "+Arrows[i]+" "+Directions[i],
                    Padding=new Thickness(12,8,12,8), FontSize=14, TextAlignment=TextAlignment.Center,
                    Margin=new Thickness(0,0,i==3?0:7,0), Cursor=Cursors.Hand,
                    ToolTip=Directions[i]+"の調整へ移動" };
                // A direction can be picked directly, e.g. to fine-tune just one of them later.
                int direction=i;
                stepLabels[i].MouseLeftButtonUp+=delegate
                {
                    calibration.Stop();
                    calibration.SetStep(direction);
                    UpdateStep();
                };
                Grid.SetColumn(stepLabels[i],i); steps.Children.Add(stepLabels[i]);
            }
            root.Children.Add(steps);
            heading=new TextBlock { FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=Brush(53,52,47) };
            Grid.SetRow(heading,1); root.Children.Add(heading);
            instructions=new TextBlock { FontSize=13,TextWrapping=TextWrapping.Wrap,
                Foreground=Brush(95,89,78),Margin=new Thickness(0,8,0,14) };
            Grid.SetRow(instructions,2); root.Children.Add(instructions);

            var preview = new Border { Background=Brush(241,235,222),BorderBrush=Brush(224,216,202),
                BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(10),
                Focusable=true,Margin=new Thickness(0,0,0,12),Cursor=Cursors.Cross,MinHeight=PreviewMinHeight };
            Grid.SetRow(preview,3); root.Children.Add(preview);
            var art=new Grid { Width=1469,Height=1071 };
            art.Children.Add(character);
            var overlay=new Canvas { Width=1469,Height=1071,IsHitTestVisible=false };
            rangeOutline=new Rectangle { Stroke=Brush(198,117,89),StrokeThickness=4,
                StrokeDashArray=new DoubleCollection { 8,8 },Fill=new SolidColorBrush(Color.FromArgb(15,198,117,89)) };
            handMarker=new Ellipse { Width=22,Height=22,Fill=Brush(198,117,89),Stroke=Brush(255,255,255),StrokeThickness=4 };
            overlay.Children.Add(rangeOutline); overlay.Children.Add(handMarker); art.Children.Add(overlay);
            preview.Child=new Viewbox { Child=art,Stretch=Stretch.Uniform };
            AutomationProperties.SetName(preview,"絵をクリックして、この方向の動きを試す");
            preview.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e)
            {
                Point cursor;
                if(TryCursor(out cursor)) calibration.Begin(cursor);
                preview.Focus(); UpdateStatus(); e.Handled=true;
            };
            // Returning to the controls is not part of the deliberate calibration gesture.
            preview.MouseLeave+=delegate { calibration.Stop(); UpdateStatus(); };

            var controls=new StackPanel(); Grid.SetRow(controls,4); root.Children.Add(controls);
            status=new TextBlock { FontSize=13,Foreground=Brush(118,71,54),
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8) };
            controls.Children.Add(status);
            var hold=MakeButton("端の位置で止めて調整",false);
            hold.HorizontalAlignment=HorizontalAlignment.Left;
            hold.Click+=delegate { calibration.HoldEndpoint(); UpdateStatus(); };
            controls.Children.Add(hold);
            controls.Children.Add(new TextBlock { Text="この方向に、どこまで動かす？",FontSize=14,
                Foreground=Brush(53,52,47),Margin=new Thickness(0,12,0,2) });
            var sliderRow=new Grid();
            sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            sliderRow.ColumnDefinitions.Add(new ColumnDefinition());
            sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            distance=new Slider { Minimum=0,TickFrequency=1,IsSnapToTickEnabled=true,SmallChange=1,LargeChange=10,Margin=new Thickness(5,8,5,8) };
            AutomationProperties.SetName(distance,"この方向の移動幅");
            // One step per click (held down, it keeps stepping); the slider also takes the arrow keys.
            var smaller=Nudge("－ 小さく",-1);
            sliderRow.Children.Add(smaller);
            Grid.SetColumn(distance,1); sliderRow.Children.Add(distance);
            var larger=Nudge("大きく ＋",1);
            Grid.SetColumn(larger,2); sliderRow.Children.Add(larger); controls.Children.Add(sliderRow);
            distanceLabel=new TextBlock { FontSize=12,Foreground=Brush(103,100,90),Margin=new Thickness(0,0,0,6),TextWrapping=TextWrapping.Wrap };
            controls.Children.Add(distanceLabel);
            var restoreRow=new WrapPanel { Margin=new Thickness(0,0,0,10) };
            var toDefault=MakeButton("この方向を初期値に戻す",false);
            toDefault.Click+=delegate { Restore(character.Profile.MouseRange); };
            restoreRow.Children.Add(toDefault);
            var toOpened=MakeButton("開いた時の値に戻す",false);
            toOpened.Click+=delegate { Restore(opened); };
            restoreRow.Children.Add(toOpened);
            controls.Children.Add(restoreRow);
            distance.ValueChanged+=delegate
            {
                if(updating)return;
                calibration.SetDistance(distance.Value);
                if(previewChanged!=null)previewChanged(SelectedRange);
                UpdateOverlay(); UpdateStatus(); UpdateDistanceLabel();
            };

            var actions=new DockPanel { LastChildFill=false,Margin=new Thickness(0,8,0,0) };
            Grid.SetRow(actions,5); root.Children.Add(actions);
            var cancel=MakeButton("キャンセル",false); cancel.IsCancel=true;
            cancel.Click+=delegate { DialogResult=false; };
            DockPanel.SetDock(cancel,Dock.Left); actions.Children.Add(cancel);
            next=MakeButton("この方向で決定 →",true); next.IsDefault=true;
            next.Click+=delegate
            {
                calibration.Stop();
                if(calibration.StepIndex==3) { DialogResult=true; return; }
                calibration.SetStep(calibration.StepIndex+1); UpdateStep();
            };
            DockPanel.SetDock(next,Dock.Right); actions.Children.Add(next);
            back=MakeButton("← 戻る",false);
            back.Click+=delegate { calibration.SetStep(calibration.StepIndex-1); UpdateStep(); };
            DockPanel.SetDock(back,Dock.Right); actions.Children.Add(back);

            PreviewKeyDown+=delegate(object sender,KeyEventArgs e)
            {
                if(e.Key==Key.Space && calibration.IsTracking)
                { calibration.Stop(); UpdateStatus(); e.Handled=true; }
            };
            Deactivated+=delegate { calibration.Stop(); UpdateStatus(); };
            Loaded+=delegate
            {
                FitToScreen(); clock.Start(); CompositionTarget.Rendering+=OnRendering;
                Dispatcher.BeginInvoke(new Action(FitContent),System.Windows.Threading.DispatcherPriority.Loaded);
            };
            Closed+=delegate
            {
                calibration.Stop(); CompositionTarget.Rendering-=OnRendering; clock.Stop();
            };
            UpdateStep();
        }

        private void UpdateStep()
        {
            int step=calibration.StepIndex;
            heading.Text=Arrows[step]+" マウスを"+Directions[step]+"へ動かすとき";
            instructions.Text="① 絵の中をクリックしてから、マウスを"+Directions[step]+"へ動かしてください。絵の外へ出ると停止します。\n"+
                "② 到達位置を下のスライダーで合わせ、次の方向へ進みます。";
            for(int i=0;i<4;i++)
            {
                stepLabels[i].Background=i==step?Brush(198,117,89):Brush(239,233,221);
                stepLabels[i].Foreground=i==step?Brush(255,255,255):Brush(103,100,90);
            }
            updating=true;
            distance.Maximum=calibration.MaximumDistance;
            distance.Value=calibration.Distance;
            updating=false;
            back.IsEnabled=step>0;
            next.Content=step==3?"保存して完了":"この方向で決定 →";
            UpdateOverlay(); UpdateStatus(); UpdateDistanceLabel();
        }

        private const double PreviewMinHeight=200;

        private void FitContent()
        {
            double rows=0;
            for(int i=0;i<root.RowDefinitions.Count;i++) if(i!=3) rows+=root.RowDefinitions[i].ActualHeight;
            root.MinHeight=rows+PreviewMinHeight+12;
            root.Height=Math.Max(0,scroll.ActualHeight-root.Margin.Top-root.Margin.Bottom);
        }

        private RepeatButton Nudge(string text,int step)
        {
            var button=new RepeatButton { Content=text,MinWidth=76,Height=32,Padding=new Thickness(8,0,8,0),
                Delay=400,Interval=60,Background=Brush(255,255,255),Foreground=Brush(63,62,57),
                BorderBrush=Brush(210,199,184),BorderThickness=new Thickness(1),VerticalAlignment=VerticalAlignment.Center };
            AutomationProperties.SetName(button,step<0?"移動幅を1小さく":"移動幅を1大きく");
            button.Click+=delegate { distance.Value=Math.Max(distance.Minimum,Math.Min(distance.Maximum,Math.Round(distance.Value)+step)); };
            return button;
        }

        private void Restore(MouseMotionRange from)
        {
            calibration.Stop();
            calibration.RestoreEndpoint(from);
            updating=true;
            distance.Maximum=calibration.MaximumDistance;
            distance.Value=calibration.Distance;
            updating=false;
            if(previewChanged!=null)previewChanged(SelectedRange);
            UpdateOverlay(); UpdateStatus(); UpdateDistanceLabel();
        }

        private void UpdateDistanceLabel()
        {
            distanceLabel.Text="移動幅 "+calibration.Distance.ToString("0")+"（初期値 "+calibration.DistanceTo(character.Profile.MouseRange).ToString("0")+
                "・開いた時 "+calibration.DistanceTo(opened).ToString("0")+"）。調整中は手を端に固定します。絵をクリックすれば、また自分のマウスで試せます。";
        }

        private void UpdateStatus()
        {
            if(calibration.IsTracking)
                status.Text="マウスを"+Directions[calibration.StepIndex]+"へ動かしてください。Spaceで途中でも停止できます。";
            else if(calibration.Progress>=.999)
                status.Text="この方向の端で停止中。スライダーで到達位置を合わせられます。";
            else if(calibration.Progress>0)
                status.Text="試した位置で停止中。絵をクリックすると、もう一度試せます。";
            else
                status.Text="待機中 — 自動では動きません。絵をクリックして始めてください。";
        }

        private void UpdateOverlay()
        {
            rangeOutline.Width=Math.Max(1,SelectedRange.Right-SelectedRange.Left);
            rangeOutline.Height=Math.Max(1,SelectedRange.Bottom-SelectedRange.Top);
            Canvas.SetLeft(rangeOutline,540+SelectedRange.Left);
            Canvas.SetTop(rangeOutline,990+SelectedRange.Top);
        }

        private void OnRendering(object sender,EventArgs e)
        {
            double elapsed=clock.Elapsed.TotalSeconds;
            double dt=Math.Max(0,Math.Min(.05,elapsed-lastFrame)); lastFrame=elapsed;
            if(calibration.IsTracking)
            {
                Point cursor;
                if(TryCursor(out cursor))calibration.Observe(cursor);
            }
            Point pointer=PreviewPointer;
            character.MouseRange=SelectedRange;
            character.Tick(elapsed,dt,false,false,pointer.X,pointer.Y,0,false,false);
            Point offset=character.MouseOffset;
            Canvas.SetLeft(handMarker,540+offset.X-11); Canvas.SetTop(handMarker,990+offset.Y-11);
            UpdateStatus();
        }

        private bool TryCursor(out Point point)
        {
            NativePoint native;
            var dpi=VisualTreeHelper.GetDpi(this);
            bool ok=GetCursorPos(out native);
            point=new Point(native.X/dpi.DpiScaleX,native.Y/dpi.DpiScaleY);
            return ok;
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

        private static SolidColorBrush Brush(byte r,byte g,byte b)
        { return new SolidColorBrush(Color.FromRgb(r,g,b)); }
        private static Button MakeButton(string text,bool primary)
        {
            return new Button { Content=text,MinWidth=100,Height=36,Padding=new Thickness(14,0,14,0),
                Margin=new Thickness(0,0,8,0),Background=primary?Brush(198,117,89):Brush(255,255,255),
                Foreground=primary?Brush(255,255,255):Brush(63,62,57),BorderBrush=Brush(210,199,184),BorderThickness=new Thickness(1) };
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    }
}
