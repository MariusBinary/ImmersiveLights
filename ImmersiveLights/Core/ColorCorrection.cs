using System;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Core
{
    public static class ColorCorrection
    {
        public static void GenerateColorCorrectionMap(
            ColorCorrectionConfig config, out byte[][] gamma)
        {
            InitJaggedArray<byte>(out gamma, 256, 3);

            // Pre-compute gamma correction table for LED brightness levels:
            for (int i = 0; i < 256; i++)
            {
                double f = Math.Pow(i / 255.0, config.GammaCorrection);
                gamma[i][0] = (byte)(config.CorrectionEnabled ? (f * config.RedCorrection) : i);
                gamma[i][1] = (byte)(config.CorrectionEnabled ? (f * config.GreenCorrection) : i);
                gamma[i][2] = (byte)(config.CorrectionEnabled ? (f * config.BlueCorrection) : i);
            }
        }

        public static void InitJaggedArray<T>(out T[][] array, int size1, int size2)
        {
            array = new T[size1][];
            for (int i = 0; i < size1; i++) {
                array[i] = new T[size2];
            }
        }
    }
}
