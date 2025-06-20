using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml;


namespace WpfApp3
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

                    var watcher = new FileSystemWatcher(Path.GetDirectoryName(logPath), Path.GetFileName(logPath));
                    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                    watcher.Changed += (s, args) =>
                    {
                        try
                        {
                            var text = File.ReadAllText(logPath);
                            Dispatcher.Invoke(() =>
                            {
                                job.Log = text;
                                if (lsv_jobs.SelectedItem == job)
                                {
                                    editorLogs.Document.Blocks.Clear();
                                    editorLogs.AppendText(text);
                                }
                            });
                        }
                        catch { }
                    };

                    job.Watcher = watcher;
                    watcher.EnableRaisingEvents = true;

                    while (!job.Cancellation.Token.IsCancellationRequested)
                    {
                        Thread.Sleep(500);
                    }

                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    job.Status = "Cancelled";
                    job.Cancellation.Token.Register(() =>
                    {
                        if (job.Watcher != null)
                        {
                            job.Watcher.EnableRaisingEvents = false;
                            job.Watcher.Dispose();
                        }
                    });
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

        bool IsStructuredData(string text,  out DataTable dataTable)
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
            var theseLogs = new System.Windows.Documents.TextRange(editorLogs.Document.ContentStart, editorLogs.Document.ContentEnd).Text;

            var popupViewer = new LogViewerWindow(theseLogs);
            bool? result = popupViewer.ShowDialog();

            if (result == true)
            {
                editorLogs.Document.Blocks.Clear();
                editorLogs.AppendText(popupViewer.logText);
            }
        }
        private void lb_jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lsv_jobs.SelectedItem is RemoteJob job)
            {
               if(IsStructuredData(job.Log, out var tableData))
                {
                    dg_logs.ItemsSource = tableData.DefaultView;
                }
                else
                {
                    dg_logs.IsEnabled = false;
                    // Fallback to plain text display
                    editorLogs.IsEnabled = true;
                    editorLogs.Document.Blocks.Clear();
                    editorLogs.AppendText(job.Log);
                }




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
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WpfApp3");
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
        {        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }
private void lsv_jobs_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (lsv_jobs.SelectedItem is RemoteJob selectedJob)
    {
        string log = selectedJob.Log ?? "No log output yet.";

                var table = ParseFormattedTable(log);
                LoadTableTextToWpfGrid(dg_logs, log);

                var cleaned = log.Trim();

                editorLogs.Document.Blocks.Clear();
        editorLogs.AppendText(cleaned);
    }
}
        //
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


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeAdminTools();
            string path = @"L:\\Scripts\\System"; // or dynamically prompt user
            var rootItems = LoadPs1DirectoryTree(path);
            treeViewScripts.ItemsSource = rootItems;
            LoadWmiNamespaces();
            LoadSavedQueries();
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
                    string args =  tool.executeTarget + " /computer:" + target ;
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
            combo_fontsize.ItemsSource = fontsize;
        }

        private void combo_fontsize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (double.TryParse(combo_fontsize.SelectionBoxItem.ToString(), out double fontSize))
            {
                editorLogs.FontSize = fontSize;
            }
            else
            {
                // Handle invalid input, e.g., log an error or set a default value
            }
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

        private readonly string QueriesDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WpfApp3", "WmiQueries");

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
            string target = GetTargets().FirstOrDefault() ?? "localhost";
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
            catch { }
        }

        private void cb_WmiClass_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lb_WmiProperties.ItemsSource = null;
            if (cb_WmiClass.SelectedItem == null || cb_WmiNamespace.SelectedItem == null) return;
            string ns = cb_WmiNamespace.SelectedItem.ToString();
            string cls = cb_WmiClass.SelectedItem.ToString();
            string target = GetTargets().FirstOrDefault() ?? "localhost";
            try
            {
                var scope = new ManagementScope($"\\\\{target}\\{ns}");
                scope.Connect();
                var mc = new ManagementClass(scope, new ManagementPath(cls), null);
                var props = new List<string>();
                foreach (PropertyData p in mc.Properties)
                {
                    props.Add(p.Name);
                }
                lb_WmiProperties.ItemsSource = props;
            }
            catch { }
        }

        private void btnBuildWmiQuery_Click(object sender, RoutedEventArgs e)
        {
            if (cb_WmiClass.SelectedItem == null) return;
            var selectedProps = lb_WmiProperties.SelectedItems.Cast<string>().ToList();
            string propPart = selectedProps.Count > 0 ? string.Join(",", selectedProps) : "*";
            txt_wmiQuery.Text = $"SELECT {propPart} FROM {cb_WmiClass.SelectedItem}";
        }

        private IEnumerable<string> GetTargets()
        {
            return txtWmiTargets.Text.Split(new[] { '\n', '\r', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private async void btnRunWmiQuery_Click(object sender, RoutedEventArgs e)
        {
            string query = txt_wmiQuery.Text.Trim();
            if (string.IsNullOrWhiteSpace(query) || cb_WmiNamespace.SelectedItem == null) return;
            string ns = cb_WmiNamespace.SelectedItem.ToString();
            var resultsTable = new DataTable();
            bool columnsCreated = false;

            foreach (var target in GetTargets())
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var scope = new ManagementScope($"\\\\{target}\\{ns}");
                        scope.Connect();
                        var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
                        foreach (ManagementObject obj in searcher.Get())
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
                                row[p.Name] = obj[p.Name];
                            row["Target"] = target;
                            resultsTable.Rows.Add(row);
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"Failed to query {target}: {ex.Message}"));
                    }
                });
            }
            dgWmiResults.ItemsSource = resultsTable.DefaultView;
        }

        private void LoadSavedQueries()
        {
            try
            {
                Directory.CreateDirectory(QueriesDir);
                var files = Directory.GetFiles(QueriesDir, "*.txt");
                var names = files.Select(Path.GetFileName).ToList();
                lb_savedQueries.ItemsSource = names;
            }
            catch { }
        }

        private void btnLoadQuery_Click(object sender, RoutedEventArgs e)
        {
            if (lb_savedQueries.SelectedItem is string file)
            {
                string path = Path.Combine(QueriesDir, file);
                if (File.Exists(path))
                    txt_wmiQuery.Text = File.ReadAllText(path);
            }
        }
    }
}