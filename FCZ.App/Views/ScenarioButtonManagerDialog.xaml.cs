using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using FCZ.App.Models;
using FCZ.Core.Models;

namespace FCZ.App.Views
{
    public partial class ScenarioButtonManagerDialog : Window
    {
        public ObservableCollection<Scenario?> AssignedScenarios { get; } = new();

        public ScenarioButtonManagerDialog(ObservableCollection<Scenario> availableScenarios, ObservableCollection<ScenarioButton> currentButtons)
        {
            InitializeComponent();
            AvailableScenariosListBox.ItemsSource = availableScenarios;
            ButtonsListView.ItemsSource = currentButtons;

            // Initialize assigned scenarios
            foreach (var button in currentButtons)
            {
                AssignedScenarios.Add(button.AssignedScenario);
            }
        }

        private void AssignButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedScenario = AvailableScenariosListBox.SelectedItem as Scenario;
            var selectedButton = ButtonsListView.SelectedItem as ScenarioButton;

            if (selectedScenario != null && selectedButton != null)
            {
                selectedButton.AssignedScenario = selectedScenario;
                AssignedScenarios[selectedButton.Index] = selectedScenario;
                ButtonsListView.Items.Refresh();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedButton = ButtonsListView.SelectedItem as ScenarioButton;
            if (selectedButton != null)
            {
                selectedButton.AssignedScenario = null;
                AssignedScenarios[selectedButton.Index] = null;
                ButtonsListView.Items.Refresh();
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var button in ButtonsListView.Items.Cast<ScenarioButton>())
            {
                button.AssignedScenario = null;
                AssignedScenarios[button.Index] = null;
            }
            ButtonsListView.Items.Refresh();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}


