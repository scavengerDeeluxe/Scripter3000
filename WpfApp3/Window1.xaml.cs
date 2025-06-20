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
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ScriptEditorWindow : Window
    {
        public string ScriptText { get; private set; }

        public ScriptEditorWindow(string currentScript)
        {
            InitializeComponent();
            editorPopout.Text = currentScript;
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            ScriptText = editorPopout.Text;
            this.Close();
        }
    }

}
