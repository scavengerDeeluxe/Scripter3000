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

namespace ScriptArcade
{
    /// <summary>
    /// Interaction logic for LogViewer.xaml
    /// </summary>
    public partial class LogViewerWindow : Window
    {
        public static readonly DependencyProperty LogTextProperty =
            DependencyProperty.Register(nameof(LogText), typeof(string), typeof(LogViewerWindow), new PropertyMetadata(string.Empty));

        public string LogText
        {
            get => (string)GetValue(LogTextProperty);
            set => SetValue(LogTextProperty, value);
        }

        public LogViewerWindow(string theseLogs)
        {
            InitializeComponent();
            LogText = theseLogs;
            DataContext = this;
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

}
