using DepRos;
using System.Windows;

namespace DepRosTester;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    [DependencyProperty]
    public virtual partial int @MyProperty { get; set; }

    public MainWindow() {
        InitializeComponent();
    }
}
