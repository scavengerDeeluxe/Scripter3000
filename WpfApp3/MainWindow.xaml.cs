using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Newtonsoft.Json; // Add at the top if not present
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Management.Automation;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml;
using MessageBox = System.Windows.MessageBox;

namespace ScriptArcade
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        //     private ObservableCollection<RemoteJob> Jobs = new ObservableCollection<RemoteJob>();
        private JobManager jobManager;

        public MainWindow()
        {
            InitializeComponent();
            LoadPowerShellHighlighting();
            string path = @"D:\\Scripts\\System";
            _allItems = LoadPs1DirectoryTree(path);
            treeViewScripts.ItemsSource = _allItems;

            jobManager = new JobManager();
            lsv_jobs.ItemsSource = jobManager.Jobs;

            // Example usage of AddJob

        }
        public ObservableCollection<FileSystemItem> LoadPs1DirectoryTree(string rootPath)
        {
            var items = new ObservableCollection<FileSystemItem>();

            try
            {
                foreach (var dir in Directory.GetDirectories(rootPath))
                {
                    var dirItem = new FileSystemItem
                    {
                        Name = System.IO.Path.GetFileName(dir),
                        FullPath = dir,
                        IsDirectory = true
                    };

                    // Add .ps1 children from this directory
                    //         foreach (var ps1File in Directory.GetFiles(dir, "*.ps1"))
                    //           {
                    //             dirItem.Children.Add(new FileSystemItem
                    //             {
                    //                 Name = System.IO.Path.GetFileName(ps1File),
                    //                  FullPath = ps1File,
                    //                 IsDirectory = false
                    //              });
                    //        }

                    // Recurse into subdirectories
                    var childDirs = LoadPs1DirectoryTree(dir);
                    foreach (var child in childDirs)
                        dirItem.Children.Add(child);

                    items.Add(dirItem);
                }

                // Also add any ps1 files directly under the root path
                foreach (var ps1File in Directory.GetFiles(rootPath, "*.ps1"))
                {
                    items.Add(new FileSystemItem
                    {
                        Name = Path.GetFileName(ps1File),
                        FullPath = ps1File,
                        IsDirectory = false
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading directory: {ex.Message}");
            }

            return items;
        }
        private void LoadPowerShellHighlighting()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "PowerShell.xshd");
            using (var stream = File.OpenRead(path))
            using (var reader = XmlReader.Create(stream))
            {
                var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                editorScript.SyntaxHighlighting = definition;
            }
        }
        private void treeViewScripts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (treeViewScripts.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                try
                {
                    string scriptText = File.ReadAllText(item.FullPath);
                    editorScript.Clear();
                    editorScript.AppendText(scriptText);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to load script: {ex.Message}");
                }
            }
        }
        private void OpenScriptPopout_Click_1(object sender, RoutedEventArgs e)
        {
            var currentScript = editorScript.Text;

            var popup = new ScriptEditorWindow(currentScript);
            popup.Owner = this;
            popup.Show(); // Non-modal: parent window remains interactive


            // If you want to update the main editor when the popout closes, you can handle the Closed event:
            popup.Closed += (s, args) =>
            {
                if (!string.IsNullOrEmpty(popup.ScriptText))
                {
                    editorScript.Text = popup.ScriptText;
                }

            };
        }
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Fix for CS1501: No overload for method 'ClearValue' takes 0 arguments
            // The `ClearValue` method requires a DependencyProperty as an argument. 
            // Assuming `editorLogs` is a WPF control, the correct usage is to pass the appropriate DependencyProperty.
            editorLogs.ClearValue(System.Windows.Controls.TextBox.TextProperty);
            string pcTarget = txt_Target.Text;

            var scriptText = editorScript.Text.Trim();

            if (string.IsNullOrWhiteSpace(scriptText))
            {
                System.Windows.MessageBox.Show("No script to run.");
                return;
            }

            SaveHistory(scriptText);
            string thisDate = DateTime.Now.ToString("yyyyMMddHHmmss");
            string remoteFileName = "temppowershellrunner" + thisDate + ".ps1";

            var job = new RemoteJob
            {
                Name = remoteFileName,
                Script = scriptText,
                ScriptName = ((FileSystemItem)treeViewScripts.SelectedItem).Name,
                Status = "Starting",
                Cancellation = new CancellationTokenSource(),
                DateTime = thisDate,
                TargetPC = pcTarget,
                LogPath = $@"\\{pcTarget}\C$\windows\temp\pslogs{thisDate}.log",
                remotePath = $@"\\{pcTarget}\C$\windows\temp\temppowershellrunner{thisDate}.ps1"
            };
            //  Jobs.Add(job);
            jobManager.AddJob(job);

            await Task.Run(() =>
            {
                try
                {
                    string remotePath = $@"\\{job.TargetPC}\C$\windows\temp\{job.Name}";
                    string logPath = $@"\\{job.TargetPC}\C$\windows\temp\pslogs{job.DateTime}.log";

                    job.LogPath = logPath;

                    if (!CopyScriptToRemote(job))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            job.Status = "Error copying script";
                            editorLogs.AppendText($"❌ Error copying script to {pcTarget}\n");
                        });
                        return;
                    }

                    RunRemoteScript(job);

                    job.Status = "Running";

                    _ = Task.Run(() => MonitorLogFile(job));

                    while (!job.Cancellation.Token.IsCancellationRequested && IsRemoteProcessRunning(job))
                    {
                        Thread.Sleep(500);
                    }

                    job.Status = job.Cancellation.Token.IsCancellationRequested ? "Cancelled" : "Completed";
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        job.Status = "Error: " + ex.Message;
                        editorLogs.AppendText($"❌ Error running script on {pcTarget}: {ex.Message}\n");
                    });
                }
            });
        }
        bool IsStructuredData(string text, out DataTable dataTable)
        {
            {
                dataTable = new DataTable();

                if (string.IsNullOrWhiteSpace(text))
                    return false;

                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                    return false; // Must have at least header + data

                string[] delimiterCandidates = { "\t", ",", "|", ";" };
                string detectedDelimiter = null;

                foreach (var delimiter in delimiterCandidates)
                {
                    var firstLineParts = lines[0].Split(new[] { delimiter }, StringSplitOptions.None);
                    if (firstLineParts.Length > 1)
                    {
                        detectedDelimiter = delimiter;
                        break;
                    }
                }

                // Fallback: try splitting on multiple spaces
                if (detectedDelimiter == null)
                {
                    detectedDelimiter = "  ";
                }

                var columnCount = lines[0].Split(new[] { detectedDelimiter }, StringSplitOptions.RemoveEmptyEntries).Length;

                foreach (var line in lines)
                {
                    var columns = line.Split(new[] { detectedDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length != columnCount)
                        return false; // Inconsistent row lengths → not structured
                }

                // Create columns
                for (int i = 0; i < columnCount; i++)
                {
                    dataTable.Columns.Add($"Column{i + 1}");
                }

                // Populate rows
                foreach (var line in lines)
                {
                    var cells = line.Split(new[] { detectedDelimiter }, StringSplitOptions.None);
                    dataTable.Rows.Add(cells);
                }

                return true;
            }
        }
        private void OpenLogsPopou(object sender, RoutedEventArgs e)
        {
            if (dg_logs.ItemsSource != null && dg_logs.Visibility == Visibility.Visible)
            {
                var gridPopup = new DataGridViewerWindow(dg_logs.ItemsSource);
                gridPopup.Owner = this;
                gridPopup.Show();
                return;
            }

            var theseLogs = new System.Windows.Documents.TextRange(editorLogs.Document.ContentStart, editorLogs.Document.ContentEnd).Text;
            var popupViewer = new LogViewerWindow(theseLogs);
            bool? result = popupViewer.ShowDialog();

            if (result == true)
            {
                editorLogs.Document.Blocks.Clear();
                editorLogs.AppendText(popupViewer.LogText);
            }
        }

        private void OpenWmiResultsPopout_Click(object sender, RoutedEventArgs e)
        {
            if (dgWmiResults.ItemsSource != null)
            {
                var gridPopup = new DataGridViewerWindow(dgWmiResults.ItemsSource);
                gridPopup.Owner = this;
                gridPopup.Show();
            }
            else
            {
                MessageBox.Show("No results to display.");
            }
        }
        private void lb_jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lsv_jobs.SelectedItem is RemoteJob job)
            {
                DisplayJobLog(job);
            }
        }
        private void CancelJob_Click(object sender, RoutedEventArgs e)
        {
            if (lsv_jobs.SelectedItem is RemoteJob job)
            {
                job.Cancellation?.Cancel();
            }
        }
        private void SaveHistory(string script)
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScriptArcade");
                Directory.CreateDirectory(dir);
                var file = System.IO.Path.Combine(dir, "history.log");
                File.AppendAllText(file, $"{DateTime.Now:u} - {script}{Environment.NewLine}");
            }
            catch { }
        }
        private void SaveScript_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "PowerShell script|*.ps1|All Files|*.*";
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, editorScript.Text);
            }
        }
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            string target = txt_Target.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                System.Windows.MessageBox.Show("Enter a target computer name first.");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    using (var ping = new System.Net.NetworkInformation.Ping())
                    {
                        var reply = ping.Send(target, 2000);
                        Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(reply.Status == System.Net.NetworkInformation.IPStatus.Success ?
                                $"Ping to {target} successful" : $"Ping to {target} failed: {reply.Status}");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"Ping failed: {ex.Message}"));
                }
            });
        }
        private void RunRemoteScript(RemoteJob job)
        {
            Console.WriteLine("Running remote script on: " + job.TargetPC);
            string filename = "C:\\windows\\temp\\temppowershellrunner" + job.DateTime + ".ps1";
            string uncScriptPath = $"C:\\Windows\\Temp\\temppowershellrunner{job.DateTime}.ps1";
            string uncLogPath = $"C:\\Windows\\Temp\\pslogs{job.DateTime}.log";

            string command = $"cmd /c powershell.exe -ExecutionPolicy Bypass -NoProfile -File \"{uncScriptPath}\" > \"{uncLogPath}\" 2>&1";
            //string command = "powershell -ExecutionPolicy Bypass -NoProfile -File \"C:\\windows\\temp\\temppowershellrunner" + thisDate + ".ps1\" *>&1 | tee-object -FilePath \"C:\\windows\\temp\\" + thisDate + ".log\"";
            //   string command = "cmd /c powershell.exe -ExecutionPolicy Bypass -NoProfile -File " + job.remotePath + " *> " + job.logPath + " 2>&1";
            Console.WriteLine("Command to run: " + command);

            ConnectionOptions options = new ConnectionOptions();
            options.EnablePrivileges = true;

            var scope = new ManagementScope($"\\\\{job.TargetPC}\\root\\cimv2", options);

            try
            {
                Console.WriteLine("Connecting to WMI on " + job.TargetPC);
                scope.Connect();

                ManagementClass processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);

                ManagementBaseObject inParams = processClass.GetMethodParameters("Create");
                inParams["CommandLine"] = command;

                ManagementBaseObject outParams = processClass.InvokeMethod("Create", inParams, null);
                int processId = Convert.ToInt32(outParams["ProcessId"]);
                job.ProcessId = processId;
                Console.WriteLine("ReturnValue: " + outParams["ReturnValue"]);
                Console.WriteLine("ProcessId: " + outParams["ProcessId"]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("WMI operation failed: " + ex.Message);
            }
        }
        private bool CopyScriptToRemote(RemoteJob job)
        {
            try
            {
                var tempFile = Path.GetTempFileName();

                string logpath = job.LogPath;
                File.WriteAllText(tempFile, job.Script);
                File.Copy(tempFile, job.remotePath, true);
                File.Delete(tempFile);
                return true;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"Failed to copy script to {job.TargetPC}: {ex.Message}");
                });
                return false;
            }
        }
        private bool IsRemoteProcessRunning(RemoteJob job)
        {
            string LogPath_ = job.LogPath.Split('\\')[2];
            try
            {
                var scope = new ManagementScope($"\\\\{job.TargetPC}\\root\\cimv2");
                scope.Connect();
                var query = new ObjectQuery($"SELECT * FROM Win32_Process WHERE ProcessId = {job.ProcessId}");
                using (var searcher = new ManagementObjectSearcher(scope, query))
                using (var results = searcher.Get())
                {
                    return results.Count > 0;
                }
            }
            catch
            {
                return false;
            }
        }
        private void ListBoxItem_Selected(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txt_Target.Text))
            {
                System.Windows.MessageBox.Show("Target computer name is required.");
                return;
            }

            try
            {
                string args = $"/computer={txt_Target.Text}";
                Process.Start("compmgmt.msc", args);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to launch Computer Management for {txt_Target.Text}: {ex.Message}");
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            jobManager.Dispose();
            base.OnClosed(e);
        }
        private void ListBoxItem_Selected_1(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txt_Target.Text))
            {
                System.Windows.MessageBox.Show("Target computer name is required.");
                return;
            }

            try
            {
                string args = $"/computer={txt_Target.Text}";
                Process.Start("eventvwr.msc", args);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to launch Computer Management for {txt_Target.Text}: {ex.Message}");
            }
        }
        private void bt_Connect_Click(object sender, RoutedEventArgs e)
        { }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }
        private void lsv_jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lsv_jobs.SelectedItem is RemoteJob selectedJob)
            {
                DisplayJobLog(selectedJob);
            }
        }
        private void LoadTableTextToWpfGrid(System.Windows.Controls.DataGrid grid, string rawText)
        {
            var table = ParseFormattedTable(rawText);

            // Convert to a list of dictionaries for dynamic binding
            var items = new List<Dictionary<string, string>>();
            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, string>();
                foreach (DataColumn col in table.Columns)
                {
                    dict[col.ColumnName] = row[col].ToString();
                }
                items.Add(dict);
            }

            grid.ItemsSource = items;
        }
        private DataTable ParseFormattedTable(string rawText)
        {
            var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return new DataTable();

            // Assume header is first line, divider (---) is second, then data rows
            var headers = Regex.Split(lines[0].Trim(), "\\s{2,}");
            var table = new DataTable();
            foreach (var header in headers)
                table.Columns.Add(header);

            for (int i = 2; i < lines.Length; i++)
            {
                var rowParts = Regex.Split(lines[i].Trim(), "\\s{2,}");
                var row = table.NewRow();

                for (int j = 0; j < headers.Length && j < rowParts.Length; j++)
                    row[j] = rowParts[j];

                table.Rows.Add(row);
            }

            return table;
        }

        private void DisplayJobLog(RemoteJob job)
        {
            if (IsStructuredData(job.Log, out var tableData))
            {
                dg_logs.ItemsSource = tableData.DefaultView;
                dg_logs.Visibility = Visibility.Visible;
                editorLogs.Visibility = Visibility.Collapsed;
            }
            else
            {
                dg_logs.ItemsSource = null;
                dg_logs.Visibility = Visibility.Collapsed;
                editorLogs.Visibility = Visibility.Visible;
                editorLogs.Document.Blocks.Clear();
                editorLogs.AppendText(job.Log ?? string.Empty);
            }
        }

        private void MonitorLogFile(RemoteJob job)
        {
            string logPath = job.LogPath;
            string last = string.Empty;

            while (!job.Cancellation.Token.IsCancellationRequested && job.Status == "Running")
            {
                try
                {
                    if (File.Exists(logPath))
                    {
                        var text = File.ReadAllText(logPath);
                        if (text != last)
                        {
                            last = text;
                            Dispatcher.Invoke(() =>
                            {
                                job.Log = text;
                                if (lsv_jobs.SelectedItem == job)
                                {
                                    DisplayJobLog(job);
                                }
                            });
                        }
                    }
                }
                catch { }

                Thread.Sleep(1000);
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeAdminTools();
            LoadWmiNamespaces();
            LoadQueriesFromJson("D:\\Scripts\\WmiQueries.json");
        }

        private List<WmiQuery> savedQueries = new List<WmiQuery>();

        private void LoadQueriesFromJson(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                savedQueries = JsonConvert.DeserializeObject<List<WmiQuery>>(json);
                lb_savedQueries.ItemsSource = savedQueries;
                lb_savedQueries.DisplayMemberPath = "Name";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load queries: {ex.Message}");
            }
        }
private IEnumerable<string> GetTargets()
    {
        var entries = txtWmiTargets.Text
            .Split(new[] { '\n', '\r', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            if (entry.Contains("/"))
            {
                foreach (var ip in ExpandCidr(entry))
                    yield return ip;
            }
            else
            {
                yield return entry;
            }
        }
    }


    private void InitializeAdminTools()
        {
            var tools = new ObservableCollection<AdminTools>
    {
        new AdminTools { Name = "🖥 CompMgmt", executeTarget = "compmgmt.msc", tool = "Computer Management" },
        new AdminTools { Name = "🗒 EventVwr", executeTarget = "eventvwr.msc", tool = "Event Viewer" },
        new AdminTools { Name = "📅 TaskSched", executeTarget = "taskschd.msc", tool = "Task Scheduler" },
        new AdminTools { Name = "⚙ Services", executeTarget = "services.msc", tool = "Services" },
        new AdminTools { Name = "📈 PerfMon", executeTarget = "perfmon.msc", tool = "Performance Monitor" },
        new AdminTools { Name = "🖨 PrintMgmt", executeTarget = "printmanagement.msc", tool = "Print Management" }

    };

            lsv_adminTools.ItemsSource = tools;
        }
        private void lsv_adminTools_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lsv_adminTools.SelectedItem is AdminTools tool && !string.IsNullOrWhiteSpace(tool.executeTarget))
            {
                string target = txt_Target.Text.Trim();

                if (string.IsNullOrWhiteSpace(target))
                {
                    System.Windows.MessageBox.Show("Please enter a target computer name.");
                    return;
                }

                try
                {
                    string args = tool.executeTarget + " /computer:" + target;
                    Process.Start("C:\\windows\\system32\\mmc.exe", args);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to launch {tool.tool} for {target}: {ex.Message}");
                }
            }
        }
        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            var fontsize = new List<string> { "8", "9", "10", "11", "12", "14", "16", "18", "20", "22", "24", "26", "28", "30", "32" };
        }
        private void combo_fontsize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
        private void editorScript_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            editorScript.Width = 1200;
            editorScript.Height = 1200;

        }
        private void editorScript_LostFocus(object sender, RoutedEventArgs e)
        {
            editorScript.Width = 392;
            editorScript.Height = 443;


        }
        private void MultiRunner_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
        private readonly string QueriesDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScriptArcade", "WmiQueries");

        private void LoadWmiNamespaces()
        {
            var namespaces = new List<string> { "root\\cimv2", "root\\wmi", "root\\default", "root\\ccm", "root\\SMS" };
            cb_WmiNamespace.ItemsSource = namespaces;
            if (namespaces.Count > 0)
                cb_WmiNamespace.SelectedIndex = 0;
        }

        private void cb_WmiNamespace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cb_WmiNamespace.SelectedItem == null) return;

            string ns = cb_WmiNamespace.SelectedItem.ToString();
            string target = GetTargetz().FirstOrDefault() ?? "localhost";
            try
            {
                var scope = new ManagementScope($"\\\\{target}\\{ns}");
                scope.Connect();
                var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("select * from meta_class"));
                var classes = new List<string>();
                foreach (ManagementClass c in searcher.Get())
                {
                    string name = c["__CLASS"].ToString();
                    if (!name.StartsWith("__"))
                        classes.Add(name);
                }
                classes.Sort();
                cb_WmiClass.ItemsSource = classes;
            }
catch (Exception ex)
{
    System.Windows.MessageBox.Show($"Failed to load WMI classes: {ex.Message}");
}        }


        private async void cb_WmiClass_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lb_WmiProperties.ItemsSource = null;
            if (cb_WmiClass.SelectedItem == null || cb_WmiNamespace.SelectedItem == null) return;
            string ns = cb_WmiNamespace.SelectedItem.ToString();
            string cls = cb_WmiClass.SelectedItem.ToString();

            try
            {
                var props = await Task.Run(() =>
                {
                    var scope = new ManagementScope($@"\\localhost\{ns}");
                    scope.Connect();

                    var query = new ObjectQuery($"SELECT * FROM meta_class WHERE __CLASS = '{cls}'");
                    var searcher = new ManagementObjectSearcher(scope, query);
                    var result = searcher.Get().Cast<ManagementClass>().FirstOrDefault();
                    if (result == null) return new List<string>();
                    return result.Properties.Cast<PropertyData>().Select(p => p.Name).ToList();
                });

                lb_WmiProperties.ItemsSource = props;
            }
            catch
            {
                // Optionally handle or log errors here
            }
        }

        private void btnBuildWmiQuery_Click(object sender, RoutedEventArgs e)
        {
            if (cb_WmiClass.SelectedItem == null) return;
            var selectedProps = lb_WmiProperties.SelectedItems.Cast<string>().ToList();
            string propPart = selectedProps.Count > 0 ? string.Join(",", selectedProps) : "*";
            txt_wmiQuery.Text = $"SELECT {propPart} FROM {cb_WmiClass.SelectedItem}";
        }

        private IEnumerable<string> GetTargetz()
        {
            return txtWmiTargets.Text.Split(new[] { '\n', '\r', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private async void btnRunWmiQuery_Click(object sender, RoutedEventArgs e)
        {
            string query = txt_wmiQuery.Text.Trim();
            if (string.IsNullOrWhiteSpace(query) || cb_WmiNamespace.SelectedItem == null) return;
            string ns = cb_WmiNamespace.SelectedItem.ToString();

            var rawTargets = txtWmiTargets.Text
                .Split(new[] { '\n', '\r', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList();

            var resolvedTargets = new ConcurrentBag<string>();

            // Expand subnets and ping hosts
            await Task.WhenAll(rawTargets.Select(async t =>
            {
                if (IsSubnet(t))
                {
                    foreach (var ip in ExpandCidr(t))
                    {
                        if (await PingHost(ip))
                            resolvedTargets.Add(ip);
                    }
                }
                else
                {
                    if (await PingHost(t))
                        resolvedTargets.Add(t);
                }
            }));

            if (resolvedTargets.Count == 0)
            {
                MessageBox.Show("No online targets found.");
                return;
            }

            var resultsTable = new DataTable();
            var resultsLock = new object();
            bool columnsCreated = false;

            var tasks = resolvedTargets.Select(target => Task.Run(() =>
            {
                try
                {
                    var scope = new ManagementScope($"\\\\{target}\\{ns}");
                    scope.Connect();
                    var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
                    var collection = searcher.Get();

                    lock (resultsLock)
                    {
                        foreach (ManagementObject obj in collection)
                        {
                            if (!columnsCreated)
                            {
                                foreach (PropertyData p in obj.Properties)
                                    resultsTable.Columns.Add(p.Name);
                                resultsTable.Columns.Add("Target");
                                columnsCreated = true;
                            }

                            var row = resultsTable.NewRow();
                            foreach (PropertyData p in obj.Properties)
                                row[p.Name] = obj[p.Name] ?? DBNull.Value;
                            row["Target"] = target;
                            resultsTable.Rows.Add(row);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show($"Failed to query {target}:\n{ex.Message}")
                    );
                }
            })).ToList();

            await Task.WhenAll(tasks);
            dgWmiResults.ItemsSource = resultsTable.DefaultView;
        }

        // ------------------------
        // 🔧 Utility Functions
        // ------------------------

        private bool IsSubnet(string input)
        {
            return input.Contains("/") && IPAddress.TryParse(input.Split('/')[0], out _);
        }

        private IEnumerable<string> ExpandCidr(string cidr)
        {
            var parts = cidr.Split('/');
            var baseIp = IPAddress.Parse(parts[0]);
            int prefix = int.Parse(parts[1]);
            int max = 32 - prefix;
            uint ip = BitConverter.ToUInt32(baseIp.GetAddressBytes().Reverse().ToArray(), 0);
            uint count = (uint)(1 << max);

            for (uint i = 1; i < count - 1; i++) // skip network and broadcast
            {
                var bytes = BitConverter.GetBytes(ip + i).Reverse().ToArray();
                yield return new IPAddress(bytes).ToString();
            }
        }

        private async Task<bool> PingHost(string host)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, 500); // 500ms timeout
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private void btnLoadQuery_Click(object sender, RoutedEventArgs e)
        {
            if (lb_savedQueries.SelectedItem is WmiQuery selected)
            {

                cb_WmiNamespace.SelectedValue = selected.Namespace;
                cb_WmiClass.SelectedValue = selected.Class;
                txt_wmiQuery.Text = selected.Data;
            }

        }
        private void txt_wmiQuery_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void TabItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            txt_Target.Visibility = Visibility.Hidden;

        }

        private void TabItem_RequestBringIntoView_1(object sender, RequestBringIntoViewEventArgs e)
        {
            txt_Target.Visibility = Visibility.Visible;

        }

        private void TabItem_RequestBringIntoView_2(object sender, RequestBringIntoViewEventArgs e)
        {
            txt_Target.Visibility = Visibility.Visible;

        }

        private void lb_savedQueries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lb_savedQueries.SelectedItem is SavedQuery selected)
            {
                txt_wmiQuery.Text = selected.Query;
            }
        }

        private void txtWmiTargets_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }
        private ObservableCollection<FileSystemItem> _allItems; // Store the full, unfiltered tree



        private void search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = search.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(filterText))
            {
                // Show all files (no folders) if search is empty
                treeViewScripts.ItemsSource = FilterFilesOnly(_allItems);
            }
            else
            {
                treeViewScripts.ItemsSource = FilterTree(_allItems, filterText);
            }
        }

        // Helper: returns only files (no folders) from the tree, flattening the structure
        private ObservableCollection<FileSystemItem> FilterFilesOnly(IEnumerable<FileSystemItem> items)
        {
            var result = new ObservableCollection<FileSystemItem>();
            foreach (var item in items)
            {
                if (item.IsDirectory && item.Children != null)
                {
                    foreach (var child in FilterFilesOnly(item.Children))
                        result.Add(child);
                }
                else if (!item.IsDirectory)
                {
                    result.Add(item);
                }
            }
            return result;
        }
        private ObservableCollection<FileSystemItem> FilterTree(IEnumerable<FileSystemItem> items, string filter)
        {
            var result = new ObservableCollection<FileSystemItem>();
            if (items == null)
                return result;

            foreach (var item in items)
            {
                if (item.IsDirectory && item.Children != null)
                {
                    var filteredChildren = FilterTree(item.Children, filter);
                    if (filteredChildren.Count > 0)
                    {
                        // Clone the folder, but only with matching children
                        var folder = new FileSystemItem
                        {
                            Name = item.Name,
                            FullPath = item.FullPath,
                            IsDirectory = true,
                            Children = filteredChildren
                        };
                        result.Add(folder);
                    }
                }
                else if (!item.IsDirectory && item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        private void treeViewScripts_Loaded(object sender, RoutedEventArgs e)
        {


        }

    }
}