using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace Bounan.Downloader.Worker.Extensions;

internal static class ImageContextExtensions
{
    public static IImageProcessingContext DrawParallelogram(
        this IImageProcessingContext ctx,
        Color color,
        float angle,
        PointF leftTop,
        float width,
        float height)
    {
        var leftBottom = new PointF(leftTop.X - (float)(height * Math.Sin(angle)), leftTop.Y + height);
        var rightTop = new PointF(leftTop.X + width, leftTop.Y);
        var rightBottom = new PointF(leftTop.X + width - (float)(height * Math.Sin(angle)), leftTop.Y + height);

        ctx.FillPolygon(color, leftTop, rightTop, rightBottom, leftBottom);

        return ctx;
    }

    public static IImageProcessingContext ApplyScalingWaterMarkSimple(
        this IImageProcessingContext processingContext,
        Font font,
        string text,
        Color color,
        RectangleF layoutArea)
    {
        ArgumentNullException.ThrowIfNull(processingContext);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);

        // Measure the text size
        FontRectangle size = TextMeasurer.MeasureSize(text, new TextOptions(font));

        // Find out how much we need to scale the text to fill the space (up or down)
        float scalingFactor = Math.Min(layoutArea.Width / size.Width, layoutArea.Height / size.Height);
        if (scalingFactor > 1)
        {
            scalingFactor = 1;
        }

        // Create a new font
        Font scaledFont = new Font(font, scalingFactor * font.Size);

        var center = new PointF(layoutArea.Left + (layoutArea.Width / 2), layoutArea.Top + (layoutArea.Height / 2));
        var textOptions = new RichTextOptions(scaledFont)
        {
            Origin = center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return processingContext.DrawText(textOptions, text, color);
    }

    public static IImageProcessingContext ApplyScalingWaterMarkWordWrap(
        this IImageProcessingContext processingContext,
        Font font,
        string text,
        Color color,
        RectangleF layoutArea)
    {
        ArgumentNullException.ThrowIfNull(processingContext);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);

        float targetWidth = layoutArea.Width;
        float targetHeight = layoutArea.Height;
        PointF center = new PointF(layoutArea.Left + (targetWidth / 2), layoutArea.Top + (targetHeight / 2));

        const float targetMinHeight = 10;

        // Now we are working in 2 dimensions at once and can't just scale because it will cause the text to
        // reflow we need to just try multiple times
        var scaledFont = font;
        var fontRectangle = new FontRectangle(0, 0, float.MaxValue, float.MaxValue);

        const float multiplier = 0.1f;

        float scaleFactor = (scaledFont.Size * multiplier);
        int trapCount = (int)(scaledFont.Size / multiplier);
        if (trapCount < 10)
        {
            trapCount = 10;
        }

        bool isTooSmall = false;

        while ((fontRectangle.Height > targetHeight
                || fontRectangle.Height < targetMinHeight
                || fontRectangle.Width > targetWidth
               ) && trapCount > 0)
        {
            if (fontRectangle.Height > targetHeight)
            {
                if (isTooSmall)
                {
                    scaleFactor *= multiplier;
                }

                scaledFont = new Font(scaledFont, scaledFont.Size - scaleFactor);
                isTooSmall = false;
            }

            if (fontRectangle.Height < targetMinHeight)
            {
                if (!isTooSmall)
                {
                    scaleFactor *= multiplier;
                }

                scaledFont = new Font(scaledFont, scaledFont.Size + scaleFactor);
                isTooSmall = true;
            }

            trapCount--;

            fontRectangle = TextMeasurer.MeasureSize(
                text,
                new TextOptions(scaledFont)
                {
                    WrappingLength = targetWidth,
                    WordBreaking = WordBreaking.BreakWord,
                });
        }

        var textOptions = new RichTextOptions(scaledFont)
        {
            Origin = center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            WrappingLength = targetWidth,
            WordBreaking = WordBreaking.BreakWord,
        };

        return processingContext.DrawText(textOptions, text, color);
    }
}