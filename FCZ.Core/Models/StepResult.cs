namespace FCZ.Core.Models
{
    public class StepResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long DurationMs { get; set; }
    }

    public class ScenarioResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long DurationMs { get; set; }
    }
}

