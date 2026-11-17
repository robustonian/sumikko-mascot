using System;
using System.Windows;

namespace SumikkoCompanion
{
    // Measures a deliberate physical mouse movement; no timer or global input is involved.
    public sealed class MouseRangeCalibration
    {
        private const double TravelDips = 180;
        private Point origin;
        private double progress;

        public int StepIndex { get; private set; }
        public MouseMotionRange Range { get; private set; }
        public bool IsTracking { get; private set; }
        public double Progress { get { return progress; } }
        public Point PreviewPointer
        {
            get
            {
                switch (StepIndex)
                {
                    case 0: return new Point(0, -progress);
                    case 1: return new Point(0, progress);
                    case 2: return new Point(-progress, 0);
                    default: return new Point(progress, 0);
                }
            }
        }
        public double Distance
        {
            get
            {
                return StepIndex < 2 ? Range.Bottom - Range.Top : Range.Right - Range.Left;
            }
        }
        public double MaximumDistance
        {
            get
            {
                switch (StepIndex)
                {
                    case 0: return 8 - Range.Top;
                    case 1: return Range.Bottom + 160;
                    case 2: return 220 - Range.Left;
                    default: return Range.Right + 220;
                }
            }
        }

        public MouseRangeCalibration(MouseMotionRange initialRange)
        {
            if (initialRange == null) throw new ArgumentNullException("initialRange");
            Range = initialRange;
        }

        public void Begin(Point cursorInDips)
        {
            if (!IsFinite(cursorInDips)) return;
            origin = cursorInDips;
            progress = 0;
            IsTracking = true;
        }

        public void Observe(Point cursorInDips)
        {
            if (!IsTracking || !IsFinite(cursorInDips)) return;
            double movement;
            switch (StepIndex)
            {
                case 0: movement = origin.Y - cursorInDips.Y; break;
                case 1: movement = cursorInDips.Y - origin.Y; break;
                case 2: movement = origin.X - cursorInDips.X; break;
                default: movement = cursorInDips.X - origin.X; break;
            }
            progress = Math.Max(progress, Math.Max(0, Math.Min(1, movement / TravelDips)));
            if (progress >= 1) IsTracking = false;
        }

        public void HoldEndpoint()
        {
            progress = 1;
            IsTracking = false;
        }

        public void Stop()
        {
            IsTracking = false;
        }

        public void SetStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex > 3) throw new ArgumentOutOfRangeException("stepIndex");
            StepIndex = stepIndex;
            progress = 0;
            IsTracking = false;
        }

        public void SetDistance(double distance)
        {
            if (double.IsNaN(distance) || double.IsInfinity(distance)) return;
            distance = Math.Max(0, Math.Min(MaximumDistance, distance));
            switch (StepIndex)
            {
                case 0:
                    Range = new MouseMotionRange(Range.Left, Range.Right, Range.Top, Range.Top + distance);
                    break;
                case 1:
                    Range = new MouseMotionRange(Range.Left, Range.Right, Range.Bottom - distance, Range.Bottom);
                    break;
                case 2:
                    Range = new MouseMotionRange(Range.Left, Range.Left + distance, Range.Top, Range.Bottom);
                    break;
                default:
                    Range = new MouseMotionRange(Range.Right - distance, Range.Right, Range.Top, Range.Bottom);
                    break;
            }
            HoldEndpoint();
        }

        // The endpoint this step adjusts: moving the mouse up/down/left/right (screen axes)
        // carries the hand to the bottom/top/right/left of the range (the character faces you).
        public double Endpoint
        {
            get
            {
                switch (StepIndex)
                {
                    case 0: return Range.Bottom;
                    case 1: return Range.Top;
                    case 2: return Range.Right;
                    default: return Range.Left;
                }
            }
        }

        // Puts this step's endpoint back where it is in another range (the default, or the range
        // before the dialog opened), as far as the opposite endpoint allows.
        public void RestoreEndpoint(MouseMotionRange from)
        {
            SetDistance(DistanceTo(from));
        }

        // The slider distance at which this step's endpoint matches the one in another range.
        public double DistanceTo(MouseMotionRange from)
        {
            if (from == null) throw new ArgumentNullException("from");
            double distance;
            switch (StepIndex)
            {
                case 0: distance = from.Bottom - Range.Top; break;
                case 1: distance = Range.Bottom - from.Top; break;
                case 2: distance = from.Right - Range.Left; break;
                default: distance = Range.Right - from.Left; break;
            }
            return Math.Max(0, Math.Min(MaximumDistance, distance));
        }

        private static bool IsFinite(Point point)
        {
            return !double.IsNaN(point.X) && !double.IsInfinity(point.X) &&
                   !double.IsNaN(point.Y) && !double.IsInfinity(point.Y);
        }
    }
}
