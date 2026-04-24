using System.Windows;
using LogAnalyzer.ViewModels;

namespace LogAnalyzer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainViewModel();
            DataContext = viewModel;
        }
    }
}