using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfApp3
{
    /// <summary>
    /// Interaction logic for LogViewer.xaml
    /// </summary>
    public partial class LogViewerWindow : Window
    {
        public string logText { get; private set; }

        public LogViewerWindow(string theseLogs)
        {
            InitializeComponent();
            ViewerPopout.Text = theseLogs;
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            logText = ViewerPopout.Text;
            DialogResult = true;
            Close();
        }
    }

}
