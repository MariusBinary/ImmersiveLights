using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ImmersiveLights.Structures
{
    public enum ArrangementBeginEdge
    {
        TOP_LEFT, TOP_RIGHT,
        BOTTOM_RIGHT, BOTTOM_LEFT
    }

    public enum ArrangementSide
    {
        LEFT, BOTTOM, RIGHT, TOP
    }
    public enum ArrangementOrientation
    {
        CLOCKWISE, COUNTERCLOCKWISE
    }

    public struct ArrangementZone
    {
        public int[] margin { get; set; }
        public int[] disabled { get; set; }
        public int leds { get; set; }
        public int thickness { get; set; }
    }

    public struct ArrangementConfig
    {
        public ArrangementZone left;
        public ArrangementZone right;
        public ArrangementZone top;
        public ArrangementZone bottom;

        public ArrangementBeginEdge beginEdge { get; set; }
        public ArrangementOrientation orientation { get; set; }

        public ArrangementZone GetZone(ArrangementSide side)
        {
            switch (side)
            {
                case ArrangementSide.TOP:
                    return top;
                case ArrangementSide.BOTTOM:
                    return bottom;
                case ArrangementSide.LEFT:
                    return left;
                case ArrangementSide.RIGHT:
                    return right;
            }

            return new ArrangementZone();
        }

        public void SetZone(ArrangementSide side, ArrangementZone zone)
        {
            switch (side)
            {
                case ArrangementSide.TOP:
                    top = zone;
                    break;
                case ArrangementSide.BOTTOM:
                    bottom = zone;
                    break;
                case ArrangementSide.LEFT:
                    left = zone;
                    break;
                case ArrangementSide.RIGHT:
                    right = zone;
                    break;
            }
        }
    }
}
