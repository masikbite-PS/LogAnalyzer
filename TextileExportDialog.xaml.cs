using System;
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
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetDataObject(TextileContent, true);
                MessageBox.Show("Textile format copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
