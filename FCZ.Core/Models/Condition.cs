using System.Drawing;

namespace FCZ.Core.Models
{
    public class Condition
    {
        public string Kind { get; set; } = "imageExists";
        public string Template { get; set; } = string.Empty;
        public Rectangle Region { get; set; }
        public double Threshold { get; set; } = 0.9;
        public int TimeoutMs { get; set; } = 3000;
    }
}

