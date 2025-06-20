using System.ComponentModel;
using System.IO;
using System.Threading;


namespace WpfApp3
{
public partial class MainWindow
    {
        public class RemoteJob : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public string Script { get; set; }
            public string ScriptName { get; set; }
            public string TargetPC { get; set; }
            public string remotePath { get; set; }
            private string _status;
            public string logPath { get; set; }
            public string Status
            {
                get => _status;
                set { _status = value; OnPropertyChanged(nameof(Status)); }
            }
            public string DateTime { get; set; }
            private string _log;
            public string Log
            {
                get => _log;
                set { _log = value; OnPropertyChanged(nameof(Log)); }
            }

            public int ProcessId { get; set; }
            public CancellationTokenSource Cancellation { get; set; }
            public FileSystemWatcher Watcher { get; set; }
            public string LogPath { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}