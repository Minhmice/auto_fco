using System.Collections.Generic;
using System.Drawing;

namespace FCZ.Core.Models
{
    public class Scenario
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TargetProcess { get; set; } = "fczf";
        public Size WindowSize { get; set; }
        public List<Step> Steps { get; set; } = new List<Step>();
    }
}

