using System.Windows;

namespace ScriptArcade
{
    public partial class DataGridViewerWindow : Window
    {
        public object GridData
        {
            get => gridPopout.ItemsSource;
            set => gridPopout.ItemsSource = value;
        }

        public DataGridViewerWindow(object data)
        {
            InitializeComponent();
            GridData = data;
            DataContext = this;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
