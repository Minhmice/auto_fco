using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FCZ.Core.Models;
using FCZ.Core.Services;
using FCZ.Vision;

namespace FCZ.Core.Engine
{
    public class RuleEngine
    {
        private readonly IImageMatcher _imageMatcher;
        private readonly IInputService _inputService;
        private readonly TemplateStore _templateStore;
        private CancellationTokenSource? _cancellationTokenSource;
        private IntPtr _targetWindowHandle;

        public event Action<Step>? StepStarted;
        public event Action<Step, StepResult>? StepCompleted;
        public event Action<ScenarioResult>? ScenarioCompleted;

        public RuleEngine(IImageMatcher imageMatcher, IInputService inputService, TemplateStore templateStore)
        {
            _imageMatcher = imageMatcher;
            _inputService = inputService;
            _templateStore = templateStore;
        }

        public void SetTargetWindow(IntPtr hWnd)
        {
            _targetWindowHandle = hWnd;
        }

        public async Task<ScenarioResult> RunScenarioAsync(
            Scenario scenario,
            ICaptureService capture,
            CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            try
            {
                foreach (var step in scenario.Steps)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        return new ScenarioResult
                        {
                            Success = false,
                            Message = "Scenario cancelled",
                            DurationMs = stopwatch.ElapsedMilliseconds
                        };
                    }

                    StepStarted?.Invoke(step);
                    var stepResult = await ExecuteStepAsync(step, capture, _cancellationTokenSource.Token);

                    StepCompleted?.Invoke(step, stepResult);

                    if (!stepResult.Success && step.Type != "conditionalBlock")
                    {
                        return new ScenarioResult
                        {
                            Success = false,
                            Message = $"Step '{step.Id}' failed: {stepResult.Message}",
                            DurationMs = stopwatch.ElapsedMilliseconds
                        };
                    }

                    if (step.DelayAfterMs.HasValue && step.DelayAfterMs.Value > 0)
                    {
                        await Task.Delay(step.DelayAfterMs.Value, _cancellationTokenSource.Token);
                    }
                }

                stopwatch.Stop();
                var result = new ScenarioResult
                {
                    Success = true,
                    Message = "Scenario completed successfully",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };

                ScenarioCompleted?.Invoke(result);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var result = new ScenarioResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };

                ScenarioCompleted?.Invoke(result);
                return result;
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task<StepResult> ExecuteStepAsync(Step step, ICaptureService capture, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                return step switch
                {
                    WaitForImageThenClickStep s => await ExecuteWaitForImageThenClickAsync(s, capture, token),
                    WaitForImageStep s => await ExecuteWaitForImageAsync(s, capture, token),
                    ClickTemplateStep s => await ExecuteClickTemplateAsync(s, capture, token),
                    ClickPointStep s => ExecuteClickPoint(s),
                    TypeTextStep s => ExecuteTypeText(s),
                    WaitStep s => ExecuteWait(s),
                    ConditionalBlockStep s => await ExecuteConditionalBlockAsync(s, capture, token),
                    LoopStep s => await ExecuteLoopAsync(s, capture, token),
                    LogStep s => ExecuteLog(s),
                    _ => new StepResult { Success = false, Message = $"Unknown step type: {step.Type}" }
                };
            }
            catch (Exception ex)
            {
                return new StepResult
                {
                    Success = false,
                    Message = ex.Message,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private async Task<StepResult> ExecuteWaitForImageThenClickAsync(
            WaitForImageThenClickStep step,
            ICaptureService capture,
            CancellationToken token)
        {
            var template = _templateStore.GetTemplate(step.Template);
            if (template == null)
            {
                return new StepResult { Success = false, Message = $"Template not found: {step.Template}" };
            }

            var timeout = TimeSpan.FromMilliseconds(step.TimeoutMs);
            var startTime = DateTime.Now;
            int retries = 0;

            while (DateTime.Now - startTime < timeout && !token.IsCancellationRequested)
            {
                var frame = capture.LatestFrame;
                if (frame != null)
                {
                    using var frameMat = BitmapToMat(frame);
                    var match = _imageMatcher.MatchTemplate(frameMat, template, step.Region, step.Threshold);

                    if (match.Found)
                    {
                        _inputService.ClickWindowPoint(_targetWindowHandle, match.Location);
                        return new StepResult { Success = true, Message = "Image found and clicked" };
                    }
                }

                await Task.Delay(100, token);
            }

            if (retries < step.MaxRetries)
            {
                // Retry logic could be added here
            }

            return new StepResult { Success = false, Message = "Timeout waiting for image" };
        }

        private async Task<StepResult> ExecuteWaitForImageAsync(
            WaitForImageStep step,
            ICaptureService capture,
            CancellationToken token)
        {
            var template = _templateStore.GetTemplate(step.Template);
            if (template == null)
            {
                return new StepResult { Success = false, Message = $"Template not found: {step.Template}" };
            }

            var timeout = TimeSpan.FromMilliseconds(step.TimeoutMs);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout && !token.IsCancellationRequested)
            {
                var frame = capture.LatestFrame;
                if (frame != null)
                {
                    using var frameMat = BitmapToMat(frame);
                    var match = _imageMatcher.MatchTemplate(frameMat, template, step.Region, step.Threshold);

                    if (match.Found)
                    {
                        return new StepResult { Success = true, Message = "Image found" };
                    }
                }

                await Task.Delay(100, token);
            }

            return new StepResult { Success = false, Message = "Timeout waiting for image" };
        }

        private async Task<StepResult> ExecuteClickTemplateAsync(
            ClickTemplateStep step,
            ICaptureService capture,
            CancellationToken token)
        {
            var template = _templateStore.GetTemplate(step.Template);
            if (template == null)
            {
                return new StepResult { Success = false, Message = $"Template not found: {step.Template}" };
            }

            var frame = capture.LatestFrame;
            if (frame == null)
            {
                return new StepResult { Success = false, Message = "No frame available" };
            }

            using var frameMat = BitmapToMat(frame);
            var match = _imageMatcher.MatchTemplate(frameMat, template, step.Region, step.Threshold);

            if (match.Found)
            {
                _inputService.ClickWindowPoint(_targetWindowHandle, match.Location);
                return new StepResult { Success = true, Message = "Template clicked" };
            }

            return new StepResult { Success = false, Message = "Template not found" };
        }

        private StepResult ExecuteClickPoint(ClickPointStep step)
        {
            _inputService.ClickWindowPoint(_targetWindowHandle, step.Point);
            return new StepResult { Success = true, Message = "Point clicked" };
        }

        private StepResult ExecuteTypeText(TypeTextStep step)
        {
            if (step.Target == "region")
            {
                var centerX = step.Region.X + step.Region.Width / 2;
                var centerY = step.Region.Y + step.Region.Height / 2;
                _inputService.ClickWindowPoint(_targetWindowHandle, new Point(centerX, centerY));
                System.Threading.Thread.Sleep(100);
            }

            if (step.ClearBefore)
            {
                _inputService.ClearInputField();
            }

            _inputService.TypeText(step.Text);
            return new StepResult { Success = true, Message = "Text typed" };
        }

        private StepResult ExecuteWait(WaitStep step)
        {
            System.Threading.Thread.Sleep(step.Ms);
            return new StepResult { Success = true, Message = $"Waited {step.Ms}ms" };
        }

        private async Task<StepResult> ExecuteConditionalBlockAsync(
            ConditionalBlockStep step,
            ICaptureService capture,
            CancellationToken token)
        {
            var conditionResult = await EvaluateConditionAsync(step.Condition, capture, token);
            var stepsToExecute = conditionResult ? step.IfTrueSteps : step.IfFalseSteps;

            foreach (var subStep in stepsToExecute)
            {
                StepStarted?.Invoke(subStep);
                var result = await ExecuteStepAsync(subStep, capture, token);
                StepCompleted?.Invoke(subStep, result);

                if (!result.Success)
                {
                    return new StepResult { Success = false, Message = $"Conditional block step failed: {result.Message}" };
                }
            }

            return new StepResult { Success = true, Message = "Conditional block executed" };
        }

        private async Task<StepResult> ExecuteLoopAsync(
            LoopStep step,
            ICaptureService capture,
            CancellationToken token)
        {
            for (int i = 0; i < step.Repeat; i++)
            {
                foreach (var subStep in step.Body)
                {
                    StepStarted?.Invoke(subStep);
                    var result = await ExecuteStepAsync(subStep, capture, token);
                    StepCompleted?.Invoke(subStep, result);

                    if (!result.Success)
                    {
                        return new StepResult { Success = false, Message = $"Loop step failed at iteration {i + 1}: {result.Message}" };
                    }
                }
            }

            return new StepResult { Success = true, Message = $"Loop completed {step.Repeat} times" };
        }

        private StepResult ExecuteLog(LogStep step)
        {
            // Log message - could be sent to a logger
            return new StepResult { Success = true, Message = step.Message };
        }

        private async Task<bool> EvaluateConditionAsync(
            Condition condition,
            ICaptureService capture,
            CancellationToken token)
        {
            if (condition.Kind == "imageExists")
            {
                var template = _templateStore.GetTemplate(condition.Template);
                if (template == null)
                {
                    return false;
                }

                var timeout = TimeSpan.FromMilliseconds(condition.TimeoutMs);
                var startTime = DateTime.Now;

                while (DateTime.Now - startTime < timeout && !token.IsCancellationRequested)
                {
                    var frame = capture.LatestFrame;
                    if (frame != null)
                    {
                        using var frameMat = BitmapToMat(frame);
                        var match = _imageMatcher.MatchTemplate(frameMat, template, condition.Region, condition.Threshold);
                        if (match.Found)
                        {
                            return true;
                        }
                    }

                    await Task.Delay(100, token);
                }

                return false;
            }

            return false;
        }

        private OpenCvSharp.Mat BitmapToMat(Bitmap bitmap)
        {
            // Convert Bitmap to OpenCvSharp Mat
            // This is a simplified conversion
            var mat = new OpenCvSharp.Mat(bitmap.Height, bitmap.Width, OpenCvSharp.MatType.CV_8UC3);
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                unsafe
                {
                    byte* srcPtr = (byte*)data.Scan0;
                    byte* dstPtr = (byte*)mat.DataPointer;
                    int bytesPerPixel = 3;
                    int widthInBytes = data.Width * bytesPerPixel;

                    for (int y = 0; y < data.Height; y++)
                    {
                        Buffer.MemoryCopy(
                            srcPtr + y * data.Stride,
                            dstPtr + y * mat.Step(),
                            widthInBytes,
                            widthInBytes);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return mat;
        }
    }
}

