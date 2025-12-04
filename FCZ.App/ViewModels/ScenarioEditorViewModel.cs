using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCZ.Core.Configuration;
using FCZ.Core.Models;
using FCZ.Scenarios;
using Microsoft.Win32;

namespace FCZ.App.ViewModels
{
    public enum SelectionMode
    {
        None,
        Region,
        Point
    }

    public partial class ScenarioEditorViewModel : ObservableObject
    {
        [ObservableProperty]
        private Scenario? _currentScenario;

        private Step? _selectedStep;

        public Step? SelectedStep
        {
            get => _selectedStep;
            set
            {
                if (SetProperty(ref _selectedStep, value))
                {
                    if (CurrentScenario != null && value != null)
                    {
                        SelectedStepIndex = CurrentScenario.Steps.IndexOf(value);
                    }
                    else
                    {
                        SelectedStepIndex = -1;
                    }
                }
            }
        }

        [ObservableProperty]
        private int _selectedStepIndex = -1;

        [ObservableProperty]
        private ObservableCollection<string> _availableTemplates = new();

        [ObservableProperty]
        private ObservableCollection<string> _availableStepTypes = new();

        [ObservableProperty]
        private string _selectedStepType = string.Empty;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        [ObservableProperty]
        private SelectionMode _selectionMode = SelectionMode.None;

        [ObservableProperty]
        private Rectangle? _previewRegion;

        [ObservableProperty]
        private Point? _previewPoint;

        [ObservableProperty]
        private string _previewCoordinates = string.Empty;

        [ObservableProperty]
        private BitmapSource? _currentFrame;

        [ObservableProperty]
        private ObservableCollection<Size> _windowSizePresets = new();

        private Size _selectedWindowSize = new Size(1280, 720);

        public Size SelectedWindowSize
        {
            get => _selectedWindowSize;
            set
            {
                if (SetProperty(ref _selectedWindowSize, value))
                {
                    if (CurrentScenario != null)
                    {
                        CurrentScenario.WindowSize = value;
                        HasUnsavedChanges = true;
                    }
                }
            }
        }

        [ObservableProperty]
        private ObservableCollection<string> _validationErrors = new();

        private string? _currentScenarioPath;

        public ScenarioEditorViewModel()
        {
            InitializeStepTypes();
            InitializeWindowSizePresets();
            RefreshTemplates();
            NewScenario();
        }

        private void InitializeStepTypes()
        {
            AvailableStepTypes.Clear();
            AvailableStepTypes.Add("waitForImageThenClick");
            AvailableStepTypes.Add("waitForImage");
            AvailableStepTypes.Add("clickTemplate");
            AvailableStepTypes.Add("clickPoint");
            AvailableStepTypes.Add("typeText");
            AvailableStepTypes.Add("wait");
            AvailableStepTypes.Add("conditionalBlock");
            AvailableStepTypes.Add("loop");
            AvailableStepTypes.Add("log");
            SelectedStepType = AvailableStepTypes.FirstOrDefault() ?? string.Empty;
        }

        private void InitializeWindowSizePresets()
        {
            WindowSizePresets.Clear();
            WindowSizePresets.Add(new Size(800, 600));
            WindowSizePresets.Add(new Size(1024, 768));
            WindowSizePresets.Add(new Size(1280, 720));
            WindowSizePresets.Add(new Size(1920, 1080));
            SelectedWindowSize = new Size(1280, 720);
        }

        [RelayCommand]
        private void NewScenario()
        {
            CurrentScenario = new Scenario
            {
                Id = $"scenario_{DateTime.Now:yyyyMMddHHmmss}",
                Name = "New Scenario",
                Description = string.Empty,
                TargetProcess = "fczf",
                WindowSize = _selectedWindowSize,
                Steps = new System.Collections.Generic.List<Step>()
            };
            SelectedStep = null;
            SelectedStepIndex = -1;
            HasUnsavedChanges = false;
            _currentScenarioPath = null;
            ValidationErrors.Clear();
        }

        [RelayCommand]
        private void LoadScenario()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = AppConfig.ScenariosPath,
                Title = "Load Scenario"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var scenario = ScenarioSerializer.Load(dialog.FileName);
                    CurrentScenario = scenario;
                    _currentScenarioPath = dialog.FileName;
                    _selectedWindowSize = scenario.WindowSize;
                    OnPropertyChanged(nameof(SelectedWindowSize));
                    // Update window size in scenario if it doesn't match
                    if (CurrentScenario.WindowSize != _selectedWindowSize)
                    {
                        CurrentScenario.WindowSize = _selectedWindowSize;
                    }
                    SelectedStep = null;
                    SelectedStepIndex = -1;
                    HasUnsavedChanges = false;
                    ValidationErrors.Clear();
                }
                catch (Exception ex)
                {
                    ValidationErrors.Clear();
                    ValidationErrors.Add($"Error loading scenario: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void SaveScenario()
        {
            if (CurrentScenario == null)
                return;

            if (!ValidateScenarioForSave())
                return;

            string path;
            if (string.IsNullOrEmpty(_currentScenarioPath))
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = AppConfig.ScenariosPath,
                    FileName = $"{CurrentScenario.Id}.json",
                    Title = "Save Scenario"
                };

                if (dialog.ShowDialog() != true)
                    return;

                path = dialog.FileName;
            }
            else
            {
                path = _currentScenarioPath;
            }

            try
            {
                // Update window size from selected
                if (CurrentScenario != null)
                {
                    CurrentScenario.WindowSize = SelectedWindowSize;
                }

                ScenarioSerializer.Save(path, CurrentScenario);
                _currentScenarioPath = path;
                HasUnsavedChanges = false;
                ValidationErrors.Clear();
                
                // Notify parent to refresh scenarios list
                ScenarioSaved?.Invoke(CurrentScenario);
            }
            catch (Exception ex)
            {
                ValidationErrors.Clear();
                ValidationErrors.Add($"Error saving scenario: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SaveScenarioAs()
        {
            if (CurrentScenario == null)
                return;

            if (!ValidateScenarioForSave())
                return;

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = AppConfig.ScenariosPath,
                FileName = $"{CurrentScenario.Id}.json",
                Title = "Save Scenario As"
            };

            if (dialog.ShowDialog() == true)
            {
            try
            {
                // Update window size from selected
                if (CurrentScenario != null)
                {
                    CurrentScenario.WindowSize = SelectedWindowSize;
                }

                ScenarioSerializer.Save(dialog.FileName, CurrentScenario);
                _currentScenarioPath = dialog.FileName;
                HasUnsavedChanges = false;
                ValidationErrors.Clear();
                
                // Notify parent to refresh scenarios list
                ScenarioSaved?.Invoke(CurrentScenario);
            }
            catch (Exception ex)
            {
                ValidationErrors.Clear();
                ValidationErrors.Add($"Error saving scenario: {ex.Message}");
            }
            }
        }

        [RelayCommand]
        private void AddStep()
        {
            if (CurrentScenario == null || string.IsNullOrEmpty(SelectedStepType))
                return;

            Step? newStep = SelectedStepType switch
            {
                "waitForImageThenClick" => new WaitForImageThenClickStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "waitForImageThenClick",
                    Threshold = 0.9,
                    TimeoutMs = 5000,
                    MaxRetries = 0
                },
                "waitForImage" => new WaitForImageStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "waitForImage",
                    Threshold = 0.9,
                    TimeoutMs = 5000
                },
                "clickTemplate" => new ClickTemplateStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "clickTemplate",
                    Threshold = 0.9,
                    TimeoutMs = 1000
                },
                "clickPoint" => new ClickPointStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "clickPoint"
                },
                "typeText" => new TypeTextStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "typeText",
                    Target = "region",
                    ClearBefore = true
                },
                "wait" => new WaitStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "wait",
                    Ms = 1000
                },
                "conditionalBlock" => new ConditionalBlockStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "conditionalBlock",
                    Condition = new Condition(),
                    IfTrueSteps = new System.Collections.Generic.List<Step>(),
                    IfFalseSteps = new System.Collections.Generic.List<Step>()
                },
                "loop" => new LoopStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "loop",
                    Repeat = 1,
                    Body = new System.Collections.Generic.List<Step>()
                },
                "log" => new LogStep
                {
                    Id = $"step_{CurrentScenario.Steps.Count + 1}",
                    Type = "log",
                    Message = string.Empty
                },
                _ => null
            };

            if (newStep != null)
            {
                CurrentScenario.Steps.Add(newStep);
                SelectedStep = newStep;
                SelectedStepIndex = CurrentScenario.Steps.Count - 1;
                HasUnsavedChanges = true;
            }
        }

        [RelayCommand]
        private void RemoveStep()
        {
            if (CurrentScenario == null || SelectedStep == null || SelectedStepIndex < 0)
                return;

            CurrentScenario.Steps.RemoveAt(SelectedStepIndex);
            SelectedStep = null;
            SelectedStepIndex = -1;
            HasUnsavedChanges = true;
        }

        [RelayCommand]
        private void MoveStepUp()
        {
            if (CurrentScenario == null || SelectedStepIndex <= 0)
                return;

            var steps = CurrentScenario.Steps;
            var temp = steps[SelectedStepIndex];
            steps[SelectedStepIndex] = steps[SelectedStepIndex - 1];
            steps[SelectedStepIndex - 1] = temp;
            SelectedStepIndex--;
            HasUnsavedChanges = true;
        }

        [RelayCommand]
        private void MoveStepDown()
        {
            if (CurrentScenario == null || SelectedStepIndex < 0 || SelectedStepIndex >= CurrentScenario.Steps.Count - 1)
                return;

            var steps = CurrentScenario.Steps;
            var temp = steps[SelectedStepIndex];
            steps[SelectedStepIndex] = steps[SelectedStepIndex + 1];
            steps[SelectedStepIndex + 1] = temp;
            SelectedStepIndex++;
            HasUnsavedChanges = true;
        }

        [RelayCommand]
        private void DuplicateStep()
        {
            if (CurrentScenario == null || SelectedStep == null)
                return;

            // Simple deep copy via JSON serialization
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(SelectedStep);
                var duplicated = System.Text.Json.JsonSerializer.Deserialize<Step>(json);
                if (duplicated != null)
                {
                    duplicated.Id = $"step_{CurrentScenario.Steps.Count + 1}";
                    CurrentScenario.Steps.Insert(SelectedStepIndex + 1, duplicated);
                    SelectedStep = duplicated;
                    SelectedStepIndex++;
                    HasUnsavedChanges = true;
                }
            }
            catch
            {
                // Fallback: manual copy
            }
        }

        [RelayCommand]
        private void SelectRegionMode()
        {
            SelectionMode = SelectionMode.Region;
        }

        [RelayCommand]
        private void SelectPointMode()
        {
            SelectionMode = SelectionMode.Point;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            PreviewRegion = null;
            PreviewPoint = null;
            SelectionMode = SelectionMode.None;
        }

        [RelayCommand]
        private void UseSelectedRegion()
        {
            if (PreviewRegion.HasValue && SelectedStep != null)
            {
                var region = PreviewRegion.Value;
                switch (SelectedStep)
                {
                    case WaitForImageThenClickStep step:
                        step.Region = region;
                        break;
                    case WaitForImageStep step:
                        step.Region = region;
                        break;
                    case ClickTemplateStep step:
                        step.Region = region;
                        break;
                    case TypeTextStep step:
                        step.Region = region;
                        break;
                }
                HasUnsavedChanges = true;
            }
        }

        [RelayCommand]
        private void UseSelectedPoint()
        {
            if (PreviewPoint.HasValue && SelectedStep is ClickPointStep step)
            {
                step.Point = PreviewPoint.Value;
                OnPropertyChanged(nameof(SelectedStep)); // Trigger UI update
                HasUnsavedChanges = true;
            }
        }

        [RelayCommand]
        private void CaptureTemplate()
        {
            if (!PreviewRegion.HasValue || CurrentFrame == null)
            {
                ValidationErrors.Clear();
                ValidationErrors.Add("Please select a region on the preview first");
                return;
            }

            try
            {
                var region = PreviewRegion.Value;
                
                // Convert BitmapSource to Bitmap
                var bitmap = ConvertBitmapSourceToBitmap(CurrentFrame);
                if (bitmap == null)
                {
                    ValidationErrors.Clear();
                    ValidationErrors.Add("Failed to convert frame to bitmap");
                    return;
                }

                // Crop the region
                if (region.X < 0 || region.Y < 0 || 
                    region.X + region.Width > bitmap.Width || 
                    region.Y + region.Height > bitmap.Height)
                {
                    ValidationErrors.Clear();
                    ValidationErrors.Add("Selected region is out of bounds");
                    return;
                }

                var cropped = new System.Drawing.Bitmap(region.Width, region.Height);
                using (var g = System.Drawing.Graphics.FromImage(cropped))
                {
                    g.DrawImage(bitmap, new System.Drawing.Rectangle(0, 0, region.Width, region.Height), 
                               region, System.Drawing.GraphicsUnit.Pixel);
                }

                // Show save dialog
                var dialog = new SaveFileDialog
                {
                    Filter = "PNG files (*.png)|*.png|All files (*.*)|*.*",
                    InitialDirectory = AppConfig.TemplatesPath,
                    FileName = $"template_{DateTime.Now:yyyyMMddHHmmss}.png",
                    Title = "Save Template"
                };

                if (dialog.ShowDialog() == true)
                {
                    cropped.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    RefreshTemplates();
                    
                    // Auto-fill template name if editing a step that needs it
                    if (SelectedStep != null)
                    {
                        var templateName = Path.GetFileName(dialog.FileName);
                        switch (SelectedStep)
                        {
                            case WaitForImageThenClickStep step:
                                step.Template = templateName;
                                break;
                            case WaitForImageStep step:
                                step.Template = templateName;
                                break;
                            case ClickTemplateStep step:
                                step.Template = templateName;
                                break;
                        }
                        HasUnsavedChanges = true;
                    }
                }

                cropped.Dispose();
                bitmap.Dispose();
            }
            catch (Exception ex)
            {
                ValidationErrors.Clear();
                ValidationErrors.Add($"Error capturing template: {ex.Message}");
            }
        }

        private System.Drawing.Bitmap? ConvertBitmapSourceToBitmap(BitmapSource bitmapSource)
        {
            try
            {
                var stride = bitmapSource.PixelWidth * ((bitmapSource.Format.BitsPerPixel + 7) / 8);
                var buffer = new byte[stride * bitmapSource.PixelHeight];
                bitmapSource.CopyPixels(buffer, stride, 0);

                var bitmap = new System.Drawing.Bitmap(
                    bitmapSource.PixelWidth,
                    bitmapSource.PixelHeight,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bitmapData.Scan0, buffer.Length);
                bitmap.UnlockBits(bitmapData);

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        private void RefreshTemplates()
        {
            AvailableTemplates.Clear();
            if (Directory.Exists(AppConfig.TemplatesPath))
            {
                var files = Directory.GetFiles(AppConfig.TemplatesPath, "*.png");
                foreach (var file in files)
                {
                    AvailableTemplates.Add(Path.GetFileName(file));
                }
            }
        }

        [RelayCommand]
        private void ValidateScenarioCommand()
        {
            ValidateScenario();
        }

        private void ValidateScenario()
        {
            ValidationErrors.Clear();
            if (CurrentScenario == null)
            {
                ValidationErrors.Add("No scenario loaded");
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentScenario.Name))
            {
                ValidationErrors.Add("Scenario name is required");
            }

            if (CurrentScenario.WindowSize.Width <= 0 || CurrentScenario.WindowSize.Height <= 0)
            {
                ValidationErrors.Add("Invalid window size");
            }

            for (int i = 0; i < CurrentScenario.Steps.Count; i++)
            {
                var step = CurrentScenario.Steps[i];
                ValidateStep(step, i);
            }
        }

        private void ValidateStep(Step step, int index)
        {
            switch (step)
            {
                case WaitForImageThenClickStep s:
                    if (string.IsNullOrWhiteSpace(s.Template))
                        ValidationErrors.Add($"Step {index + 1}: Template is required");
                    if (s.Region.Width <= 0 || s.Region.Height <= 0)
                        ValidationErrors.Add($"Step {index + 1}: Invalid region");
                    break;
                case WaitForImageStep s:
                    if (string.IsNullOrWhiteSpace(s.Template))
                        ValidationErrors.Add($"Step {index + 1}: Template is required");
                    break;
                case ClickTemplateStep s:
                    if (string.IsNullOrWhiteSpace(s.Template))
                        ValidationErrors.Add($"Step {index + 1}: Template is required");
                    break;
                case ClickPointStep s:
                    if (s.Point.X < 0 || s.Point.Y < 0)
                        ValidationErrors.Add($"Step {index + 1}: Invalid point");
                    break;
                case TypeTextStep s:
                    if (string.IsNullOrWhiteSpace(s.Text))
                        ValidationErrors.Add($"Step {index + 1}: Text is required");
                    break;
                case WaitStep s:
                    if (s.Ms <= 0)
                        ValidationErrors.Add($"Step {index + 1}: Wait time must be > 0");
                    break;
                case LogStep s:
                    if (string.IsNullOrWhiteSpace(s.Message))
                        ValidationErrors.Add($"Step {index + 1}: Log message is required");
                    break;
            }
        }

        private bool ValidateScenarioForSave()
        {
            ValidateScenario();
            return ValidationErrors.Count == 0;
        }

        public void SetCurrentFrame(BitmapSource? frame)
        {
            CurrentFrame = frame;
        }
    }
}

