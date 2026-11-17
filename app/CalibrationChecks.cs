using System;
using System.Windows;

namespace SumikkoCompanion
{
    public static class CalibrationChecks
    {
        public static void Run(Action<bool, string> assert)
        {
            if (assert == null) throw new ArgumentNullException("assert");
            var initial = MouseMotionRange.Default;
            var calibration = new MouseRangeCalibration(initial);
            calibration.Observe(new Point(100, 0));
            assert(calibration.Progress == 0 && !calibration.IsTracking,
                "Calibration does not move before Begin");

            Point[] movements = {
                new Point(100, -90), new Point(100, 90),
                new Point(10, 0), new Point(190, 0)
            };
            Point[] expectedPointers = {
                new Point(0, -0.5), new Point(0, 0.5),
                new Point(-0.5, 0), new Point(0.5, 0)
            };
            for (int step = 0; step < 4; step++)
            {
                calibration.SetStep(step);
                assert(calibration.Progress == 0 && !calibration.IsTracking &&
                    ReferenceEquals(calibration.Range, initial), "Changing steps is idle and keeps the range");
                calibration.Begin(new Point(100, 0));
                calibration.Observe(new Point(100 - (movements[step].X - 100),
                    -(movements[step].Y)));
                assert(calibration.Progress == 0, "Wrong direction does not advance step " + step);
                calibration.Observe(movements[step]);
                assert(Near(calibration.Progress, 0.5) && calibration.PreviewPointer == expectedPointers[step],
                    "Physical direction maps to preview step " + step);
                Point mapped = calibration.Range.Map(calibration.PreviewPointer.X,
                    calibration.PreviewPointer.Y);
                assert(IsFinite(mapped), "Renderer inverse mapping remains finite at step " + step);
                calibration.Observe(new Point(100, 0));
                assert(Near(calibration.Progress, 0.5), "Returning cursor keeps the furthest position");
                calibration.Stop();
                calibration.Observe(movements[step]);
                assert(Near(calibration.Progress, 0.5), "Stop freezes the preview");
                calibration.HoldEndpoint();
                assert(calibration.Progress == 1 && !calibration.IsTracking,
                    "HoldEndpoint freezes at the limit");
            }

            CheckDistance(assert, initial, 0, 20, -120, 120, -45, -25);
            CheckDistance(assert, initial, 1, 30, -120, 120, -25, 5);
            CheckDistance(assert, initial, 2, 40, -120, -80, -45, 5);
            CheckDistance(assert, initial, 3, 50, 70, 120, -45, 5);

            calibration.SetStep(0);
            calibration.Begin(new Point(10, 10));
            calibration.Observe(new Point(double.NaN, 0));
            assert(calibration.Progress == 0, "Nonfinite cursor positions are ignored");
            calibration.SetDistance(double.PositiveInfinity);
            assert(calibration.Progress == 0 && ReferenceEquals(calibration.Range, initial),
                "Nonfinite distances are ignored");
            calibration.SetDistance(-100);
            assert(calibration.Distance == 0 && calibration.Range.Top == calibration.Range.Bottom,
                "Negative distances clamp to a valid zero span");
            calibration.SetStep(1);
            calibration.SetDistance(10000);
            assert(Near(calibration.Distance, calibration.MaximumDistance),
                "Distances clamp to the reachable endpoint");

            // One direction can be put back to the default, or to how it was when the dialog opened.
            var custom = new MouseMotionRange(-50, 60, -30, 0);
            double[][] restored = {
                new double[] { -50, 60, -30, 5 }, new double[] { -50, 60, -45, 0 },
                new double[] { -50, 120, -30, 0 }, new double[] { -120, 60, -30, 0 } };
            double[] endpoints = { 0, -30, 60, -50 };
            for (int step = 0; step < 4; step++)
            {
                var restore = new MouseRangeCalibration(custom);
                restore.SetStep(step);
                assert(Near(restore.Endpoint, endpoints[step]), "The endpoint of step " + step + " is the one its direction reaches");
                double expected = restore.DistanceTo(MouseMotionRange.Default);
                restore.RestoreEndpoint(MouseMotionRange.Default);
                double[] r = restored[step];
                assert(Near(restore.Range.Left, r[0]) && Near(restore.Range.Right, r[1]) && Near(restore.Range.Top, r[2]) &&
                    Near(restore.Range.Bottom, r[3]) && Near(restore.Distance, expected),
                    "Restoring step " + step + " to the default changes only its own endpoint");
                restore.RestoreEndpoint(custom);
                assert(Near(restore.Range.Left, custom.Left) && Near(restore.Range.Right, custom.Right) &&
                    Near(restore.Range.Top, custom.Top) && Near(restore.Range.Bottom, custom.Bottom),
                    "Restoring step " + step + " to the opening range brings it back exactly");
            }
            var crossing = new MouseRangeCalibration(new MouseMotionRange(-10, 10, -30, -20));
            crossing.SetStep(1);
            crossing.RestoreEndpoint(new MouseMotionRange(-10, 10, -5, 8));
            assert(Near(crossing.Range.Top, -20) && Near(crossing.Range.Bottom, -20),
                "A restored endpoint never crosses the opposite one");
        }

        private static void CheckDistance(Action<bool, string> assert, MouseMotionRange initial,
            int step, double distance, double left, double right, double top, double bottom)
        {
            var calibration = new MouseRangeCalibration(initial);
            calibration.SetStep(step);
            calibration.SetDistance(distance);
            assert(Near(calibration.Range.Left, left) && Near(calibration.Range.Right, right) &&
                Near(calibration.Range.Top, top) && Near(calibration.Range.Bottom, bottom) &&
                calibration.Progress == 1 && !calibration.IsTracking,
                "Distance changes only the chosen endpoint at step " + step);
        }

        private static bool Near(double a, double b)
        {
            return Math.Abs(a - b) < 0.000001;
        }

        private static bool IsFinite(Point point)
        {
            return !double.IsNaN(point.X) && !double.IsInfinity(point.X) &&
                !double.IsNaN(point.Y) && !double.IsInfinity(point.Y);
        }
    }
}
