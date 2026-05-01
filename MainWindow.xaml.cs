using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LogAnalyzer.Models;
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

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void SipDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is SipMessage msg)
                ((MainViewModel)DataContext).SipViewModel.SelectedMessage = msg;
        }

        private static DataGrid? FindSqlDataGrid(object sender)
        {
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm && cm.PlacementTarget is DataGrid dg)
                return dg;
            return null;
        }

        private static System.Collections.Generic.IEnumerable<SqlDataColumn> SelectedSqlRows(DataGrid dg)
        {
            foreach (var item in dg.SelectedItems)
                if (item is SqlDataColumn c) yield return c;
        }

        private static void CopyToClipboard(string text)
        {
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetDataObject(text, true);
        }

        private void SqlCopyFieldValue_Click(object sender, RoutedEventArgs e)
        {
            var dg = FindSqlDataGrid(sender);
            if (dg == null) return;
            var lines = SelectedSqlRows(dg).Select(c => $"{c.Name} = {c.DisplayValue}");
            CopyToClipboard(string.Join(System.Environment.NewLine, lines));
        }

        private void SqlCopyField_Click(object sender, RoutedEventArgs e)
        {
            var dg = FindSqlDataGrid(sender);
            if (dg == null) return;
            var lines = SelectedSqlRows(dg).Select(c => c.Name);
            CopyToClipboard(string.Join(System.Environment.NewLine, lines));
        }

        private void SqlCopyValue_Click(object sender, RoutedEventArgs e)
        {
            var dg = FindSqlDataGrid(sender);
            if (dg == null) return;
            var lines = SelectedSqlRows(dg).Select(c => c.DisplayValue);
            CopyToClipboard(string.Join(System.Environment.NewLine, lines));
        }
    }
}