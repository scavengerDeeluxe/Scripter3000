using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Windows;
using System.Windows.Media;
using System.Xml;
namespace ScriptArcade
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    /// 
    public partial class ScriptEditorWindow : Window
    {
        public string ScriptText { get; private set; }

        public ScriptEditorWindow(string currentScript)
        {
            InitializeComponent();
            LoadMonokaiSodaTheme();
            editorPopout.Text = currentScript;


        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            ScriptText = editorPopout.Text;
            this.Close();
        }
        private void LoadMonokaiSodaTheme()
        {
            using (var reader = new XmlTextReader("Resources/MonokaiSoda.xshd"))
            {
                var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                editorPopout.SyntaxHighlighting = definition;


                editorPopout.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#272822"));
                editorPopout.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8f8f2"));
                editorPopout.TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BD93F9"));
                editorPopout.TextArea.TextView.LinkTextUnderline = false;
            }
        }
    }
}
