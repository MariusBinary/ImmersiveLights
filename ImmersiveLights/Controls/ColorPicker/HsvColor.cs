using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImmersiveLights.Controls.ColorPicker
{
    internal struct HsvColor
    {
        public double H;
        public double S;
        public double V;

        public HsvColor(double h, double s, double v)
        {
            H = h;
            S = s;
            V = v;
        }
    }
}
