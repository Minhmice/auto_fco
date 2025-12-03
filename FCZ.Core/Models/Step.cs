using System.Collections.Generic;
using System.Drawing;

namespace FCZ.Core.Models
{
    public abstract class Step
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? DelayAfterMs { get; set; }
    }

    public class WaitForImageThenClickStep : Step
    {
        public string Template { get; set; } = string.Empty;
        public Rectangle Region { get; set; }
        public double Threshold { get; set; } = 0.9;
        public int TimeoutMs { get; set; } = 5000;
        public int MaxRetries { get; set; } = 0;
    }

    public class WaitForImageStep : Step
    {
        public string Template { get; set; } = string.Empty;
        public Rectangle Region { get; set; }
        public double Threshold { get; set; } = 0.9;
        public int TimeoutMs { get; set; } = 5000;
    }

    public class ClickTemplateStep : Step
    {
        public string Template { get; set; } = string.Empty;
        public Rectangle Region { get; set; }
        public double Threshold { get; set; } = 0.9;
        public int TimeoutMs { get; set; } = 1000;
    }

    public class ClickPointStep : Step
    {
        public Point Point { get; set; }
    }

    public class TypeTextStep : Step
    {
        public string Target { get; set; } = "region";
        public Rectangle Region { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool ClearBefore { get; set; } = true;
    }

    public class WaitStep : Step
    {
        public int Ms { get; set; }
    }

    public class ConditionalBlockStep : Step
    {
        public Condition Condition { get; set; } = new Condition();
        public List<Step> IfTrueSteps { get; set; } = new List<Step>();
        public List<Step> IfFalseSteps { get; set; } = new List<Step>();
    }

    public class LoopStep : Step
    {
        public int Repeat { get; set; }
        public List<Step> Body { get; set; } = new List<Step>();
    }

    public class LogStep : Step
    {
        public string Message { get; set; } = string.Empty;
    }
}

