using System.Windows;
using FCZ.App.ViewModels;
using FCZ.Core.Configuration;
using FCZ.Core.Engine;
using FCZ.Core.Services;
using FCZ.Vision;

namespace FCZ.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Initialize services
            var windowManager = new WindowManager();
            var processWatcher = new ProcessWatcher(windowManager);
            var captureService = new CaptureService();
            var inputService = new InputService();
            var templateStore = new TemplateStore(AppConfig.TemplatesPath);
            var imageMatcher = new ImageMatcher();
            var ruleEngine = new RuleEngine(imageMatcher, inputService, templateStore);
            var gameLauncher = new GameLauncher(windowManager, inputService);

            // Create and set ViewModel
            var viewModel = new MainViewModel(
                windowManager,
                processWatcher,
                captureService,
                ruleEngine,
                templateStore,
                inputService,
                imageMatcher,
                gameLauncher);

            DataContext = viewModel;
        }
    }
}
