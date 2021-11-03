using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ImmersiveLights.Core;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Pages
{
    public partial class VirtualScreen : Window
    {
        #region Variables
        private Rectangle[] Leds;
        private bool loaded = false;
        private double screenWidth = 0;
        private double screenHeight = 0;
        private ArrangementConfig config;
        private ArrangementSide[] path;
        private Point[][] canvasAreas;
        private Rect dispBounds;
        private float step;
        #endregion

        public VirtualScreen()
        {
            InitializeComponent();

            LoadPreferences();
            this.DataContext = this;
        }

        public void LoadPreferences()
        {
            config = Preferences.GetPreference<ArrangementConfig>("settings", "arrangement");
        }

        public void Update(byte[] data)
        {
            if (!loaded) return;
            this.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < (data.Length - 10) / 3; i++)
                {
                    var background = new SolidColorBrush(Color.FromArgb(255,
                        data[10 + (i * 3)], data[10 + (i * 3) + 1], data[10 + (i * 3) + 2]));
                    Leds[i].Fill = background;
                }
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            screenWidth = this.ActualWidth;
            screenHeight = this.ActualHeight;

            int totalLeds = config.top.leds + config.bottom.leds + 
                config.left.leds + config.right.leds;

            Leds = new Rectangle[totalLeds];

            DrawAreaV.Width = screenWidth;
            DrawAreaV.Height = screenHeight;
            DrawAreaH.Width = screenWidth;
            DrawAreaH.Height = screenHeight;

            canvasAreas = new Point[4][] {
                new Point[] { new Point(0,0), new Point(0, 0) },
                new Point[] { new Point(0,0), new Point(0, 0) },
                new Point[] { new Point(0,0), new Point(0, 0) },
                new Point[] { new Point(0,0), new Point(0, 0) }
            };

            DrawAreaV.Children.Clear();
            DrawAreaH.Children.Clear();

            // Calcolo del percorso.
            int[] clockwiseSides = new int[] { 3, 2, 1, 0 };
            int[] counterClockwiseSides = new int[] { 0, 3, 2, 1 };

            path = new ArrangementSide[4];
            for (int b = (int)config.beginEdge, i = 0; i < 4; i++)
            {
                if (config.orientation == ArrangementOrientation.CLOCKWISE)
                    path[i] = (ArrangementSide)clockwiseSides[(b + i) > 3 ? i - (4 - b) : (b + i)];
                else
                    path[i] = (ArrangementSide)counterClockwiseSides[(b - i) < 0 ? i : (b - i)];
            }

            dispBounds = new Rect(new Point(0, 0), new Size(screenWidth, screenHeight));
            dispBounds.X = dispBounds.Y = 0;

            int ledIndex = 0;
            for (int p = 0; p < path.Length; p++)
            {
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
                    xRange = (float)(int)((zone.thickness * dispBounds.Width) / 100);
                }
                else
                {
                    double leftMargin = (zone.margin[0] * dispBounds.Width) / 100;
                    double rightMargin = (zone.margin[1] * dispBounds.Width) / 100;
                    areaWidth = dispBounds.Width - leftMargin - rightMargin;
                    xRange = (float)(int)(areaWidth / zone.leds);
                    areaOffsetX = (float)leftMargin;
                }

                if (path[p] == ArrangementSide.BOTTOM || path[p] == ArrangementSide.TOP)
                {
                    areaHeight = dispBounds.Height;
                    yRange = (float)(int)((zone.thickness * dispBounds.Height) / 100);
                }
                else
                {
                    double topMargin = (zone.margin[0] * dispBounds.Height) / 100;
                    double bottomMargin = (zone.margin[1] * dispBounds.Height) / 100;
                    areaHeight = dispBounds.Height - topMargin - bottomMargin;
                    yRange = (float)(int)(areaHeight / zone.leds);
                    areaOffsetY = (float)topMargin;
                }

                int currentLed = 0;
                while (currentLed < zone.leds)
                {
                    int led =
                       (config.orientation == ArrangementOrientation.CLOCKWISE && path[p] == ArrangementSide.BOTTOM) ||
                       (config.orientation == ArrangementOrientation.CLOCKWISE && path[p] == ArrangementSide.LEFT) ||
                       (config.orientation == ArrangementOrientation.COUNTERCLOCKWISE && path[p] == ArrangementSide.RIGHT) ||
                       (config.orientation == ArrangementOrientation.COUNTERCLOCKWISE && path[p] == ArrangementSide.TOP) ?
                       zone.leds - currentLed - 1 : currentLed;

                    double startX = step * 0.5f;
                    if (path[p] == ArrangementSide.TOP || path[p] == ArrangementSide.BOTTOM)
                    {
                        startX = ((int)areaOffsetX + (xRange * led)) + step * 0.5f;
                    }
                    else
                    {
                        if (path[p] == ArrangementSide.RIGHT)
                        {
                            startX = ((float)areaWidth - (float)xRange) + step * 0.5f;
                        }
                    }

                    double startY = step * 0.5f;
                    if (path[p] == ArrangementSide.LEFT || path[p] == ArrangementSide.RIGHT)
                    {
                        startY = (int)areaOffsetY + (yRange * led) + step * 0.5f;
                    }
                    else
                    {
                        if (path[p] == ArrangementSide.BOTTOM)
                        {
                            startY = ((float)areaHeight - (float)yRange) + step * 0.5f;
                        }
                    }

                    double width = xRange;
                    double height = yRange;
                    double left = startX;
                    double top = startY;

                    if (led == 0)
                    {
                        canvasAreas[p][0] = new Point(startX, startY);
                    }
                    else if (led == zone.leds - 1)
                    {
                        canvasAreas[p][1] = new Point(startX + xRange, startY + yRange);
                    }

                    bool isDisabled = Array.Exists(zone.disabled, x => x == led);

                    Rectangle grid = new Rectangle()
                    {
                        Width = width,
                        Height = height,
                        StrokeThickness = 2,
                        Stroke = Brushes.Black
                    };

                    Leds[ledIndex + currentLed] = grid;

                    Canvas.SetLeft(grid, left);
                    Canvas.SetTop(grid, top);


                    if (path[p] == ArrangementSide.RIGHT || path[p] == ArrangementSide.LEFT) {
                        DrawAreaV.Children.Add(grid);
                    } else {
                        DrawAreaH.Children.Add(grid);
                    }

                    currentLed++;
                }

                ledIndex += zone.leds;
            }

            loaded = true;
        }
    }
}
