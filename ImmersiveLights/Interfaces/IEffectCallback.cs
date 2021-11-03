using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImmersiveLights.Interfaces
{
    public enum Effects
    {
        NONE, COLOR, SCENES, MUSIC, AMBILIGHT
    }

    public interface IEffectCallback
    {
        /// <summary>
        /// Tipo di effetto.
        /// </summary>
        Effects EffectType { get; }
        /// <summary>
        /// Indica che le impostazioni dell'effetto hanno subito una modifica
        /// e devono essere ricaricate.
        /// </summary>
        void OnPreferencesChanged<T>(string key, T value);
        /// <summary>
        /// Indica che l'effetto deve iniziare.
        /// </summary>
        void OnBrightnessChanged(double brightness);
        /// <summary>
        /// Indica che l'effetto deve iniziare.
        /// </summary>
        void OnEffectStarted();
        /// <summary>
        /// Indica che l'effetto deve fermarsi.
        /// </summary>
        void OnEffectStopped();
    }
}
