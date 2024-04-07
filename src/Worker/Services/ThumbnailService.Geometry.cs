using SixLabors.ImageSharp;

namespace Bounan.Downloader.Worker.Services;

internal partial class ThumbnailService
{
    private static class Geometry
    {
        public const int TmbWidth = 1920;
        public const int TmbHeight = 1080;
        private const float Angle = 0.52f;

        public static class BotName
        {
            private const int BaseWidth = 810;
            private const int Height = 130;
            private const int PaddingTop = 70;
            private const int TextPadding = 15;

            private static float ExtraWidth => (float)(Math.Sin(Angle) * Height);

            private static PointF TopLeft => new (TmbWidth - BaseWidth, PaddingTop);

            private static PointF TopRight => new (TmbWidth, PaddingTop);

            private static PointF BottomRight => new (TmbWidth, PaddingTop + Height);

            private static PointF BottomLeft => new (TmbWidth - BaseWidth - ExtraWidth, PaddingTop + Height);

            public static PointF[] Polygon => [TopLeft, TopRight, BottomRight, BottomLeft];

            public static RectangleF TextRectangle => new (
                TopLeft.X,
                TopLeft.Y + TextPadding,
                BaseWidth - TextPadding * 2,
                Height - TextPadding * 2);
        }

        public static class AnimeName
        {
            private const int Between = 20;
            private const int Top = 625;
            private const int Height = 270;
            private const int TextPadding = 20;

            public static class Large
            {
                private const int BaseWidth = 1220;

                private static float ExtraWidth => (float)(Math.Sin(Angle) * Height);

                public static PointF TopLeft => new ((float)TmbWidth / 2 - (float)BaseWidth / 2, Top);

                public static PointF TopRight => new (TopLeft.X + BaseWidth + ExtraWidth, Top);

                public static PointF BottomRight => new (TopLeft.X + BaseWidth, Top + Height);

                public static PointF BottomLeft => new (TopLeft.X - ExtraWidth, Top + Height);

                public static PointF[] Polygon => [TopLeft, TopRight, BottomRight, BottomLeft];

                public static RectangleF TextRectangle => new (
                    TopLeft.X,
                    TopLeft.Y + TextPadding,
                    BaseWidth,
                    Height - TextPadding * 2);
            }

            public static class MediumLeft
            {
                private const int Width = 60;

                private static readonly float ExtraWidth = (float)(Math.Sin(Angle) * Height);

                public static readonly PointF TopLeft = new (Large.TopLeft.X - Between - Width, Top);

                private static readonly PointF TopRight = new (Large.TopLeft.X - Between, Top);

                private static readonly PointF BottomRight = new (Large.TopLeft.X - Between - ExtraWidth, Top + Height);

                private static readonly PointF BottomLeft =
                    new (Large.TopLeft.X - Between - Width - ExtraWidth, Top + Height);

                public static readonly PointF[] Polygon = [TopLeft, TopRight, BottomRight, BottomLeft];
            }

            public static class SmallLeft
            {
                private const int Width = 30;

                private static readonly float ExtraWidth = (float)(Math.Sin(Angle) * Height);

                private static readonly PointF TopLeft = new (MediumLeft.TopLeft.X - Between - Width, Top);

                private static readonly PointF TopRight = new (MediumLeft.TopLeft.X - Between, Top);

                private static readonly PointF BottomRight =
                    new (MediumLeft.TopLeft.X - Between - ExtraWidth, Top + Height);

                private static readonly PointF BottomLeft =
                    new (MediumLeft.TopLeft.X - Between - Width - ExtraWidth, Top + Height);

                public static readonly PointF[] Polygon = [TopLeft, TopRight, BottomRight, BottomLeft];
            }

            public static class MediumRight
            {
                private const int Width = 60;

                private static readonly float ExtraWidth = (float)(Math.Sin(Angle) * Height);

                private static readonly PointF TopLeft = new (Large.TopRight.X + Between, Top);

                public static readonly PointF TopRight = new (Large.TopRight.X + Between + Width, Top);

                private static readonly PointF BottomRight =
                    new (Large.TopRight.X + Between + Width - ExtraWidth, Top + Height);

                private static readonly PointF BottomLeft =
                    new (Large.TopRight.X + Between - ExtraWidth, Top + Height);

                public static readonly PointF[] Polygon = [TopLeft, TopRight, BottomRight, BottomLeft];
            }

            public static class SmallRight
            {
                private const int Width = 30;

                private static readonly float ExtraWidth = (float)(Math.Sin(Angle) * Height);

                private static readonly PointF TopLeft = new (MediumRight.TopRight.X + Between, Top);

                private static readonly PointF TopRight = new (MediumRight.TopRight.X + Between + Width, Top);

                private static readonly PointF BottomRight =
                    new (MediumRight.TopRight.X + Between + Width - ExtraWidth, Top + Height);

                private static readonly PointF BottomLeft =
                    new (MediumRight.TopRight.X + Between - ExtraWidth, Top + Height);

                public static readonly PointF[] Polygon = [TopLeft, TopRight, BottomRight, BottomLeft];
            }
        }

        public static class Episode
        {
            private const int BaseWidth = 550;
            private const int Height = 125;
            private const int TextPadding = 15;

            private static float ExtraWidth => (float)(Math.Sin(Angle) * Height);
            private static PointF TopLeft => AnimeName.Large.BottomLeft;
            private static PointF TopRight => new (TopLeft.X + BaseWidth + ExtraWidth, TopLeft.Y);
            private static PointF BottomRight => new (TopLeft.X + BaseWidth, TopLeft.Y + Height);
            private static PointF BottomLeft => new (TopLeft.X - ExtraWidth, TopLeft.Y + Height);

            public static PointF[] Polygon => [TopLeft, TopRight, BottomRight, BottomLeft];

            public static RectangleF TextRectangle => new (
                TopLeft.X,
                TopLeft.Y + TextPadding,
                BaseWidth,
                Height - TextPadding * 2);
        }

        public static class Dub
        {
            private const int BaseWidth = 487;
            private const int Height = 125;
            private const int TextPadding = 15;

            private static float ExtraWidth => (float)(Math.Sin(Angle) * Height);
            private static PointF TopRight => AnimeName.Large.BottomRight;
            private static PointF BottomRight => new (TopRight.X - ExtraWidth, TopRight.Y + Height);
            private static PointF BottomLeft => new (TopRight.X - BaseWidth - ExtraWidth, TopRight.Y + Height);
            private static PointF TopLeft => new (TopRight.X - BaseWidth, TopRight.Y);

            public static PointF[] Polygon => [TopLeft, TopRight, BottomRight, BottomLeft];

            public static RectangleF TextRectangle => new (
                TopLeft.X,
                TopLeft.Y + TextPadding,
                BaseWidth - ExtraWidth,
                Height - TextPadding * 2);
        }
    }
}