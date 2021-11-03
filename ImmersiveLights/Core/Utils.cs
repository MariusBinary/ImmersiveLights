using System.Windows;
using ImmersiveLights.Interfaces;
using ImmersiveLights.Pages;

namespace ImmersiveLights.Core
{
    public class Utils
    {
        public static void InitJaggedArray<T>(out T[][] array, int size1, int size2)
        {
            array = new T[size1][];
            for (int i = 0; i < size1; i++)
            {
                array[i] = new T[size2];
            }
        }

        // Notifica il cambio di una preferenza ad un effetto.
        public static void NotifyEffect(Effects effect, string key, object value = null)
        {
            var mainWindow = ((MainWindow)Application.Current.MainWindow);
            if (mainWindow.currentEffect == effect && mainWindow.currentEffectControl != null) {
                (mainWindow.currentEffectControl as IEffectCallback).OnPreferencesChanged(key, value);
            }
        }
    }
}
