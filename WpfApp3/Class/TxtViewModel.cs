using System.ComponentModel;
using System.Runtime.CompilerServices;

public class TxtViewModel : INotifyPropertyChanged
{
    private string _sharedText;
    public string SharedText
    {
        get => _sharedText;
        set { _sharedText = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}