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
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ScriptEditorWindow : Window
    {
        public static readonly DependencyProperty ScriptTextProperty =
            DependencyProperty.Register(nameof(ScriptText), typeof(string), typeof(ScriptEditorWindow), new PropertyMetadata(string.Empty));

        public string ScriptText
        {
            get => (string)GetValue(ScriptTextProperty);
            set => SetValue(ScriptTextProperty, value);
        }

        public ScriptEditorWindow(string currentScript)
        {
            InitializeComponent();
            ScriptText = currentScript;
            DataContext = this;
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

}
