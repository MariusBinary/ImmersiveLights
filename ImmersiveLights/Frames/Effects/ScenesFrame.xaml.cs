using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Core;
using ImmersiveLights.Models;
using ImmersiveLights.Helpers;
using ImmersiveLights.Controls;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Frames
{
    public partial class ScenesFrame : UserControl, IEffectCallback
    {
        #region Variables
        private TimerHandler timer;
        private IFrameCallback serial;
        private double oldBrightness = 255;
        private double maxBrightness = 255;
        private List<SceneModel> scenes;
        private SceneModel currentScene;
        private int selectedScene;
        private Bitmap frameBitmap;
        private byte[] serialData;
        private short[][] ledColor;
        private byte[][] gamma;
        private Rect dispBounds;
        private int[][] pixelOffset;
        private int[] densityZones;
        private bool canUpdate;
        private string imageBasePath;
        #endregion

        #region Main
        public ScenesFrame(IFrameCallback serial)
        {
            InitializeComponent();

            this.serial = serial;
            timer = new TimerHandler();
            timer.Create(new Action(() => { }), OnUpdate);

            LoadPreferences();
            OnEffectStarted();

            this.DataContext = this;
        }
        
        public void LoadPreferences()
        {
            bool lightSwitch = Preferences.GetPreference<bool>("settings", "lightSwitch");
            double lightIntensity = Preferences.GetPreference<double>("settings", "lightIntensity");
            maxBrightness = (short)(lightSwitch ? lightIntensity : 0.0);

            imageBasePath = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), $"ImmersiveLights\\scenes\\");

            scenes = Preferences.GetPreference<List<SceneModel>>("scenes", "items");

            scenes.ForEach(scene => {
                scene.Image = Path.Combine(imageBasePath, scene.FileName); 
            });

            selectedScene = Preferences.GetPreference<int>("scenes", "selected");
            selectedScene = selectedScene >= scenes.Count ? scenes.Count - 1 : selectedScene;

            if (selectedScene != -1) {
                selectedScene = selectedScene >= scenes.Count ? scenes.Count - 1 : selectedScene;
                scenes[selectedScene].IsActived = true;
                currentScene = scenes[selectedScene];
                canUpdate = true;
            }

            scenes.Add(new SceneModel() {
                Title = "Add scene",
                Image = "pack://application:,,,/ImmersiveLights;component/Assets/ic_add_scene.png",
                IsAddItem = true
            });

            Pussy.ItemsSource = scenes;

            frameBitmap = new Bitmap(512, 512);
            dispBounds = new Rect(new System.Windows.Point(0, 0), new System.Windows.Size(512, 512));
            dispBounds.X = dispBounds.Y = 0;

            var arrangement = Preferences.GetPreference<ArrangementConfig>("settings", "arrangement");
            Arrangement.GeneratePixelBuffer(512, 512, arrangement, out pixelOffset, out densityZones);

            int totalLeds = arrangement.top.leds + arrangement.bottom.leds +
                arrangement.left.leds + arrangement.right.leds;

            // Inizializza gli array
            Utils.InitJaggedArray<short>(out ledColor, totalLeds, 3);
            serialData = new byte[10 + totalLeds * 3];

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

        #region Bitmap
        private void OnUpdate()
        {
            if (currentScene != null && (canUpdate || oldBrightness != maxBrightness)) {
                if (File.Exists(currentScene.Image)) {
                    Bitmap bitmap = new Bitmap(currentScene.Image);
                    OnCapturedFrame(bitmap);
                } else {
                    this.Dispatcher.Invoke(() => {
                        WpfMessageBox.Show(FindResource("alertSceneLoadErrorTitle") as string, 
                            String.Format(FindResource("alertSceneLoadErrorDescription") as string, currentScene.Image));
                    });
                }

                canUpdate = false;
                oldBrightness = maxBrightness;
            }
        }

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
                    if (densityB > 0)
                    {
                        ledColor[i][0] = (short)((((r / densityB) & 0xff)));
                        ledColor[i][1] = (short)((((g / densityB) & 0xff)));
                        ledColor[i][2] = (short)((((b / densityB) & 0xff)));
                    }

                    // Apply gamma curve and place in serial output buffer
                    serialData[j++] = gamma[(byte)((double)ledColor[i][0] * scale)][0];
                    serialData[j++] = gamma[(byte)((double)ledColor[i][1] * scale)][1];
                    serialData[j++] = gamma[(byte)((double)ledColor[i][2] * scale)][2];
                }

                Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
                frameBitmap.UnlockBits(bitmapData);

                serial.OnDataAvailable(serialData); // Issue data to Arduino
            }
        }
        #endregion

        /// <summary>
        /// Riceve la chiamata quando un elemento della lista degli effetti viene selezionato
        /// ed effettua a sua volta un'altra chiamata alla funzione che si occupa di attivare
        /// gli effetti, con uno specifico id.
        /// </summary>
        public ICommand ApplySceneCommand => new RelayCommand(item => {
            SceneModel model = item as SceneModel;

            if (model.IsAddItem) {

                // Ask user to select an image file to create the scene.
                System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
                openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Ask user to input a name for the scene.
                    string name = WpfInputBox.Show(FindResource("alertSceneInputNameTitle") as string,
                        String.Format(FindResource("alertSceneInputNameDescription") as string, scenes.Count));

                    if (String.IsNullOrEmpty(name)) {
                        return;
                    }

                    // Generate ID
                    StringBuilder builder = new StringBuilder();
                    Enumerable
                       .Range(65, 26)
                        .Select(e => ((char)e).ToString())
                        .Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
                        .Concat(Enumerable.Range(0, 10).Select(e => e.ToString()))
                        .OrderBy(e => Guid.NewGuid())
                        .Take(11)
                        .ToList().ForEach(e => builder.Append(e));
                    string id = builder.ToString();

                    var extension = Path.GetExtension(openFileDialog.FileName);
                    var fileName = $"{id}{extension}";
                    var filePath = Path.Combine(imageBasePath, fileName);

                    // Copia l'immagine nella cartella delle risorse del programma.
                    File.Copy(openFileDialog.FileName, filePath);

                    // Aggiunge la scena alla lista delle scene.
                    scenes.Insert(scenes.Count - 1, new SceneModel() {
                        Title = name,
                        FileName = fileName,
                        Image = filePath,
                        CanDelete = true
                    });

                    // Salva la scena.
                    var cleanedList = new List<SceneModel>(scenes);
                    cleanedList.RemoveAt(scenes.Count - 1);
                    Preferences.SetPreference<List<SceneModel>>("scenes", "items", cleanedList);

                    SelectScene(currentScene, scenes[scenes.Count - 2], scenes.Count - 2);

                    // Aggiorna la lista
                    Pussy.Items.Refresh();
                }
            } else {
                SelectScene(currentScene, model);
            }

        });

        /// <summary>
        /// Riceve la chiamata quando un elemento della lista degli effetti viene selezionato
        /// ed effettua a sua volta un'altra chiamata alla funzione che si occupa di attivare
        /// gli effetti, con uno specifico id.
        /// </summary>
        public ICommand DeleteSceneCommand => new RelayCommand(item => {
            SceneModel model = item as SceneModel;
            scenes.Remove(model);

            // Salva la scena.
            var cleanedList = new List<SceneModel>(scenes);
            cleanedList.RemoveAt(scenes.Count - 1);
            Preferences.SetPreference<List<SceneModel>>("scenes", "items", cleanedList);

            // Elimina il file dalla cartella.
            if (File.Exists(model.Image)) {
                File.Delete(model.Image);
            }

            // Aggiorna la lista
            Pussy.Items.Refresh();

            selectedScene -= 1;
            if (selectedScene >= 0) {
                ApplySceneCommand.Execute(scenes[selectedScene]);
            } else {
                Preferences.SetPreference<int>("scenes", "selected", selectedScene);
            }
        });

        private void SelectScene(SceneModel oldScene, SceneModel newScene, int index = -1)
        {
            // Disabilita la vecchia scena.
            if (oldScene != null) {
                oldScene.IsActived = false;
            }

            // Abilita la nuova scena.
            newScene.IsActived = true;
            currentScene = newScene;

            // Imposta l'indice della scena.
            if (index == -1) {
                selectedScene = scenes.IndexOf(newScene);
            } else {
                selectedScene = index;
            }

            // Salva l'indice della scena.
            Preferences.SetPreference<int>("scenes", "selected", selectedScene);

            canUpdate = true;
        }

        #region Events
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                var parent = ((Control)sender).Parent as UIElement;
                parent.RaiseEvent(eventArg);
            }
        }
        #endregion

        #region IEffectCallback
        /// <summary>
        /// Ritorna il tipo di effetto.
        /// </summary>
        public Effects EffectType { get { return Effects.SCENES; } }
        /// <summary>
        /// Indica che le impostazioni dell'effetto hanno subito una modifica
        /// e devono essere ricaricate.
        /// </summary>
        public void OnPreferencesChanged<T>(string key, T value)
        {
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
            timer.Stop(() => { });
        }
        #endregion
    }
}
