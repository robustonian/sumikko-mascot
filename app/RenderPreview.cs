using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SumikkoCompanion;

// Headless rendering uses exactly the application's renderer, including its springs.
internal static class RenderPreview
{
    [STAThread]
    private static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string output = Path.Combine(root, "preview");
        Directory.CreateDirectory(output);
        bool animation = args.Length > 1 && args[1] == "--animation";
        if(animation) Directory.CreateDirectory(Path.Combine(output,"animation"));
        var view = new CharacterView(Path.Combine(root, "assets"));
        view.Measure(new Size(1469,1071));
        view.Arrange(new Rect(0,0,1469,1071));
        view.UpdateLayout();
        for(int frame=0; frame<=(animation ? 358 : 240); frame++)
        {
            view.Tick(frame/60.0,1.0/60,false,false,0,0,0,frame>0,false);
            view.UpdateLayout();
            if(animation ? frame%2==0 : (frame==0 || frame==40 || frame==165 || frame==240))
            {
                var bitmap = new RenderTargetBitmap(734,536,48,48,PixelFormats.Pbgra32);
                bitmap.Render(view);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                string file = animation ? Path.Combine(output,"animation","frame-"+(frame/2).ToString("D4")+".png") : Path.Combine(output,"frame-"+frame.ToString("D3")+".png");
                using(var stream=File.Create(file)) encoder.Save(stream);
                if(!animation) Console.WriteLine(file);
            }
        }
    }
}
