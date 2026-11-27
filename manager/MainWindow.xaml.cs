using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ValheimVRM.Manager
{
    public partial class MainWindow : Window
    {
        private const string Preferences = @"Software\ValheimVRM\Manager";
        private readonly DispatcherTimer pathDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        private readonly Dictionary<TargetKind, string> draftPaths = new Dictionary<TargetKind, string>();
        private TargetKind target;
        private InstallationState state;
        private string source;
        private int pathRevision;
        private bool ready;
        private bool changingPath;
        private bool busy;
        private double clientHeight;

        public MainWindow()
        {
            InitializeComponent();
            clientHeight = Height;
            pathDelay.Tick += async (sender, args) => await InspectPath();
        }

        private async void WindowLoaded(object sender, RoutedEventArgs args)
        {
            if (ready)
                return;
            ready = true;
            await RestorePath();
        }

        private void WindowClosing(object sender, CancelEventArgs args)
        {
            args.Cancel = busy;
        }

        private async void TargetChanged(object sender, RoutedEventArgs args)
        {
            if (!ready || busy)
                return;

            draftPaths[target] = InstallPath.Text;
            target = DedicatedTab.IsChecked == true ? TargetKind.DedicatedServer : TargetKind.Client;
            bool isClient = target == TargetKind.Client;
            if (!isClient)
                clientHeight = Height;
            MinHeight = isClient ? 535 : 340;
            Height = isClient ? clientHeight : 340;
            ClientComponent.Visibility = Visible(isClient);
            ModelsPanel.Visibility = Visible(isClient);
            ShowStatus("");
            await RestorePath();
        }

        private async Task RestorePath()
        {
            source = null;
            SetPath(draftPaths.TryGetValue(target, out var draft) ? draft : ReadPath(target));
            if (string.IsNullOrWhiteSpace(InstallPath.Text))
                await DetectCurrentTarget();
            else
                await InspectPath();
        }

        private void SetPath(string path)
        {
            changingPath = true;
            InstallPath.Text = path ?? "";
            changingPath = false;
        }

        private void PathEdited(object sender, TextChangedEventArgs args)
        {
            if (!ready || changingPath)
                return;

            pathRevision++;
            source = null;
            state = null;
            PathHint.Text = "正在检查目录…";
            ShowStatus("");
            UpdateControls();
            pathDelay.Stop();
            pathDelay.Start();
        }

        private async void PathKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                await InspectPath();
            }
        }

        private async void BrowsePath(object sender, RoutedEventArgs args)
        {
            using (var dialog = new Forms.FolderBrowserDialog
            {
                Description = "选择 Valheim 的安装目录",
                SelectedPath = state?.Game.Root ?? "",
                ShowNewFolderButton = false
            })
            {
                var owner = new DialogOwner(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                if (dialog.ShowDialog(owner) != Forms.DialogResult.OK)
                    return;

                source = null;
                SetPath(dialog.SelectedPath);
                ShowStatus("");
                await InspectPath();
            }
        }

        private async void DetectPath(object sender, RoutedEventArgs args) => await DetectCurrentTarget();

        private async Task DetectCurrentTarget()
        {
            pathDelay.Stop();
            int revision = ++pathRevision;
            var selection = target;
            state = null;
            PathHint.Text = "正在查找 Steam 安装目录…";
            ShowStatus("");
            UpdateControls();
            try
            {
                var game = await Task.Run(() => SteamInstallations.Find(selection));
                if (revision != pathRevision)
                    return;
                if (game != null)
                {
                    source = "已通过 Steam 检测";
                    SetPath(game.Root);
                }
                await InspectPath(game == null);
            }
            catch (Exception error) when (IsFileError(error))
            {
                if (revision == pathRevision)
                {
                    PathHint.Text = DirectoryHint;
                    ShowStatus(error.Message, true);
                }
            }
        }

        private string DirectoryHint => target == TargetKind.Client
            ? "输入包含 valheim.exe 的目录" : "输入包含 valheim_server.exe 的目录";

        private async Task InspectPath(bool detectionMiss = false)
        {
            pathDelay.Stop();
            int revision = ++pathRevision;
            var value = InstallPath.Text;
            var selection = target;
            state = null;
            PathHint.Text = "正在检查目录…";
            UpdateControls();
            try
            {
                var snapshot = await Task.Run(() => InstallationState.Read(selection, value));
                if (revision != pathRevision)
                    return;

                state = snapshot;
                PathHint.Text = snapshot == null
                    ? (detectionMiss ? "未自动找到安装。" : "") + DirectoryHint
                    : source ?? "已识别安装目录";
                if (snapshot != null)
                    RememberPath(snapshot.Game);
            }
            catch (Exception error) when (IsFileError(error))
            {
                if (revision == pathRevision)
                {
                    PathHint.Text = "无法读取此安装目录";
                    ShowStatus(error.Message, true);
                }
            }
            if (revision == pathRevision)
                UpdateControls();
        }

        private void UpdateControls()
        {
            TargetTabs.IsEnabled = !busy;
            InstallPath.IsEnabled = !busy;
            BrowseButton.IsEnabled = !busy;
            DetectButton.IsEnabled = !busy;
            LoaderStatus.Text = state?.LoaderStatus ?? "";
            UpdateComponent(ModComponent.Client, state?.Client, ClientStatus, ClientInstall, ClientUninstall);
            UpdateComponent(ModComponent.Server, state?.Server, ServerStatus, ServerInstall, ServerUninstall);

            var models = state?.Models ?? Array.Empty<AvatarFile>();
            bool installed = state?.Client?.Installed == true;
            AddModel.IsEnabled = !busy && installed;
            ModelList.IsEnabled = !busy;
            ModelList.ItemsSource = models;
            ModelCount.Text = installed ? $"{models.Count} 个" : "";
            ModelEmpty.Visibility = Visible(models.Count == 0);
            ModelEmpty.Text = installed ? "暂无模型" : "安装客户端模组后可管理模型";
        }

        private void UpdateComponent(ModComponent component, ComponentState info, TextBlock label, Button install, Button uninstall)
        {
            bool present = info?.Installed == true;
            bool bundled = BundledPackages.Contains(component);
            label.Text = info?.Description ?? "等待安装目录";
            if (!bundled && info != null)
                label.Text += " · 缺少安装包";
            label.Foreground = (Brush)FindResource(present ? "Accent" : "Muted");
            install.Content = present ? "重新安装" : "安装";
            install.Style = (Style)FindResource(present ? typeof(Button) : (object)"Primary");
            install.IsEnabled = !busy && info != null && bundled;
            uninstall.Visibility = Visible(present);
            uninstall.IsEnabled = !busy && present;
        }

        private async void InstallClient(object sender, RoutedEventArgs args) => await Install(ModComponent.Client);
        private async void InstallServer(object sender, RoutedEventArgs args) => await Install(ModComponent.Server);
        private async void UninstallClient(object sender, RoutedEventArgs args) => await Uninstall(ModComponent.Client);
        private async void UninstallServer(object sender, RoutedEventArgs args) => await Uninstall(ModComponent.Server);

        private Task Install(ModComponent component)
        {
            return RunOperation("正在安装…", game =>
            {
                ModInstaller.Install(game, component);
                return "安装完成。";
            });
        }

        private Task Uninstall(ModComponent component)
        {
            var name = component == ModComponent.Client ? "客户端模组" : "联机同步服务端";
            if (!Confirm($"卸载{name}？"))
                return Task.CompletedTask;
            return RunOperation("正在卸载…", game =>
            {
                ModInstaller.Uninstall(game, component);
                return "卸载完成。";
            });
        }

        private async void PickModels(object sender, RoutedEventArgs args)
        {
            var picker = new OpenFileDialog { Title = "添加模型", Filter = "VRM 模型 (*.vrm)|*.vrm", Multiselect = true };
            if (picker.ShowDialog(this) == true)
                await ImportModels(picker.FileNames);
        }

        private void FilesDragged(object sender, DragEventArgs args)
        {
            bool accepted = AddModel.IsEnabled && args.Data.GetData(DataFormats.FileDrop) is string[] paths
                && paths.Any(AvatarFiles.IsModel);
            args.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
            DropArea.BorderBrush = (Brush)FindResource(accepted ? "Accent" : "Line");
            args.Handled = true;
        }

        private void FilesLeft(object sender, DragEventArgs args)
        {
            DropArea.BorderBrush = (Brush)FindResource("Line");
        }

        private async void FilesDropped(object sender, DragEventArgs args)
        {
            FilesLeft(sender, args);
            args.Handled = true;
            if (AddModel.IsEnabled && args.Data.GetData(DataFormats.FileDrop) is string[] paths)
                await ImportModels(paths);
        }

        private Task ImportModels(string[] files)
        {
            return RunOperation("正在添加模型…", game => AvatarFiles.Import(game, files));
        }

        private async void DeleteModel(object sender, RoutedEventArgs args)
        {
            if (((Button)sender).DataContext is AvatarFile model && Confirm($"删除「{model.Name}」？"))
            {
                await RunOperation("正在删除模型…", game =>
                {
                    AvatarFiles.Delete(game, model);
                    return "模型已删除。";
                });
            }
        }

        private async Task RunOperation(string message, Func<GameInstallation, string> action)
        {
            if (busy || state == null)
                return;
            var game = state.Game;
            busy = true;
            UpdateControls();
            ShowStatus(message);
            try
            {
                ShowStatus(await Task.Run(() => action(game)));
            }
            catch (Exception error) when (IsFileError(error))
            {
                ShowStatus(error.Message, true);
            }
            finally
            {
                await InspectPath();
                busy = false;
                UpdateControls();
            }
        }

        private bool Confirm(string message)
        {
            return MessageBox.Show(this, message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        private void ShowStatus(string message, bool error = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = (Brush)FindResource(error ? "Error" : "Muted");
        }

        private static bool IsFileError(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException || error is SecurityException ||
                error is InvalidDataException || error is ArgumentException || error is NotSupportedException;
        }

        private static Visibility Visible(bool showing) => showing ? Visibility.Visible : Visibility.Collapsed;

        private static string ReadPath(TargetKind kind)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Preferences))
                    return key?.GetValue(kind.ToString()) as string;
            }
            catch (Exception error) when (IsFileError(error)) { return null; }
        }

        private static void RememberPath(GameInstallation game)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(Preferences))
                    key?.SetValue(game.Kind.ToString(), game.Root);
            }
            catch (Exception error) when (IsFileError(error)) { }
        }

        private sealed class DialogOwner : Forms.IWin32Window
        {
            public IntPtr Handle { get; }
            internal DialogOwner(IntPtr handle) { Handle = handle; }
        }
    }
}
