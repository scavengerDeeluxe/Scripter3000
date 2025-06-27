using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;

public class DGViewModel : INotifyPropertyChanged
{
    private string _sharedText;
    public string SharedText
    {
        get => _sharedText;
        set
        {
            if (_sharedText != value)
            {
                _sharedText = value;
                OnPropertyChanged();
                UpdateTableFromText();
            }
        }
    }

    private DataView _tableView;
    public DataView TableView
    {
        get => _tableView;
        private set { _tableView = value; OnPropertyChanged(); }
    }

    private void UpdateTableFromText()
    {
        if (string.IsNullOrWhiteSpace(_sharedText))
        {
            TableView = null;
            return;
        }

        var table = new DataTable();
        var lines = _sharedText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            TableView = null;
            return;
        }

        var headers = lines[0].Split('\t'); // Adjust delimiter as needed
        foreach (var header in headers)
            table.Columns.Add(header);

        for (int i = 1; i < lines.Length; i++)
        {
            var row = table.NewRow();
            var cells = lines[i].Split('\t');
            for (int j = 0; j < headers.Length && j < cells.Length; j++)
                row[j] = cells[j];
            table.Rows.Add(row);
        }

        TableView = table.DefaultView;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}