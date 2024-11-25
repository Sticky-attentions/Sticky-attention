using StickyHomeworks.Core.Context;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StickyHomeworks.Views;

/// <summary>
/// EmotionsMgrWindow.xaml 的交互逻辑
/// </summary>
public partial class EmotionsMgrWindow : Window
{
    public AppDbContext DbContext { get; set; }
    public string SelectedEmoji { get; private set; }

    public EmotionsMgrWindow(AppDbContext dbContext)
    {
        InitializeComponent();
        DataContext = this;
        DbContext = dbContext;
    }

    private void Buttonoff_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}