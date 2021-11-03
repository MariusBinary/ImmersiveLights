using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImmersiveLights.Interfaces
{
    public interface ISerialResponse
    {
        /// <summary>
        /// Esegue le istruzioni all'interno quando il dispositivo seriale è stato
        /// collegato corretamente.
        /// </summary>
        void OnBoardConnect();
        /// <summary>
        /// Esegue le istruzioni all'interno quando il dispositivo seriale è stato
        /// scollegato oppure quando la communicazione è stata interrota forzatamente.
        /// </summary>
        void OnBoardDisconnect();
        /// <summary>
        /// Esegue le istruzioni all'interno quando la communicazione con il dispositivo è 
        /// stata interrota forzatamente.
        /// </summary>
        void OnBoardError();
    }
}
