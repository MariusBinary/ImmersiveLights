using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ImmersiveLights.Controls;
using ImmersiveLights.Core;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Pages
{
    public partial class ArrangementWizard : Window
    {
        #region Properties
        // General Properties
        public int BeginEdge { get; set; } = 0;
        public int Orientation { get; set; } = 0;

        // Left Properties
        public int LeftLEDs { get; set; } = 7;
        public int LeftTopMargin { get; set; } = 15;
        public int LeftBottomMargin { get; set; } = 15;
        public int LeftThickness { get; set; } = 15;
        public bool LeftEnabled { get; set; } = true;

        // Right Properties
        public int RightLEDs { get; set; } = 7;
        public int RightTopMargin { get; set; } = 15;
        public int RightBottomMargin { get; set; } = 15;
        public int RightThickness { get; set; } = 15;
        public bool RightEnabled { get; set; } = true;

        // Top Properties
        public int TopLEDs { get; set; } = 12;
        public int TopLeftMargin { get; set; } = 15;
        public int TopRightMargin { get; set; } = 15;
        public int TopThickness { get; set; } = 15;
        public bool TopEnabled { get; set; } = true;

        // Bottom Properties
        public int BottomLEDs { get; set; } = 12;
        public int BottomLeftMargin { get; set; } = 15;
        public int BottomRightMargin { get; set; } = 15;
        public int BottomThickness { get; set; } = 15;
        public bool BottomEnabled { get; set; } = true;
        #endregion

        #region Variables
        private double screenWidth = 0;
        private double screenHeight = 0;
        private ArrangemenetWizardSection currentSection;
        private ArrangementConfig config;
        private ArrangementSide[] path;
        private Point[][] canvasAreas;
        private int currentZone = -1;
        private object currentSender;
        private Rect dispBounds;
        private int[][] pixelOffset;
        private int row, col;
        private int[] x, y;
        private float step, start;
        private int totalLeds;
        private int[] zonesDensity;
        bool isAlgorithmEnabled = false;
        #endregion

        #region Main
        public ArrangementWizard()
        {
            InitializeComponent();

            LoadPreferences();
            this.DataContext = this;
        }

        public void LoadPreferences()
        {
            this.config = Preferences.GetPreference<ArrangementConfig>("settings", "arrangement");

            // General Properties
            BeginEdge = (int)this.config.beginEdge;
            Orientation = (int)this.config.orientation;

            // Left Properties
            LeftLEDs = this.config.left.leds;
            LeftTopMargin = this.config.left.margin[0];
            LeftBottomMargin = this.config.left.margin[1];
            LeftThickness = this.config.left.thickness;
            LeftEnabled = this.config.left.disabled.Length == 0;

            // Right Properties
            RightLEDs = this.config.right.leds;
            RightTopMargin = this.config.right.margin[0];
            RightBottomMargin = this.config.right.margin[1];
            RightThickness = this.config.right.thickness;
            RightEnabled = this.config.right.disabled.Length == 0;

            // Top Properties
            TopLEDs = this.config.top.leds;
            TopLeftMargin = this.config.top.margin[0];
            TopRightMargin = this.config.top.margin[1];
            TopThickness = this.config.top.thickness;
            TopEnabled = this.config.top.disabled.Length == 0;

            // Bottom Properties
            BottomLEDs = this.config.bottom.leds;
            BottomLeftMargin = this.config.bottom.margin[0];
            BottomRightMargin = this.config.bottom.margin[1];
            BottomThickness = this.config.bottom.thickness;
            BottomEnabled = this.config.bottom.disabled.Length == 0;
        }

        private void Show()
        {
            var a = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                FillBehavior = FillBehavior.HoldEnd,
                BeginTime = TimeSpan.FromSeconds(0),
                Duration = new Duration(TimeSpan.FromSeconds(0.8))
            };
            var storyboard = new Storyboard();

            storyboard.Children.Add(a);
            Storyboard.SetTarget(a, Tab_Main);
            Storyboard.SetTargetProperty(a, new PropertyPath(OpacityProperty));
            storyboard.Begin();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Render();
            Show();
        }
        #endregion

        public void Render()
        {
            screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

            ArrangementCanvas.Width = screenWidth;
            ArrangementCanvas.Height = screenHeight;

            canvasAreas = new Point[4][] {
                new Point[] { new Point(0,0), new Point(0, 0) },
                new Point[] { new Point(0,0), new Point(0, 0) },
                new Point[] { new Point(0,0), new Point(0, 0) },
                new Point[] { new Point(0,0), new Point(0, 0) }
            };

            ArrangementCanvas.Children.Clear();

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

                    if (led == 0) {
                        canvasAreas[p][0] = new Point(startX, startY);
                    } else if (led == zone.leds - 1) {
                        canvasAreas[p][1] = new Point(startX + xRange, startY + yRange);
                    }

                    bool isDisabled = Array.Exists(zone.disabled, x => x == led);

                    ArrangementZoneControl grid = new ArrangementZoneControl()
                    {
                        Width = width,
                        Height = height,
                        Zone = path[p],
                        Index = ledIndex + currentLed,
                        RealIndex = led,
                        Highlighted = false,
                        Enabled = !isDisabled
                    };

                    grid.OnEnabledChanged += Grid_OnEnabledChanged;

                    Canvas.SetLeft(grid, left);
                    Canvas.SetTop(grid, top);
                    ArrangementCanvas.Children.Add(grid);

                    currentLed++;
                }

                ledIndex += zone.leds;
            }

            return;
        }

        private void Grid_OnEnabledChanged(object sender, RoutedPropertyChangedEventArgs<int[]> e)
        {
            var arrangement = (ArrangementSide)e.NewValue[0];
            var led = e.NewValue[1];
            var state = e.NewValue[2];
            List<int> disabled;

            switch (arrangement)
            {
                case ArrangementSide.TOP:
                    disabled = this.config.top.disabled.ToList();
                    if (state == 0) {
                        if (!disabled.Contains(led)) {
                            disabled.Add(led);
                        }
                    } else {
                        disabled.Remove(led);
                    }
                    this.config.top.disabled = disabled.ToArray();
                    Sw_TopEnable.IsChecked = (disabled.Count == 0);
                    break;
                case ArrangementSide.BOTTOM:
                    disabled = this.config.bottom.disabled.ToList();
                    if (state == 0) {
                        if (!disabled.Contains(led)) {
                            disabled.Add(led);
                        }
                    } else {
                        disabled.Remove(led);
                    }

                    this.config.bottom.disabled = disabled.ToArray();
                    Sw_BottomEnable.IsChecked = (disabled.Count == 0);
                    break;
                case ArrangementSide.LEFT:
                    disabled = this.config.left.disabled.ToList();
                    if (state == 0) {
                        if (!disabled.Contains(led)) {
                            disabled.Add(led);
                        }
                    } else {
                        disabled.Remove(led);
                    }

                    this.config.left.disabled = disabled.ToArray();
                    Sw_LeftEnable.IsChecked = (disabled.Count == 0);
                    break;
                case ArrangementSide.RIGHT:
                    disabled = this.config.right.disabled.ToList();
                    if (state == 0) {
                        if (!disabled.Contains(led)) {
                            disabled.Add(led);
                        }
                    } else {
                        disabled.Remove(led);
                    }

                    this.config.right.disabled = disabled.ToArray();
                    Sw_RightEnable.IsChecked = (disabled.Count == 0);
                    break;
            }
        }

        private bool isInsideRect(double x1, double y1, double x2, double y2, double px, double py)
        {
            return px >= x1 && px <= x2 && py >= y1 && py <= y2;
        }

        private void ArrangementCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isAlgorithmEnabled) return;

            Point cursorPosition = e.GetPosition(ArrangementCanvas);
            double cursorX = cursorPosition.X;
            double cursorY = cursorPosition.Y;

            int newZone = -1;
            for (int i = 0; i < canvasAreas.Length; i++) {
                if (isInsideRect(
                    canvasAreas[i][0].X, 
                    canvasAreas[i][0].Y, 
                    canvasAreas[i][1].X, 
                    canvasAreas[i][1].Y,
                    cursorX,
                    cursorY)) {
                    newZone = i;
                    if (currentZone == i) break;
                }
            }

            if (newZone != currentZone) {
                ArrangementSide side = ArrangementSide.LEFT;
                if (newZone != -1) {
                    side = path[newZone];
                }

                for (int i = 0; i < ArrangementCanvas.Children.Count; i++) {
                    var zone = ArrangementCanvas.Children[i] as ArrangementZoneControl;

                    if (newZone == -1 || zone.Zone != side) {
                        zone.Highlighted = false;
                        zone.Opacity = newZone == -1 ? 1.0 : 0.2;
                        Canvas.SetZIndex(zone, 1);
                    } else {
                        zone.Highlighted = true;
                        zone.Opacity = 1.0;
                        Canvas.SetZIndex(zone, 9);
                    }
                }
            } else {
                return;
            }

            currentZone = newZone;
        }

        private void Btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            var topDisabled = this.config.top.disabled.Where(val => val < this.config.top.leds).ToArray();
            var bottomDisabled = this.config.bottom.disabled.Where(val => val < this.config.bottom.leds).ToArray();
            var leftDisabled = this.config.left.disabled.Where(val => val < this.config.left.leds).ToArray();
            var rightDisabled = this.config.right.disabled.Where(val => val < this.config.right.leds).ToArray();

            this.config = new ArrangementConfig()
            {
                top = new ArrangementZone()
                {
                    leds = TopLEDs,
                    margin = new int[] { TopLeftMargin, TopRightMargin },
                    thickness = TopThickness,
                    disabled = topDisabled
                },
                bottom = new ArrangementZone()
                {
                    leds = BottomLEDs,
                    margin = new int[] { BottomLeftMargin, BottomRightMargin },
                    thickness = BottomThickness,
                    disabled = bottomDisabled
                },
                left = new ArrangementZone()
                {
                    leds = LeftLEDs,
                    margin = new int[] { LeftTopMargin, LeftBottomMargin },
                    thickness = LeftThickness,
                    disabled = leftDisabled
                },
                right = new ArrangementZone()
                {
                    leds = RightLEDs,
                    margin = new int[] { RightTopMargin, RightBottomMargin },
                    thickness = RightThickness,
                    disabled = rightDisabled
                },
                beginEdge = (ArrangementBeginEdge)BeginEdge,
                orientation = (ArrangementOrientation)Orientation
            };

            Render();
        }

        private void Btn_Save_Click(object sender, RoutedEventArgs e)
        {
            Btn_Apply_Click(null, null);
            Preferences.SetPreference<ArrangementConfig>("settings", "arrangement", this.config);
            this.Close();
        }

        private void NewAlgorithm()
        {
            totalLeds = config.top.leds + config.bottom.leds + config.left.leds + config.right.leds;

            // Inizializza gli array
            Utils.InitJaggedArray<int>(out pixelOffset, totalLeds, 32768);

            // Initialize screen capture code for each display's dimensions.
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

            Brush[] brushes = new Brush[totalLeds];
            zonesDensity = new int[totalLeds];
            int ledIndex = 0;
            for (int p = 0; p < path.Length; p++)
            {
                // -----------------------------------------------------------
                double areaWidth, areaHeight;
                float areaOffsetX = 0f, areaOffsetY = 0f;
                float xRange = 0f, yRange = 0f;

                Brush tempBrush = null;

                ArrangementZone zone = config.left;
                switch (path[p])
                {
                    case ArrangementSide.LEFT: zone = config.left; break;
                    case ArrangementSide.RIGHT: zone = config.right; break;
                    case ArrangementSide.TOP: zone = config.top; break;
                    case ArrangementSide.BOTTOM: zone = config.bottom; break;
                }

                // -----------------------------------------------------------
                if (path[p] == ArrangementSide.LEFT || path[p] == ArrangementSide.RIGHT)
                {
                    areaWidth = dispBounds.Width;
                    xRange = (float)((zone.thickness * dispBounds.Width) / 100);
                    tempBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00));
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
                    tempBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF));
                }
                else
                {
                    double topMargin = (zone.margin[0] * dispBounds.Height) / 100;
                    double bottomMargin = (zone.margin[1] * dispBounds.Height) / 100;
                    areaHeight = dispBounds.Height - topMargin - bottomMargin;
                    yRange = (float)(areaHeight / zone.leds);
                    areaOffsetY = (float)topMargin;
                }

                //Brush tempBrush = new SolidColorBrush(Color.FromRgb((byte)r.Next(1, 255),
                //    (byte)r.Next(1, 255), (byte)r.Next(1, 233)));

                int density = 30;
                int currentLed = 0;

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

                    // punti di inizio orizzontali.
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

                    // punti di inizio verticali.
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


                    // Riempio la zona.
                    for (row = 0; row < zoneDensityLenghtY; row++)
                    {
                        for (col = 0; col < zoneDensityLenghtX; col++)
                        {
                            pixelOffset[ledIndex + currentLed][row * zoneDensityLenghtX + col] =
                                (int)(y[row] * dispBounds.Width + x[col]);
                        }
                    }

                    zonesDensity[ledIndex + currentLed] = zoneDensityLenghtX * zoneDensityLenghtY;
                    brushes[ledIndex + currentLed] = tempBrush;
                    currentLed++;
                }

                ledIndex += zone.leds;
            }

            NewPlot();

            Console.WriteLine("Debug");
        }

        void NewPlot()
        {
            int totalPixels = 0;

            // http://writeablebitmapex.codeplex.com/
            WriteableBitmap writeableBmp = BitmapFactory.New((int)screenWidth, (int)screenHeight);
            using (writeableBmp.GetBitmapContext())
            {
                for (int b = 0; b < totalLeds; b++)
                {
                    //brushes[i].Opacity = 0.8;
                    for (int n = 0; n < zonesDensity[b]; n++)
                    {
                        int pixel = pixelOffset[b][n];
                        int yA = (int)(pixel / screenWidth);
                        int xA = (int)(pixel % screenWidth);
                        totalPixels++;
                        writeableBmp.SetPixel(xA, yA, Colors.Violet);
                    }
                }
                Image waveform = new Image();
                waveform.Source = writeableBmp;
                ArrangementCanvas.Children.Add(waveform);
            }

            Console.WriteLine("NEW PIXELS: " + totalPixels);
        }

        private void Sw_LeftEnable_Click(object sender, RoutedEventArgs e)
        {
            ChangeZonesEnableState(ArrangementSide.LEFT, Sw_LeftEnable.IsChecked.Value);
        }

        private void Sw_RightEnable_Click(object sender, RoutedEventArgs e)
        {
            ChangeZonesEnableState(ArrangementSide.RIGHT, Sw_RightEnable.IsChecked.Value);
        }

        private void Sw_TopEnable_Click(object sender, RoutedEventArgs e)
        {
            ChangeZonesEnableState(ArrangementSide.TOP, Sw_TopEnable.IsChecked.Value);
        }

        private void Sw_BottomEnable_Click(object sender, RoutedEventArgs e)
        {
            ChangeZonesEnableState(ArrangementSide.BOTTOM, Sw_BottomEnable.IsChecked.Value);
        }

        /// <summary>
        /// Modifica lo stato di tutte le zone appartenenti ad uno specifico lato.
        /// </summary>
        private void ChangeZonesEnableState(ArrangementSide side, bool state)
        {
            ArrangementZone zone = this.config.GetZone(side);

            if (!state) {
                int[] disabled = new int[zone.leds];
                for (int i = 0; i < zone.leds; i++) {
                    disabled[i] = i;
                }
                zone.disabled = disabled;
            } else {
                zone.disabled = new int[] { };
            }

            this.config.SetZone(side, zone);
            Btn_Apply_Click(null, null);
        }

        #region Navigation
        private enum ArrangemenetWizardSection
        {
            GENERAL, LEFT, RIGHT, TOP, BOTTOM
        }

        /// <summary>
        /// Update the current shown section.
        /// </summary>
        private void ShowSection(ArrangemenetWizardSection section, object sender = null)
        {
            Tab_GeneralSection.Visibility = Visibility.Hidden;
            Tab_LeftSection.Visibility = Visibility.Hidden;
            Tab_RightSection.Visibility = Visibility.Hidden;
            Tab_TopSection.Visibility = Visibility.Hidden;
            Tab_BottomSection.Visibility = Visibility.Hidden;

            if (section == currentSection) {
                section = ArrangemenetWizardSection.GENERAL;
            }

            if (currentSender != null)
            {
                (currentSender as Button).Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#3E3E3E"));
                (currentSender as Button).Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFFFF"));
            }

            if (sender != null && section != ArrangemenetWizardSection.GENERAL)
            {
                (sender as Button).Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#EAEAEA"));
                (sender as Button).Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#000000"));
            }

            switch (section)
            {
                case ArrangemenetWizardSection.GENERAL:
                    Tab_GeneralSection.Visibility = Visibility.Visible;
                    break;
                case ArrangemenetWizardSection.TOP:
                    Tab_TopSection.Visibility = Visibility.Visible;
                    break;
                case ArrangemenetWizardSection.BOTTOM:
                    Tab_BottomSection.Visibility = Visibility.Visible;
                    break;
                case ArrangemenetWizardSection.LEFT:
                    Tab_LeftSection.Visibility = Visibility.Visible;
                    break;
                case ArrangemenetWizardSection.RIGHT:
                    Tab_RightSection.Visibility = Visibility.Visible;
                    break;
            }

            currentSection = section;
            currentSender = sender;
        }

        private void Btn_LeftSide_Click(object sender, RoutedEventArgs e)
        {
            ShowSection(ArrangemenetWizardSection.LEFT, sender);
        }

        private void Btn_TopSide_Click(object sender, RoutedEventArgs e)
        {
            ShowSection(ArrangemenetWizardSection.TOP, sender);
        }

        private void Btn_RightSide_Click(object sender, RoutedEventArgs e)
        {
            ShowSection(ArrangemenetWizardSection.RIGHT, sender);
        }

        private void Btn_BottomSide_Click(object sender, RoutedEventArgs e)
        {
            ShowSection(ArrangemenetWizardSection.BOTTOM, sender);
        }
        #endregion
    }
}
