using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using Spectrogram;
using Tempora.Classes.Audio;
using Tempora.Classes.Visual.AudioDisplay;
using Tempora.Classes.Visual;

namespace Tempora.Classes.DataHelpers;

/// <summary>
/// Helper class for generating and manipulating spectrograms.
/// </summary>
public static class SpectrogramHelper
{
    public static Colormap TemporaColormap = new Colormap(new CustomColormap(new List<Godot.Color> { GlobalConstants.TemporaBlue, new("ffffff") }));

    public static ImageTexture GetSpectrogramSlice(
    Godot.Image fullImage,
    int xStart,
    int xEnd,
    int targetHeight,
    int targetWidth)
    {
        targetHeight = Math.Max(targetHeight, 1);
        targetWidth = Math.Max(targetWidth, 1);
        int sliceHeight = fullImage.GetHeight();
        int imageWidth = fullImage.GetWidth();

        if (imageWidth == 0 || sliceHeight == 0)
            throw new ArgumentException("Cannot slice an empty image.");

        xStart = Math.Clamp(xStart, 0, imageWidth - 1);
        xEnd = Math.Clamp(xEnd, xStart + 1, imageWidth);
        int sliceWidth = xEnd - xStart;

        // Create image with the slice
        Godot.Image sliceImage = Godot.Image.CreateEmpty(sliceWidth, sliceHeight, false, fullImage.GetFormat());
        sliceImage.BlitRect(fullImage, new Rect2I(xStart, 0, sliceWidth, sliceHeight), new Vector2I(0, 0));

        // Resize vertically if needed
        if (sliceImage.GetHeight() != targetHeight)
        {
            sliceImage.Resize(targetWidth, targetHeight, Godot.Image.Interpolation.Nearest);
        }

        return ImageTexture.CreateFromImage(sliceImage);
    }

    /// <summary>
    /// Generate SpectrogramGenerator from AudioFile based on target width in pixels.
    /// </summary>
    public static SpectrogramGenerator GetSpectrogramGenerator_ByWidth(PcmData pcmData, int targetWidthPixels = 3000, int fftSize = 16384, int maxFreq = 2200, double multiplier = 16_000)
    {
        int stepSize = pcmData.PcmFloats[0].Length / targetWidthPixels;
        return GetSpectrogramGenerator(pcmData, stepSize, fftSize, maxFreq, multiplier);
    }

    /// <summary>
    /// Generate SpectrogramGenerator from AudioFile with known stepSize.
    /// </summary>
    public static SpectrogramGenerator GetSpectrogramGenerator(PcmData pcmData, int stepSize = 100, int fftSize = 16384, int maxFreq = 20000, double multiplier = 16_000)
    {
        if (stepSize < 1) throw new ArgumentOutOfRangeException(nameof(stepSize), "Must be at least 1.");
        double[] audio = pcmData.GetPcmAsDoubles(multiplier);
        int sampleRate = pcmData.SampleRate;
        var spectrogramGenerator = new SpectrogramGenerator(sampleRate, fftSize, stepSize, maxFreq);

        spectrogramGenerator.Add(audio);

        return spectrogramGenerator;
    }

    public static ImageTexture GenerateTexture(SpectrogramGenerator spectrogramGenerator, Colormap colormap, int intensity = 5, bool dB = true)
    {
        return ImageTexture.CreateFromImage(GenerateGodotImage(spectrogramGenerator, colormap, intensity, dB));
    }

    public static Godot.Image GenerateGodotImage(SpectrogramGenerator spectrogramGenerator, Colormap colormap, int intensity = 5, bool dB = true)
    {
        var ffts = spectrogramGenerator.GetFFTs();
        if (ffts.Count == 0)
            throw new ArgumentException("Not enough data in FFTs to generate an image yet.");

        int width = ffts.Count;
        int height = ffts[0].Length;
        byte[] rgba = new byte[width * height * 4];

        Parallel.For(0, width, x =>
        {
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                double value = ffts[x][sourceY];
                if (dB)
                    value = 20 * Math.Log10(value + 1);

                byte paletteIndex = (byte)Math.Clamp(value * intensity, 0, 255);
                var (r, g, b) = colormap.GetRGB(paletteIndex);
                int destinationY = height - 1 - sourceY;
                int pixel = (destinationY * width + x) * 4;
                rgba[pixel] = r;
                rgba[pixel + 1] = g;
                rgba[pixel + 2] = b;
                rgba[pixel + 3] = 255;
            }
        });

        return Godot.Image.CreateFromData(width, height, false, Godot.Image.Format.Rgba8, rgba);
    }
}
