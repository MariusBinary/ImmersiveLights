using Newtonsoft.Json;
using System.Windows.Media;

namespace ImmersiveLights.Models
{
    public class ColorModel
    {
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }

        [JsonIgnore]
        public SolidColorBrush Color {
            get { return new SolidColorBrush(System.Windows.Media.Color.FromRgb(Red, Green, Blue)); }
        }
    }
}
