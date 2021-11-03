using System;
using System.Windows;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Core
{
    public static class Arrangement
    {
        private static Rect dispBounds;
        private static int row, col;
        private static int[] x, y;
        private static float step, start;

        public static void GeneratePixelBuffer(
            double screenWidth, double screenHeight, ArrangementConfig config, 
            out int[][] pixelOffset, out int[] zonesDensity)
        {
            int totalLeds = config.top.leds + config.bottom.leds + config.left.leds + config.right.leds;
            InitJaggedArray<int>(out pixelOffset, totalLeds, 32768);

            dispBounds = new Rect(new Point(0, 0), new Size(screenWidth, screenHeight));
            dispBounds.X = dispBounds.Y = 0;

            // Calcolo del percorso.
            int[] clockwiseSides = new int[] { 3, 2, 1, 0 };
            int[] counterClockwiseSides = new int[] { 0, 3, 2, 1 };

            ArrangementSide[] path = new ArrangementSide[4];
            for (int b = (int)config.beginEdge, i = 0; i < 4; i++)
            {
                if (config.orientation == ArrangementOrientation.CLOCKWISE)
                    path[i] = (ArrangementSide)clockwiseSides[(b + i) > 3 ? i - (4 - b) : (b + i)];
                else
                    path[i] = (ArrangementSide)counterClockwiseSides[(b - i) < 0 ? i : (b - i)];
            }

            zonesDensity = new int[totalLeds];
            int ledIndex = 0;
            for (int p = 0; p < path.Length; p++)
            {
                // Calcolo dei parametri per ogni zona.
                double areaWidth, areaHeight;
                float areaOffsetX = 0f, areaOffsetY = 0f;
                float xRange = 0f, yRange = 0f;

                ArrangementZone zone = config.left;
                switch (path[p])
                {
                    case ArrangementSide.LEFT: zone = config.left; break;
                    case ArrangementSide.RIGHT: zone = config.right; break;
                    case ArrangementSide.TOP: zone = config.top; break;
                    case ArrangementSide.BOTTOM: zone = config.bottom; break;
                }

                if (path[p] == ArrangementSide.LEFT || path[p] == ArrangementSide.RIGHT)
                {
                    areaWidth = dispBounds.Width;
                    xRange = (float)((zone.thickness * dispBounds.Width) / 100);
                }
                else
                {
                    double leftMargin = (zone.margin[0] * dispBounds.Width) / 100;
                    double rightMargin = (zone.margin[1] * dispBounds.Width) / 100;
                    areaWidth = dispBounds.Width - leftMargin - rightMargin;
                    xRange = (float)(areaWidth / zone.leds);
                    areaOffsetX = (float)leftMargin;
                }

                if (path[p] == ArrangementSide.BOTTOM || path[p] == ArrangementSide.TOP)
                {
                    areaHeight = dispBounds.Height;
                    yRange = (float)((zone.thickness * dispBounds.Height) / 100);
                }
                else
                {
                    double topMargin = (zone.margin[0] * dispBounds.Height) / 100;
                    double bottomMargin = (zone.margin[1] * dispBounds.Height) / 100;
                    areaHeight = dispBounds.Height - topMargin - bottomMargin;
                    yRange = (float)(areaHeight / zone.leds);
                    areaOffsetY = (float)topMargin;
                }

                int density = 30;
                int currentLed = 0;

                // Calcolo dei punti per ogni zona in base alla densità.
                while (currentLed < zone.leds)
                {
                    int led =
                       (config.orientation == ArrangementOrientation.CLOCKWISE && path[p] == ArrangementSide.BOTTOM) ||
                       (config.orientation == ArrangementOrientation.CLOCKWISE && path[p] == ArrangementSide.LEFT) ||
                       (config.orientation == ArrangementOrientation.COUNTERCLOCKWISE && path[p] == ArrangementSide.RIGHT) ||
                       (config.orientation == ArrangementOrientation.COUNTERCLOCKWISE && path[p] == ArrangementSide.TOP) ?
                       zone.leds - currentLed - 1 : currentLed;

                    bool isDisabled = Array.Exists(zone.disabled, x => x == led);
                    if (isDisabled) {
                        zonesDensity[ledIndex + currentLed] = 0;
                        currentLed++;
                        continue;
                    }

                    // Calcolo punti di inizio sull'asse orizzontale.
                    int zoneDensityLenghtX = (int)((xRange / 100) * density);
                    step = (int)(xRange / zoneDensityLenghtX);
                    zoneDensityLenghtX = (int)(xRange / step);
                    x = new int[zoneDensityLenghtX];

                    start = step * 0.5f;
                    if (path[p] == ArrangementSide.TOP || path[p] == ArrangementSide.BOTTOM)
                    {
                        start = ((int)areaOffsetX + (xRange * led)) + step * 0.5f;
                    }
                    else
                    {
                        if (path[p] == ArrangementSide.RIGHT)
                        {
                            start = ((float)areaWidth - (float)xRange) + step * 0.5f;
                        }
                    }

                    for (col = 0; col < zoneDensityLenghtX; col++)
                        x[col] = (int)(start + step * (float)col);

                    // Calcolo punti di inizio sull'asse verticale.
                    int zoneDensityLenghtY = (int)((yRange / 100) * density);
                    step = (int)(yRange / zoneDensityLenghtY);
                    zoneDensityLenghtY = (int)(yRange / step);
                    y = new int[zoneDensityLenghtY];

                    start = step * 0.5f;
                    if (path[p] == ArrangementSide.LEFT || path[p] == ArrangementSide.RIGHT)
                    {
                        start = (int)areaOffsetY + (yRange * led) + step * 0.5f;
                    }
                    else
                    {
                        if (path[p] == ArrangementSide.BOTTOM)
                        {
                            start = ((float)areaHeight - (float)yRange) + step * 0.5f;
                        }
                    }

                    for (row = 0; row < zoneDensityLenghtY; row++)
                        y[row] = (int)(start + step * (float)row);

                    // Translazione dei punti di ogni zona nel buffer principale.
                    for (row = 0; row < zoneDensityLenghtY; row++)
                    {
                        for (col = 0; col < zoneDensityLenghtX; col++)
                        {
                            pixelOffset[ledIndex + currentLed][row * zoneDensityLenghtX + col] =
                                (int)(y[row] * dispBounds.Width + x[col]);
                        }
                    }

                    zonesDensity[ledIndex + currentLed] = zoneDensityLenghtX * zoneDensityLenghtY;
                    currentLed++;
                }

                ledIndex += zone.leds;
            }
        }

        public static void InitJaggedArray<T>(out T[][] array, int size1, int size2)
        {
            array = new T[size1][];
            for (int i = 0; i < size1; i++)
            {
                array[i] = new T[size2];
            }
        }
    }
}
