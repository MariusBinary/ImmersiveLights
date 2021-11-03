using ImmersiveLights.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImmersiveLights.Interfaces
{
    public interface IFrameMetadata
    {
        /// <summary>
        /// Titolo del frame.
        /// </summary>
        string Title { get; }
        /// <summary>
        /// Titolo del frame.
        /// </summary>
        double InitialHeight { get; }
        /// <summary>
        /// Titolo del frame.
        /// </summary>
        bool TryToClose(string msg);
        /// <summary>
        /// Indica un cambio dello stato di collegamento.
        /// </summary>
        void OnConnectionChanged(ConnectionStatus status);
    }
}
