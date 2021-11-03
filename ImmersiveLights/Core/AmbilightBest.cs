using System;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace ImmersiveLights.Core
{
    public class AmbilightBest : IEffectCallback
    {
        private TimerHandler timer;
        private ISerialCallback serial;
        private AmbilightConfig ambilightConfig;
        private Bitmap frameBitmap;
        private int screenResizeWidth;
        private int screenResizeHeight;
        private int screenResizePixels;

        private int[] topZone;
        private int[] rightZone;
        private int[] bottomZone;
        private int[] leftZone;
        private int vZones = 0;
        private int hZones = 0;
        private int vZonesRGB = 0;
        private int hZonesRGB = 0;
        private int zoneWidth = 0;
        private int zoneHeight = 0;
        private int zoneOffset = 0 ;
        private int zoneTotal = 0;

        private int[,] displays = new int[,] {
            {0,12,7}
        };

        private int[,] leds = new int[,] {
            {0,11,6}, {0,11,5}, {0,11,4}, {0,11,3}, {0,11,2}, {0,11,1}, {0,11,0}, // Right
            {0,11,0}, {0,10,0}, {0,9,0}, {0,8,0}, {0,7,0}, {0,6,0}, {0,5,0}, {0,4,0}, {0,3,0}, {0,2,0}, {0,1,0}, {0,0,0}, // Top
            {0,0,0}, {0,0,1}, {0,0,2}, {0,0,3}, {0,0,4}, {0,0,5}, {0,0,6}, // Left
            {0,0,6}, {0,1,6}, {0,2,6}, {0,3,6}, {0,4,6}, {0,5,6}, {0,6,6}, {0,7,6}, {0,8,6}, {0,9,6}, {0,10,6}, {0,11,6}, // Bottom
        };

        public AmbilightBest(ISerialCallback serial, AmbilightConfig config)
        {
            this.serial = serial;
            this.ApplyConfig(config);

            timer = new TimerHandler();
            timer.SetInterval(10);
            timer.Create(new Action(() =>
            {
                InitializeDX11(0);
            }), new Action(() =>
            {
                if (isDX11Inizialized && isDX11Running)
                {
                    ProcessDX11();
                }
            }), null);
        }

        public void ApplyConfig(AmbilightConfig config)
        {
            ambilightConfig = config;

            // Imposta la dimensione dell'immagine.
            screenResizeWidth = 512;
            screenResizeHeight = 512;
            screenResizePixels = screenResizeHeight * screenResizeWidth;
            frameBitmap = new Bitmap(screenResizeWidth, screenResizeHeight);

            // Imposta la dimensione delle zone.
            topZone = new int[config.topZones * 3];
            rightZone = new int[config.rightZones * 3];
            bottomZone = new int[config.bottomZones * 3];
            leftZone = new int[config.leftZones * 3];

            // Mappatura dello schermo su una griglia.
            hZones = Math.Max(config.topZones, config.bottomZones);
            vZones = Math.Max(config.leftZones, config.rightZones);
            hZonesRGB = hZones * 3;
            vZonesRGB = vZones * 3;

            // Dimensione delle singole zone.
            zoneWidth = screenResizeWidth / hZones;
            zoneHeight = screenResizeHeight / vZones;
            zoneOffset = 2;
            zoneTotal = (zoneWidth * zoneHeight) * zoneOffset;
        }

        #region Ambilight
        private void OnCapturedFrame(Bitmap frame)
        {
            // Pulizia delle zone.
            Array.Clear(topZone, 0, hZonesRGB);
            Array.Clear(rightZone, 0, vZonesRGB);
            Array.Clear(bottomZone, 0, hZonesRGB);
            Array.Clear(leftZone, 0, vZonesRGB);

            using (Graphics g = Graphics.FromImage(frameBitmap))
            {
                g.DrawImage(frame, 0, 0, screenResizeWidth, screenResizeHeight);

                BitmapData bitmapData = frameBitmap.LockBits(new Rectangle(0, 0, screenResizeWidth, screenResizeHeight),
                  ImageLockMode.ReadWrite, frameBitmap.PixelFormat);

                int bytesPerPixel = Bitmap.GetPixelFormatSize(frameBitmap.PixelFormat) / 8;
                int byteCount = bitmapData.Stride * screenResizeWidth;
                byte[] pixels = new byte[byteCount];
                IntPtr ptrFirstPixel = bitmapData.Scan0;
                Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;


                for (int i = 0; i < leds.Length / 3; i++)
                {
                    int red = 0, green = 0, blue = 0;
                    int zoneWidth = (screenResizeWidth / displays[0, 1]) * 2;
                    int zoneHeight = (screenResizeHeight / displays[0, 2]) * 2;

                    //for (int x = leds[i, 1] * zoneWidth; x < zoneWidth; x++)
                    //{
                    //    for (int y = leds[i, 2] * zoneHeight; y < zoneHeight; y++)
                    //    {
                    //        leds[i,1]
                    //    }
                    //        blue += pixels[(leds[i][o] * bytesPerPixel) + 0];
                    //    green += pixels[(leds[i][o] * bytesPerPixel) + 1];
                    //    red += pixels[(leds[i][o] * bytesPerPixel) + 2];
                    //}

                }

                for (int y = 0; y < heightInPixels; y++)
                {
                    int currentLine = y * bitmapData.Stride;

                    // Indice della zona sull'asse verticale.
                    int vIndex = Convert.ToInt32(Math.Floor((double)y / (double)zoneHeight));
                    vIndex = vIndex == vZones ? vZones - 1 : vIndex;

                    for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                    {
                        // Indice della zona sull'asse orizzontale.
                        int hIndex = Convert.ToInt32(Math.Floor(((double)x / (double)bytesPerPixel) / (double)zoneWidth));
                        hIndex = hIndex == hZones ? hZones - 1 : hIndex;

                        // Colori del pixel.
                        int oldBlue = pixels[currentLine + x];
                        int oldGreen = pixels[currentLine + x + 1];
                        int oldRed = pixels[currentLine + x + 2];

                        if (vIndex < zoneOffset)
                        {
                            topZone[((hZones * 3) - (hIndex * 3) - (3 - 0))] += oldRed;
                            topZone[((hZones * 3) - (hIndex * 3) - (3 - 1))] += oldGreen;
                            topZone[((hZones * 3) - (hIndex * 3) - (3 - 2))] += oldBlue;
                        }

                        if (vIndex >= vZones - zoneOffset)
                        {
                            bottomZone[(hIndex * 3) + 0] += oldRed;
                            bottomZone[(hIndex * 3) + 1] += oldGreen;
                            bottomZone[(hIndex * 3) + 2] += oldBlue;
                        }

                        if (hIndex < zoneOffset)
                        {
                            leftZone[(vIndex * 3) + 0] += oldRed;
                            leftZone[(vIndex * 3) + 1] += oldGreen;
                            leftZone[(vIndex * 3) + 2] += oldBlue;
                        }

                        if (hIndex >= hZones - zoneOffset)
                        {
                            rightZone[((vZones * 3) - (vIndex * 3) - (3 - 0))] += oldRed;
                            rightZone[((vZones * 3) - (vIndex * 3) - (3 - 1))] += oldGreen;
                            rightZone[((vZones * 3) - (vIndex * 3) - (3 - 2))] += oldBlue;
                        }
                    }
                }

                Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
                frameBitmap.UnlockBits(bitmapData);
            }

            byte[] topZone1 = new byte[hZones * 3];
            byte[] rightZone1 = new byte[vZones * 3];
            byte[] bottomZone1 = new byte[hZones * 3];
            byte[] leftZone1 = new byte[vZones * 3];

            for (int i = 0; i < hZones; i++)
            {
                topZone1[(i * 3) + 0] = ToByte(topZone[(i * 3) + 0] / zoneTotal);
                topZone1[(i * 3) + 1] = ToByte(topZone[(i * 3) + 1] / zoneTotal);
                topZone1[(i * 3) + 2] = ToByte(topZone[(i * 3) + 2] / zoneTotal);

                bottomZone1[(i * 3) + 0] = ToByte(bottomZone[(i * 3) + 0] / zoneTotal);
                bottomZone1[(i * 3) + 1] = ToByte(bottomZone[(i * 3) + 1] / zoneTotal);
                bottomZone1[(i * 3) + 2] = ToByte(bottomZone[(i * 3) + 2] / zoneTotal);
            }

            for (int i = 0; i < vZones; i++)
            {

                leftZone1[(i * 3) + 0] = ToByte(leftZone[(i * 3) + 0] / zoneTotal);
                leftZone1[(i * 3) + 1] = ToByte(leftZone[(i * 3) + 1] / zoneTotal);
                leftZone1[(i * 3) + 2] = ToByte(leftZone[(i * 3) + 2] / zoneTotal);

                rightZone1[(i * 3) + 0] = ToByte(rightZone[(i * 3) + 0] / zoneTotal);
                rightZone1[(i * 3) + 1] = ToByte(rightZone[(i * 3) + 1] / zoneTotal);
                rightZone1[(i * 3) + 2] = ToByte(rightZone[(i * 3) + 2] / zoneTotal);
            }

            List<byte> list = new List<byte>();
            list.AddRange(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 });
            list.AddRange(rightZone1);
            list.AddRange(topZone1);
            list.AddRange(leftZone1);
            list.AddRange(bottomZone1);

            Console.WriteLine("SENDING: " + list.ToArray().Length);

            // Invio dei colori alla striscia led.
            serial.OnDataAvailable(list.ToArray());
        }
        #endregion

        #region Helpers
        public byte ToByte(int value)
        {
            return Convert.ToByte(value > 255 ? 255 : value);
        }
        #endregion

        #region Screen Garabber
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
                Thread.Sleep(250);
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

        #region IEffect
        public void OnEffectStart() {
            timer.Start();
        }

        public void OnEffectStop() {
            timer.Stop();
            StopDX11();
            FreeDX11();
        }
        #endregion
    }
}