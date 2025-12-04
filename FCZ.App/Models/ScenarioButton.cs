using FCZ.Core.Models;

namespace FCZ.App.Models
{
    public class ScenarioButton
    {
        public int Index { get; set; }
        public Scenario? AssignedScenario { get; set; }
        public string DisplayName => AssignedScenario?.Name ?? $"Button {Index + 1}";
        public bool IsAssigned => AssignedScenario != null;
    }
}


