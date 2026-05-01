using System.Windows;

namespace LogAnalyzer
{
    public partial class TextileExportDialog : Window
    {
        public string TextileContent { get; set; }

        public TextileExportDialog(string textileContent)
        {
            InitializeComponent();
            TextileContent = textileContent;
            DataContext = this;
            Owner = Application.Current?.MainWindow != this
                ? Application.Current?.MainWindow
                : null;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TextileTextBox.Text);
            MessageBox.Show("Textile format copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
