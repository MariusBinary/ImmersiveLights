using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImmersiveLights.Structures
{
    public struct ColorCorrectionConfig
    {
        public bool CorrectionEnabled { get; set; }
        public double GammaCorrection { get; set; }
        public double RedCorrection { get; set; }
        public double GreenCorrection { get; set; }
        public double BlueCorrection { get; set; }
    }
}
