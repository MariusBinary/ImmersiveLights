using System;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using ImmersiveLights.Core;
using ImmersiveLights.Structures;
using ImmersiveLights.Interfaces;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using ImmersiveLights.Pages;

namespace ImmersiveLights.Frames
{
    public partial class AmbilightFrame : UserControl, IEffectCallback
    {
        #region Variables
        private TimerHandler timer;
        private IFrameCallback serial;
        private Bitmap frameBitmap;
        private short minBrightness = 0;
        private short maxBrightness = 100;
        private short fade = 150;
        private byte[] serialData;
        private short[][] ledColor;
        private short[][] prevColor;
        private byte[][] gamma;
        private Rect dispBounds;
        private int[][] pixelOffset;
        private int[] densityZones;
        private int effectType;
        private int i;
        private int screenIndex;
        #endregion

        #region Main
        public AmbilightFrame(IFrameCallback serial)
        {
            InitializeComponent();
            this.serial = serial;

            timer = new TimerHandler();
            LoadPreferences();

            timer.Create(new Action(() => {
                InitializeDX11(screenIndex);
            }), new Action(() =>
            {
                if (isDX11Inizialized && isDX11Running) {
                    ProcessDX11();
                }
            }));

            OnEffectStarted();
        }

        public void LoadPreferences()
        {
            screenIndex = Preferences.GetPreference<int>("ambilight", "captureDevice");
            if (screenIndex < 0) screenIndex = 0;

            // Imposta l'intervallo di cattura.
            int captureIntensity = Preferences.GetPreference<int>("ambilight", "captureFramerate");
            int[] captureIntervals = new int[] { 8, 32, 64 };
            timer.SetInterval(captureIntervals[captureIntensity]);

            fade = Preferences.GetPreference<short>("ambilight", "ledTransition");
            minBrightness = Preferences.GetPreference<short>("ambilight", "minBrightness");

            // Carica le preferenze relative al filtro della banda.
            effectType = Preferences.GetPreference<int>("ambilight", "effectType");

            switch (effectType)
            {
                case 0:
                    Rad_AverageColor.IsChecked = true;
                    break;
                case 1:
                    Rad_ZoneDivision.IsChecked = true;
                    break;
            }

            bool lightSwitch = Preferences.GetPreference<bool>("settings", "lightSwitch");
            double lightIntensity = Preferences.GetPreference<double>("settings", "lightIntensity");
            maxBrightness = (short)(lightSwitch ? lightIntensity : 0.0);

            // Initialize screen capture code for each display's dimensions.
            frameBitmap = new Bitmap(512, 512);
            dispBounds = new Rect(new System.Windows.Point(0, 0), new System.Windows.Size(512, 512));
            dispBounds.X = dispBounds.Y = 0;

            var arrangement = Preferences.GetPreference<ArrangementConfig>("settings", "arrangement");
            Arrangement.GeneratePixelBuffer(512, 512, arrangement, out pixelOffset, out densityZones);

            int totalLeds = arrangement.top.leds + arrangement.bottom.leds + 
                arrangement.left.leds + arrangement.right.leds;

            // Inizializza gli array
            Utils.InitJaggedArray<short>(out ledColor, totalLeds, 3);
            Utils.InitJaggedArray<short>(out prevColor, totalLeds, 3);
            serialData = new byte[10 + totalLeds * 3];

            for (i = 0; i < totalLeds; i++)
            {
                prevColor[i][0] = prevColor[i][1] = prevColor[i][2] = (short)(minBrightness / 3);
            }

            // A special header / magic word is expected by the corresponding LED
            // streaming code running on the Arduino.  This only needs to be initialized
            // once (not in draw() loop) because the number of LEDs remains constant:
            serialData[0] = (byte)0x49;
            serialData[1] = (byte)0x6D;
            serialData[2] = (byte)0x6D;
            serialData[3] = (byte)0x65;
            serialData[4] = (byte)0x72;
            serialData[5] = (byte)0x73;
            serialData[6] = (byte)0x69;
            serialData[7] = (byte)0x76;
            serialData[8] = (byte)0x65;
            serialData[9] = (byte)0x4C;
            //serialData[10] = (byte)((leds.Length - 1) >> 8);   // LED count high byte
            //serialData[11] = (byte)((leds.Length - 1) & 0xff); // LED count low byte
            //serialData[12] = (byte)(serialData[10] ^ serialData[11] ^ 0x55); // Checksum

            var colorCorrection = Preferences.GetPreference<ColorCorrectionConfig>("settings", "colorCorrection");
            ColorCorrection.GenerateColorCorrectionMap(colorCorrection, out gamma);
        }
        #endregion

        #region Ambilight
        private void OnCapturedFrame(Bitmap frame)
        {
            using (Graphics graph = Graphics.FromImage(frameBitmap))
            {
                graph.DrawImage(frame, 0, 0, 512, 512);

                BitmapData bitmapData = frameBitmap.LockBits(new Rectangle(0, 0, 512, 512),
                  ImageLockMode.ReadOnly, frameBitmap.PixelFormat);
                // Maybe readonly -------

                int bytesPerPixel = Bitmap.GetPixelFormatSize(frameBitmap.PixelFormat) / 8;
                int byteCount = bitmapData.Stride * 512;
                byte[] pixels = new byte[byteCount];
                IntPtr ptrFirstPixel = bitmapData.Scan0;
                Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;

                //* PROCESSING **************************************************

                int i, j, o, weight, sum, deficit, s2;

                double scale = maxBrightness / 255.0;
                weight = 257 - fade;    // 'Weighting factor' for new frame vs. old
                j = 10;                 // Serial led data follows header / magic word

                // This computes a single pixel value filtered down from a rectangular
                // section of the screen.  While it would seem tempting to use the native
                // image scaling in Processing/Java, in practice this didn't look very
                // good -- either too pixelated or too blurry, no happy medium.  So
                // instead, a "manual" downsampling is done here.  In the interest of
                // speed, it doesn't actually sample every pixel within a block, just
                // a selection of 256 pixels spaced within the block...the results still
                // look reasonably smooth and are handled quickly enough for video.

                int densityA = densityZones.Length;
                for (i = 0; i < densityA; i++)
                {
                    int r = 0, g = 0, b = 0;
                    int densityB = densityZones[i];
                    for (o = 0; o < densityB; o++)
                    {
                        b += pixels[(pixelOffset[i][o] * bytesPerPixel) + 0];
                        g += pixels[(pixelOffset[i][o] * bytesPerPixel) + 1];
                        r += pixels[(pixelOffset[i][o] * bytesPerPixel) + 2];
                    }

                    // Blend new pixel value with the value from the prior frame
                    if (densityB > 0) {
                        ledColor[i][0] = (short)((((r / densityB) & 0xff) * weight + prevColor[i][0] * fade) >> 8);
                        ledColor[i][1] = (short)((((g / densityB) & 0xff) * weight + prevColor[i][1] * fade) >> 8);
                        ledColor[i][2] = (short)((((b / densityB) & 0xff) * weight + prevColor[i][2] * fade) >> 8);
                    }

                    // Boost pixels that fall below the minimum brightness
                    sum = ledColor[i][0] + ledColor[i][1] + ledColor[i][2];
                    if (sum < minBrightness)
                    {
                        if (sum == 0)
                        {
                            // To avoid divide-by-zero
                            deficit = minBrightness / 3; // Spread equally to R,G,B
                            ledColor[i][0] += (short)deficit;
                            ledColor[i][1] += (short)deficit;
                            ledColor[i][2] += (short)deficit;
                        }
                        else
                        {
                            deficit = minBrightness - sum;
                            s2 = sum * 2;
                            // Spread the "brightness deficit" back into R,G,B in proportion to
                            // their individual contribition to that deficit.  Rather than simply
                            // boosting all pixels at the low end, this allows deep (but saturated)
                            // colors to stay saturated...they don't "pink out."
                            ledColor[i][0] += (short)(deficit * (sum - ledColor[i][0]) / s2);
                            ledColor[i][1] += (short)(deficit * (sum - ledColor[i][1]) / s2);
                            ledColor[i][2] += (short)(deficit * (sum - ledColor[i][2]) / s2);
                        }
                    }

                    // Apply gamma curve and place in serial output buffer
                    serialData[j++] = gamma[(byte)((double)ledColor[i][0] * scale)][0];
                    serialData[j++] = gamma[(byte)((double)ledColor[i][1] * scale)][1];
                    serialData[j++] = gamma[(byte)((double)ledColor[i][2] * scale)][2];
                }

                Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
                frameBitmap.UnlockBits(bitmapData);

                serial.OnDataAvailable(serialData); // Issue data to Arduino

                // Copy LED color data to prior frame array for next pass
                Array.Copy(ledColor, 0, prevColor, 0, ledColor.Length);
            }
        }
        #endregion

        #region Screen Grabber
        private bool isDX11Inizialized = false;
        private bool isDX11Running = false;
        private int DX11Screen = 0;
        private SharpDX.Direct3D11.Device DX11Device;
        private Output1 DX11Output;
        private int DX11Width;
        private int DX11Height;
        private Texture2D DX11Texture;
        private OutputDuplication DX11DuplicatedOutput;

        private void InitializeDX11(int screen)
        {
            if (!isDX11Inizialized || DX11Screen != screen)
            {
                var factory = new Factory1();
                var adapter = factory.GetAdapter1(0);
                DX11Device = new SharpDX.Direct3D11.Device(adapter);
                screen = adapter.GetOutputCount() < screen ? 0 : screen;
                var output = adapter.GetOutput(screen);
                DX11Output = output.QueryInterface<Output1>();

                // Width/Height of desktop to capture
                DX11Width = output.Description.DesktopBounds.Right;
                DX11Height = output.Description.DesktopBounds.Bottom;

                // Create Staging texture CPU-accessible
                var textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = DX11Width,
                    Height = DX11Height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };

                DX11Texture = new Texture2D(DX11Device, textureDesc);
                DX11DuplicatedOutput = DX11Output.DuplicateOutput(DX11Device);
                isDX11Inizialized = true;
            }

            // Avvia la cattura.
            StartDX11();
        }
        private void StartDX11()
        {
            if (isDX11Inizialized)
            {
                isDX11Running = true;
            }
        }
        private void ProcessDX11()
        {
            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation duplicateFrameInformation;

                // Try to get duplicated frame within given time is ms
                if (DX11DuplicatedOutput.TryAcquireNextFrame(10, out duplicateFrameInformation, out screenResource) == Result.Ok)
                {
                    // copy resource into memory that can be accessed by the CPU
                    using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                        DX11Device.ImmediateContext.CopyResource(screenTexture2D, DX11Texture);

                    // Get the desktop capture texture
                    var mapSource = DX11Device.ImmediateContext.MapSubresource(DX11Texture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                    // Create Drawing.Bitmap
                    using (var bitmap = new Bitmap(DX11Width, DX11Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        var boundsRect = new System.Drawing.Rectangle(0, 0, DX11Width, DX11Height);

                        // Copy pixels from screen capture Texture to GDI bitmap
                        var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                        var sourcePtr = mapSource.DataPointer;
                        var destPtr = mapDest.Scan0;
                        for (int y = 0; y < DX11Height; y++)
                        {
                            // Copy a single line 
                            Utilities.CopyMemory(destPtr, sourcePtr, DX11Width * 4);

                            // Advance pointers
                            sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                            destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                        }

                        // Release source and dest locks
                        bitmap.UnlockBits(mapDest);
                        DX11Device.ImmediateContext.UnmapSubresource(DX11Texture, 0);

                        OnCapturedFrame(bitmap);
                    }
                    screenResource.Dispose();
                    DX11DuplicatedOutput.ReleaseFrame();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        private void StopDX11()
        {
            if (isDX11Inizialized)
            {
                isDX11Running = false;
                Task.Delay(250);
            }
        }
        private void FreeDX11()
        {
            // Arresta il metodo di cattura.
            StopDX11();

            // Rilascia le risorse occupate dal metodo di cattura.
            if (isDX11Inizialized)
            {
                DX11Device.Dispose();
                DX11Output.Dispose();
                DX11DuplicatedOutput.Dispose();
                DX11Texture.Dispose();
                isDX11Inizialized = false;
            }
        }
        #endregion

        #region Events
        private void Rad_AverageColor_Click(object sender, RoutedEventArgs e)
        {
            if (Rad_AverageColor.IsChecked.Value)
            {
                effectType = 0;
                Core.Preferences.SetPreference<int>("ambilight", "effectType", 0);
            }
        }

        private void Rad_ZoneDivision_Click(object sender, RoutedEventArgs e)
        {
            if (Rad_ZoneDivision.IsChecked.Value)
            {
                effectType = 1;
                Core.Preferences.SetPreference<int>("ambilight", "effectType", 1);
            }
        }
        #endregion

        #region IEffectCallback
        /// <summary>
        /// Ritorna il tipo di effetto.
        /// </summary>
        public Effects EffectType { get { return Effects.AMBILIGHT; } }
        /// <summary>
        /// Indica che le impostazioni dell'effetto hanno subito una modifica
        /// e devono essere ricaricate.
        /// </summary>
        public void OnPreferencesChanged<T>(string key, T value)
        {
            switch (key)
            {
                case "captureFramerate":
                    int captureIntensity = Convert.ToInt32(value);
                    int[] captureIntervals = new int[] { 8, 32, 64 };
                    timer.SetInterval(captureIntervals[captureIntensity]);
                    break;
                case "ledTransition":
                    fade = Convert.ToInt16(value);
                    break;
                case "minBrightness":
                    minBrightness = Convert.ToInt16(value);
                    break;
            }
        }
        public void OnBrightnessChanged(double brightness)
        {
            this.maxBrightness = (short)brightness;
        }
        /// <summary>
        /// Indica che l'effetto deve iniziare.
        /// </summary>
        public void OnEffectStarted()
        {
            timer.Start();
        }
        /// <summary>
        /// Indica che l'effetto deve fermarsi.
        /// </summary>
        public void OnEffectStopped()
        {
            timer.Stop(() => {
                StopDX11();
                FreeDX11();
            });
        }
        #endregion

        private void Run_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var mainWindow = ((MainWindow)Application.Current.MainWindow);
            mainWindow.ForceArrangementEditor();
        }
    }
}
