using System.Reflection;
using System.Windows;
using PS2Manager.App.Services;

namespace PS2Manager.App.Views.Dialogs;

public partial class AboutWindow : DialogWindow
{
    public const string RepoUrl = "https://github.com/pearlxcore/PS2-Manager";
    public const string KofiUrl = "https://ko-fi.com/pearlxcore";

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "");
    }

    private void OnGitHub(object sender, RoutedEventArgs e) => ShellService.Open(RepoUrl);
    private void OnKofi(object sender, RoutedEventArgs e) => ShellService.Open(KofiUrl);
    private void OnReportBug(object sender, RoutedEventArgs e) => ShellService.Open(RepoUrl + "/issues/new");
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
