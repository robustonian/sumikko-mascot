using System;
using System.Windows;

namespace SumikkoCompanion
{
    // Offsets in the character artboard, independent of window size. A zero-width
    // or zero-height range intentionally locks that axis.
    public sealed class MouseMotionRange
    {
        public double Left { get; private set; }
        public double Right { get; private set; }
        public double Top { get; private set; }
        public double Bottom { get; private set; }
        public static MouseMotionRange Default { get { return new MouseMotionRange(-120,120,-45,5); } }

        public MouseMotionRange(double left, double right, double top, double bottom)
        {
            left = FiniteClamp(left,-220,220,-120);
            right = FiniteClamp(right,-220,220,120);
            top = FiniteClamp(top,-160,8,-45);
            bottom = FiniteClamp(bottom,-160,8,5);
            Left = Math.Min(left,right);
            Right = Math.Max(left,right);
            Top = Math.Min(top,bottom);
            Bottom = Math.Max(top,bottom);
        }

        public Point Map(double pointerX, double pointerY)
        {
            // Inverted axes preserve the approved across-the-desk mouse direction.
            double x = (1 - FiniteClamp(pointerX,-1,1,0)) * .5;
            double y = (1 - FiniteClamp(pointerY,-1,1,0)) * .5;
            return new Point(Left+(Right-Left)*x,Top+(Bottom-Top)*y);
        }

        public MouseMotionRange Reduced()
        {
            double cx=(Left+Right)*.5, cy=(Top+Bottom)*.5;
            return new MouseMotionRange(cx+(Left-cx)*.32,cx+(Right-cx)*.32,
                cy+(Top-cy)*.32,cy+(Bottom-cy)*.32);
        }

        private static double FiniteClamp(double value,double min,double max,double fallback)
        {
            if(double.IsNaN(value)||double.IsInfinity(value))value=fallback;
            return Math.Max(min,Math.Min(max,value));
        }
    }
}
