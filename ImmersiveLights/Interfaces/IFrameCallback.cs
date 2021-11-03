using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImmersiveLights.Interfaces
{
    public interface IFrameCallback
    {
        /// <summary>
        /// Invia i dati forniti.
        /// </summary>
        void OnDataAvailable(byte[] data);
    }
}
