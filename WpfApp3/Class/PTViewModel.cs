using System.ComponentModel;
using System.Runtime.CompilerServices;

public class TextViewModel : INotifyPropertyChanged
{
    private string _sharedPlainText;
    public string SharedPlainText
    {
        get => _sharedPlainText;
        set { _sharedPlainText = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}