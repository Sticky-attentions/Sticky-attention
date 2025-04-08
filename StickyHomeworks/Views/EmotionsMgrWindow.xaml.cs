using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StickyHomeworks.Views;

public partial class EmotionsMgrWindow : Window, INotifyPropertyChanged
{
    // 笑脸和情感表情
    public ObservableCollection<string> HumanEmojis { get; } = new ObservableCollection<string>
    {
        "😀", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "😊", "😇",
        "🙂", "🙃", "😉", "😌", "😍", "🥰", "😘", "😗", "😙", "😚",
        "😋", "😛", "😝", "😜", "🤪", "🤨", "🧐", "🤓", "😎", "🤩",
        "🥳", "😏", "😒", "😞", "😔", "😟", "😕", "🙁", "☹️", "😣",
        "😖", "😫", "😩", "🥺", "😢", "😭", "😤", "😠", "😡", "🤬",
        "🤯", "😳", "🥵", "🥶", "😱", "😨", "😰", "😥", "😓", "🤗",
        "🤔", "🤭", "🤫", "🤥", "😶", "😐", "😑", "😬", "🙄", "😯",
        "😦", "😧", "😮", "😲", "🥱", "😴", "🤤", "😪", "😵", "🤐",
        "🥴", "🤢", "🤮", "🤧", "😷", "🤒", "🤕", "🤑", "🤠", "😈",
        "👿", "👹", "👺", "🤡", "💩", "👻", "💀", "☠️", "👽", "👾",
        "🤖", "🎃"
    };

    // 学习和作业表情
    public ObservableCollection<string> SchoolEmojis { get; } = new ObservableCollection<string>
    {
        "📚", "✏️", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝",
        "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝",
        "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝",
        "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝",
        "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝",
        "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝", "📝"
    };

    public string? SelectedEmoji { get; private set; }

    public EmotionsMgrWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void EmojiListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is string emoji)
        {
            SelectedEmoji = emoji;
        }
    }

    private void ButtonOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void ButtonClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}