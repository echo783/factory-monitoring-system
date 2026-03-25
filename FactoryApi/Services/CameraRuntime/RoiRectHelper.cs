using FactoryApi.Models;
using OpenCvSharp;

namespace FactoryApi.Services.CameraRuntime
{
    public static class RoiRectHelper
    {
        public static (Rect objectRect, Rect labelRect) BuildFromCameraConfig(
            CameraConfig config,
            int frameWidth,
            int frameHeight)
        {
            var objectRect = BuildPixelRect(
                config.ObjectRoiX,
                config.ObjectRoiY,
                config.ObjectRoiW,
                config.ObjectRoiH,
                frameWidth,
                frameHeight);

            var labelRect = BuildPixelRect(
                config.LabelRoiX,
                config.LabelRoiY,
                config.LabelRoiW,
                config.LabelRoiH,
                frameWidth,
                frameHeight);

            return (objectRect, labelRect);
        }

        private static Rect BuildPixelRect(
            double x,
            double y,
            double w,
            double h,
            int frameWidth,
            int frameHeight)
        {
            if (frameWidth <= 0 || frameHeight <= 0)
                return new Rect(0, 0, 0, 0);

            int rx = (int)Math.Round(x);
            int ry = (int)Math.Round(y);
            int rw = (int)Math.Round(w);
            int rh = (int)Math.Round(h);

            if (rw <= 0 || rh <= 0)
                return new Rect(0, 0, 0, 0);

            rx = Math.Max(0, rx);
            ry = Math.Max(0, ry);

            int maxW = frameWidth - rx;
            int maxH = frameHeight - ry;

            if (maxW <= 0 || maxH <= 0)
                return new Rect(0, 0, 0, 0);

            rw = Math.Min(rw, maxW);
            rh = Math.Min(rh, maxH);

            if (rw <= 0 || rh <= 0)
                return new Rect(0, 0, 0, 0);

            return new Rect(rx, ry, rw, rh);
        }
    }
}