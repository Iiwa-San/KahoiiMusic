using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KahoiiMusic.Services
{
    public class DynamicThemePalette
    {
        // Compact mode 2-stop gradient
        public Color CompactTop { get; set; }
        public Color CompactBottom { get; set; }

        // Maximized mode 4-stop gradient
        public Color[] MaximizedColors { get; set; }
        public float[] MaximizedPositions { get; set; }

        // Accents
        public Color AccentColor { get; set; }
        public Color SecondaryAccentColor { get; set; }
        public Color ButtonGradientTop { get; set; }
        public Color ButtonGradientBottom { get; set; }

        public DynamicThemePalette Clone()
        {
            return new DynamicThemePalette
            {
                CompactTop = this.CompactTop,
                CompactBottom = this.CompactBottom,
                MaximizedColors = (Color[])this.MaximizedColors.Clone(),
                MaximizedPositions = (float[])this.MaximizedPositions.Clone(),
                AccentColor = this.AccentColor,
                SecondaryAccentColor = this.SecondaryAccentColor,
                ButtonGradientTop = this.ButtonGradientTop,
                ButtonGradientBottom = this.ButtonGradientBottom
            };
        }
    }

    public static class ColorExtractorService
    {
        private static readonly float[] DefaultMaximizedPositions = new float[] { 0.0f, 0.28f, 0.70f, 1.0f };

        public static DynamicThemePalette GetDefaultTheme()
        {
            return new DynamicThemePalette
            {
                CompactTop = Color.FromArgb(43, 16, 127),      // Signature Cyber dark purple (#2B107F)
                CompactBottom = Color.FromArgb(255, 38, 128),   // Signature Cyber vibrant pink (#FF2680)
                MaximizedColors = new Color[]
                {
                    Color.FromArgb(32, 12, 75),       // Dark rich indigo top (#200C4B)
                    Color.FromArgb(52, 20, 135),      // Vibrant purple (#341487)
                    Color.FromArgb(185, 15, 140),     // Magenta transition (#B90F8C)
                    Color.FromArgb(250, 0, 145)       // Bright radiant hot pink at bottom (#FA0091)
                },
                MaximizedPositions = (float[])DefaultMaximizedPositions.Clone(),
                AccentColor = Color.FromArgb(250, 0, 145),
                SecondaryAccentColor = Color.FromArgb(180, 50, 220),
                ButtonGradientTop = Color.FromArgb(105, 12, 150),
                ButtonGradientBottom = Color.FromArgb(235, 0, 130)
            };
        }

        public static DynamicThemePalette ExtractThemeFromBitmap(Bitmap sourceBmp)
        {
            if (sourceBmp == null) return GetDefaultTheme();

            Bitmap sample = null;
            bool shouldDisposeSample = false;

            try
            {
                if (sourceBmp.Width == 24 && sourceBmp.Height == 24)
                {
                    sample = sourceBmp;
                }
                else
                {
                    sample = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(sample))
                    {
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                        g.DrawImage(sourceBmp, 0, 0, 24, 24);
                    }
                    shouldDisposeSample = true;
                }

                // 24 hue bins (15 degrees each)
                const int binCount = 24;
                float[] binScores = new float[binCount];
                float[] binR = new float[binCount];
                float[] binG = new float[binCount];
                float[] binB = new float[binCount];
                float totalSat = 0f;
                int validPixelCount = 0;

                BitmapData data = sample.LockBits(
                    new Rectangle(0, 0, sample.Width, sample.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    int stride = data.Stride;
                    int bytes = Math.Abs(stride) * sample.Height;
                    byte[] rgbValues = new byte[bytes];
                    Marshal.Copy(data.Scan0, rgbValues, 0, bytes);

                    for (int y = 0; y < sample.Height; y++)
                    {
                        int rowOffset = y * stride;
                        for (int x = 0; x < sample.Width; x++)
                        {
                            int idx = rowOffset + (x * 4);
                            byte b = rgbValues[idx];
                            byte g = rgbValues[idx + 1];
                            byte r = rgbValues[idx + 2];
                            byte a = rgbValues[idx + 3];

                            if (a < 128) continue;

                            RgbToHsl(r, g, b, out float h, out float s, out float l);

                            validPixelCount++;
                            totalSat += s;

                            // Filter out extreme black or white
                            if (l < 0.08f || l > 0.92f) continue;

                            // Score weighting: favors saturated colors with medium lightness
                            float lumBalance = 1.0f - Math.Abs(l - 0.5f) * 1.35f;
                            if (lumBalance < 0.1f) lumBalance = 0.1f;
                            float score = (s * s) * lumBalance;

                            int bin = (int)(h / (360f / binCount)) % binCount;
                            if (bin < 0) bin = 0;

                            binScores[bin] += score;
                            binR[bin] += r * score;
                            binG[bin] += g * score;
                            binB[bin] += b * score;
                        }
                    }
                }
                finally
                {
                    sample.UnlockBits(data);
                }

                if (validPixelCount == 0) return GetDefaultTheme();

                float avgSat = totalSat / validPixelCount;

                // Find winning hue bin
                int bestBin = -1;
                float bestScore = 0f;
                for (int i = 0; i < binCount; i++)
                {
                    if (binScores[i] > bestScore)
                    {
                        bestScore = binScores[i];
                        bestBin = i;
                    }
                }

                // If image is almost completely monochrome / desaturated
                if (bestBin == -1 || bestScore < 0.5f || avgSat < 0.12f)
                {
                    // Fall back to a sleek deep slate / graphite theme
                    return new DynamicThemePalette
                    {
                        CompactTop = Color.FromArgb(18, 20, 26),
                        CompactBottom = Color.FromArgb(52, 58, 72),
                        MaximizedColors = new Color[]
                        {
                            Color.FromArgb(14, 15, 20),
                            Color.FromArgb(24, 27, 36),
                            Color.FromArgb(38, 43, 56),
                            Color.FromArgb(62, 70, 88)
                        },
                        MaximizedPositions = (float[])DefaultMaximizedPositions.Clone(),
                        AccentColor = Color.FromArgb(130, 145, 175),
                        SecondaryAccentColor = Color.FromArgb(90, 105, 130),
                        ButtonGradientTop = Color.FromArgb(45, 52, 68),
                        ButtonGradientBottom = Color.FromArgb(75, 86, 110)
                    };
                }

                // Calculate representative color from winning bin
                float winR = binR[bestBin] / bestScore;
                float winG = binG[bestBin] / bestScore;
                float winB = binB[bestBin] / bestScore;

                RgbToHsl((byte)Math.Max(0, Math.Min(255, (int)winR)),
                         (byte)Math.Max(0, Math.Min(255, (int)winG)),
                         (byte)Math.Max(0, Math.Min(255, (int)winB)),
                         out float baseHue, out float baseSat, out float baseLum);

                // Boost saturation for vivid, luminous appearance
                float vividSat = Math.Max(0.72f, Math.Min(baseSat * 1.15f, 0.96f));

                // Generate harmonious gradient stops:
                // Stop 0: Deep night tint (Dark top preserves header & text contrast)
                Color max0 = HslToColor(baseHue, 0.65f, 0.13f);

                // Stop 1 (0.28): Rich mid tone
                Color max1 = HslToColor((baseHue + 6f) % 360f, vividSat * 0.85f, 0.23f);

                // Stop 2 (0.70): Vibrant transition tone
                Color max2 = HslToColor((baseHue + 16f) % 360f, vividSat * 0.92f, 0.38f);

                // Stop 3 (1.00): Radiant neon glow rising from bottom
                Color max3 = HslToColor((baseHue + 26f) % 360f, vividSat, 0.52f);

                // Compact mode stops:
                Color compTop = HslToColor(baseHue, 0.70f, 0.15f);
                Color compBottom = HslToColor((baseHue + 22f) % 360f, vividSat, 0.46f);

                Color accent = max3;
                Color secAccent = max2;
                Color btnTop = HslToColor((baseHue + 10f) % 360f, vividSat * 0.80f, 0.28f);
                Color btnBottom = HslToColor((baseHue + 26f) % 360f, vividSat, 0.48f);

                return new DynamicThemePalette
                {
                    CompactTop = compTop,
                    CompactBottom = compBottom,
                    MaximizedColors = new Color[] { max0, max1, max2, max3 },
                    MaximizedPositions = (float[])DefaultMaximizedPositions.Clone(),
                    AccentColor = accent,
                    SecondaryAccentColor = secAccent,
                    ButtonGradientTop = btnTop,
                    ButtonGradientBottom = btnBottom
                };
            }
            catch
            {
                return GetDefaultTheme();
            }
            finally
            {
                if (shouldDisposeSample && sample != null)
                {
                    try { sample.Dispose(); } catch { }
                }
            }
        }

        public static Color Lerp(Color c1, Color c2, float t)
        {
            if (t <= 0f) return c1;
            if (t >= 1f) return c2;

            int a = (int)(c1.A + (c2.A - c1.A) * t);
            int r = (int)(c1.R + (c2.R - c1.R) * t);
            int g = (int)(c1.G + (c2.G - c1.G) * t);
            int b = (int)(c1.B + (c2.B - c1.B) * t);

            return Color.FromArgb(
                Math.Max(0, Math.Min(255, a)),
                Math.Max(0, Math.Min(255, r)),
                Math.Max(0, Math.Min(255, g)),
                Math.Max(0, Math.Min(255, b))
            );
        }

        public static DynamicThemePalette LerpPalette(DynamicThemePalette from, DynamicThemePalette to, float t)
        {
            if (from == null) return to;
            if (to == null) return from;
            if (t <= 0f) return from;
            if (t >= 1f) return to;

            Color[] maxColors = new Color[4];
            for (int i = 0; i < 4; i++)
            {
                Color cFrom = (from.MaximizedColors != null && i < from.MaximizedColors.Length) ? from.MaximizedColors[i] : to.MaximizedColors[i];
                Color cTo = (to.MaximizedColors != null && i < to.MaximizedColors.Length) ? to.MaximizedColors[i] : cFrom;
                maxColors[i] = Lerp(cFrom, cTo, t);
            }

            return new DynamicThemePalette
            {
                CompactTop = Lerp(from.CompactTop, to.CompactTop, t),
                CompactBottom = Lerp(from.CompactBottom, to.CompactBottom, t),
                MaximizedColors = maxColors,
                MaximizedPositions = (float[])to.MaximizedPositions.Clone(),
                AccentColor = Lerp(from.AccentColor, to.AccentColor, t),
                SecondaryAccentColor = Lerp(from.SecondaryAccentColor, to.SecondaryAccentColor, t),
                ButtonGradientTop = Lerp(from.ButtonGradientTop, to.ButtonGradientTop, t),
                ButtonGradientBottom = Lerp(from.ButtonGradientBottom, to.ButtonGradientBottom, t)
            };
        }

        public static void RgbToHsl(byte r, byte g, byte b, out float h, out float s, out float l)
        {
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;

            float max = Math.Max(rf, Math.Max(gf, bf));
            float min = Math.Min(rf, Math.Min(gf, bf));
            float delta = max - min;

            l = (max + min) / 2f;

            if (delta < 0.0001f)
            {
                h = 0f;
                s = 0f;
                return;
            }

            s = (l > 0.5f) ? delta / (2f - max - min) : delta / (max + min);

            if (Math.Abs(max - rf) < 0.0001f)
            {
                h = (gf - bf) / delta + (gf < bf ? 6f : 0f);
            }
            else if (Math.Abs(max - gf) < 0.0001f)
            {
                h = (bf - rf) / delta + 2f;
            }
            else
            {
                h = (rf - gf) / delta + 4f;
            }

            h *= 60f;
            if (h < 0f) h += 360f;
        }

        public static Color HslToColor(float h, float s, float l)
        {
            h = (h % 360f + 360f) % 360f;
            s = Math.Max(0f, Math.Min(1f, s));
            l = Math.Max(0f, Math.Min(1f, l));

            if (s < 0.0001f)
            {
                int val = (int)(l * 255f);
                return Color.FromArgb(val, val, val);
            }

            float q = (l < 0.5f) ? (l * (1f + s)) : (l + s - (l * s));
            float p = 2f * l - q;

            float hk = h / 360f;
            float tr = hk + (1f / 3f);
            float tg = hk;
            float tb = hk - (1f / 3f);

            byte r = (byte)(Math.Max(0f, Math.Min(1f, ColorChannelFromHue(p, q, tr))) * 255f);
            byte g = (byte)(Math.Max(0f, Math.Min(1f, ColorChannelFromHue(p, q, tg))) * 255f);
            byte b = (byte)(Math.Max(0f, Math.Min(1f, ColorChannelFromHue(p, q, tb))) * 255f);

            return Color.FromArgb(r, g, b);
        }

        private static float ColorChannelFromHue(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
}
