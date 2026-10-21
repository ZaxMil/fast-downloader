using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FastDownloader
{
    public static class JsonHelper
    {
        private static JavaScriptSerializer serializer = new JavaScriptSerializer();
        public static string Serialize(object obj) { return serializer.Serialize(obj); }
        public static Dictionary<string, object> ParseDict(string json) { return serializer.Deserialize<Dictionary<string, object>>(json); }
    }

    public class BrowserInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string exePath { get; set; }
        public string extensionsUrl { get; set; }
        public string icon { get; set; }
        public bool installed { get; set; }
    }

    public static class BrowserInstaller
    {
        public const string ExtensionId = "gofdohcbjeiggejfkdhbpgddoejjdafg";

        public static List<BrowserInfo> DetectBrowsers()
        {
            var list = new List<BrowserInfo>();

            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            // 1. Microsoft Edge
            string edgePath = Path.Combine(progFilesX86, @"Microsoft\Edge\Application\msedge.exe");
            if (!File.Exists(edgePath)) edgePath = Path.Combine(progFiles, @"Microsoft\Edge\Application\msedge.exe");
            list.Add(new BrowserInfo
            {
                id = "edge",
                name = "Microsoft Edge",
                exePath = edgePath,
                extensionsUrl = "edge://extensions",
                icon = "🌐",
                installed = File.Exists(edgePath)
            });

            // 2. Google Chrome
            string chromePath = Path.Combine(progFiles, @"Google\Chrome\Application\chrome.exe");
            if (!File.Exists(chromePath)) chromePath = Path.Combine(progFilesX86, @"Google\Chrome\Application\chrome.exe");
            if (!File.Exists(chromePath)) chromePath = Path.Combine(localApp, @"Google\Chrome\Application\chrome.exe");
            list.Add(new BrowserInfo
            {
                id = "chrome",
                name = "Google Chrome",
                exePath = chromePath,
                extensionsUrl = "chrome://extensions",
                icon = "🟡",
                installed = File.Exists(chromePath)
            });

            // 3. Brave Browser
            string bravePath = Path.Combine(progFiles, @"BraveSoftware\Brave-Browser\Application\brave.exe");
            if (!File.Exists(bravePath)) bravePath = Path.Combine(progFilesX86, @"BraveSoftware\Brave-Browser\Application\brave.exe");
            if (!File.Exists(bravePath)) bravePath = Path.Combine(localApp, @"BraveSoftware\Brave-Browser\Application\brave.exe");
            list.Add(new BrowserInfo
            {
                id = "brave",
                name = "Brave Browser",
                exePath = bravePath,
                extensionsUrl = "brave://extensions",
                icon = "🦁",
                installed = File.Exists(bravePath)
            });

            // 4. Opera
            string operaPath = Path.Combine(localApp, @"Programs\Opera\launcher.exe");
            if (!File.Exists(operaPath)) operaPath = Path.Combine(progFiles, @"Opera\launcher.exe");
            list.Add(new BrowserInfo
            {
                id = "opera",
                name = "Opera",
                exePath = operaPath,
                extensionsUrl = "opera://extensions",
                icon = "🔴",
                installed = File.Exists(operaPath)
            });

            // 5. Vivaldi
            string vivaldiPath = Path.Combine(localApp, @"Vivaldi\Application\vivaldi.exe");
            if (!File.Exists(vivaldiPath)) vivaldiPath = Path.Combine(progFiles, @"Vivaldi\Application\vivaldi.exe");
            list.Add(new BrowserInfo
            {
                id = "vivaldi",
                name = "Vivaldi",
                exePath = vivaldiPath,
                extensionsUrl = "vivaldi://extensions",
                icon = "🔴",
                installed = File.Exists(vivaldiPath)
            });

            return list;
        }

        public static bool InstallToBrowser(string browserId, string appDir, out string browserName, out string error)
        {
            browserName = browserId;
            error = null;
            try
            {
                var browsers = DetectBrowsers();
                var target = browsers.Find(b => b.id.Equals(browserId, StringComparison.OrdinalIgnoreCase));
                if (target == null || !target.installed)
                {
                    error = "Browser is not detected on this system.";
                    return false;
                }

                browserName = target.name;
                string extDir = Path.Combine(appDir, "extension");

                // 1. Registry Keys for external extension
                string regSubKey = null;
                if (target.id == "edge") regSubKey = @"Software\Microsoft\Edge\Extensions\" + ExtensionId;
                else if (target.id == "chrome") regSubKey = @"Software\Google\Chrome\Extensions\" + ExtensionId;
                else if (target.id == "brave") regSubKey = @"Software\BraveSoftware\Brave-Browser\Extensions\" + ExtensionId;
                else if (target.id == "opera") regSubKey = @"Software\Opera Software\Opera Stable\Extensions\" + ExtensionId;
                else if (target.id == "vivaldi") regSubKey = @"Software\Vivaldi\Extensions\" + ExtensionId;

                if (!string.IsNullOrEmpty(regSubKey))
                {
                    try
                    {
                        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(regSubKey))
                        {
                            if (k != null)
                            {
                                k.SetValue("path", extDir);
                                k.SetValue("version", "1.0.0");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        error = "Registry notice: " + ex.Message;
                    }
                }

                // 2. Put extension directory path in Clipboard & open Explorer
                try
                {
                    Thread staThread = new Thread(() =>
                    {
                        try { Clipboard.SetText(extDir); } catch { }
                    });
                    staThread.SetApartmentState(ApartmentState.STA);
                    staThread.Start();
                    staThread.Join(400);

                    Process.Start("explorer.exe", extDir);
                }
                catch { }

                // 3. Launch browser with --load-extension
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = target.exePath,
                        Arguments = string.Format("--load-extension=\"{0}\" \"{1}\"", extDir, target.extensionsUrl),
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    error = "Could not launch browser: " + ex.Message;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    public class YtTaskInfo
    {
        public string Gid { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public string Quality { get; set; }
        public string Queue { get; set; }
        public int Progress { get; set; }
        public string Status { get; set; } // "downloading", "paused", "completed", "error"
        public long DownloadSpeed { get; set; }
        public long TotalLength { get; set; }
        public long CompletedLength { get; set; }
        public long Eta { get; set; }
        public Process Process { get; set; }
    }

    public class QueueConfig
    {
        public bool Enabled { get; set; }
        public string StartTime { get; set; } // "02:00"
        public string StopTime { get; set; }  // "07:00"
        public int MaxConcurrent { get; set; }
        public bool Shutdown { get; set; }
        public bool Active { get; set; }

        public QueueConfig()
        {
            MaxConcurrent = 3;
            Active = true;
        }
    }

    public class EngineManager
    {
        private Process ariaProcess;
        public string DownloadsFolder { get; private set; }
        public string AppDir { get; private set; }
        private readonly object ytLock = new object();
        public List<YtTaskInfo> YtTasks { get; private set; }
        public Dictionary<string, QueueConfig> Queues { get; private set; }
        public bool AutoCaptureEnabled { get; set; }
        private string settingsFilePath;

        public EngineManager()
        {
            AppDir = AppDomain.CurrentDomain.BaseDirectory;
            DownloadsFolder = Path.Combine(AppDir, "downloads");
            if (!Directory.Exists(DownloadsFolder))
            {
                Directory.CreateDirectory(DownloadsFolder);
            }
            AutoCaptureEnabled = true;
            settingsFilePath = Path.Combine(AppDir, "settings.json");
            LoadSettings();

            YtTasks = new List<YtTaskInfo>();
            Queues = new Dictionary<string, QueueConfig>();
            Queues["MainQueue"] = new QueueConfig { Enabled = false, StartTime = "02:00", StopTime = "07:00", MaxConcurrent = 3, Shutdown = false, Active = true };
            Queues["NightQueue"] = new QueueConfig { Enabled = true, StartTime = "02:00", StopTime = "07:00", MaxConcurrent = 1, Shutdown = true, Active = false };
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    var dict = JsonHelper.ParseDict(json);
                    if (dict != null && dict.ContainsKey("autoCapture"))
                    {
                        AutoCaptureEnabled = Convert.ToBoolean(dict["autoCapture"]);
                    }
                }
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                var dict = new Dictionary<string, object>();
                dict["autoCapture"] = AutoCaptureEnabled;
                File.WriteAllText(settingsFilePath, JsonHelper.Serialize(dict));
            }
            catch { }
        }

        public void StartAria2()
        {
            string ariaExe = Path.Combine(AppDir, "bin", "aria2c.exe");
            if (!File.Exists(ariaExe)) ariaExe = Path.Combine(AppDir, "aria2c.exe");
            if (!File.Exists(ariaExe))
            {
                string scriptPath = Path.Combine(AppDir, "scripts", "setup-binaries.ps1");
                if (File.Exists(scriptPath))
                {
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = string.Format("-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"{0}\"", scriptPath),
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            var p = Process.Start(psi);
                            p.WaitForExit();
                            StartAria2();
                        }
                        catch { }
                    });
                }
                return;
            }

            try
            {
                string safeDir = DownloadsFolder.Replace("\\", "/");
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ariaExe,
                    Arguments = string.Format(
                        "--enable-rpc --rpc-listen-all=false --rpc-listen-port=6800 --disable-ipv6=true " +
                        "--async-dns-server=8.8.8.8,1.1.1.1 --file-allocation=none " +
                        "--max-connection-per-server=16 --split=16 --min-split-size=1M " +
                        "--max-concurrent-downloads=5 --continue=true --dir=\"{0}\" " +
                        "--enable-dht=true --enable-peer-exchange=true",
                        safeDir),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                ariaProcess = Process.Start(psi);
            }
            catch { }
        }

        public void StopAria2()
        {
            try
            {
                if (ariaProcess != null && !ariaProcess.HasExited) ariaProcess.Kill();
            }
            catch { }

            lock (ytLock)
            {
                foreach (var t in YtTasks)
                {
                    try { if (t.Process != null && !t.Process.HasExited) t.Process.Kill(); } catch { }
                }
            }
        }

        public string AddAriaUri(string url, bool startNow, string cookies = null, string userAgent = null, string referer = null, string filename = null)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6800/jsonrpc");
                req.Method = "POST";
                req.ContentType = "application/json";
                var options = new Dictionary<string, object>();
                if (!startNow)
                {
                    options["pause"] = "true";
                }
                if (!string.IsNullOrEmpty(filename))
                {
                    options["out"] = filename;
                }
                var headers = new List<string>();
                if (!string.IsNullOrEmpty(cookies)) headers.Add("Cookie: " + cookies);
                if (!string.IsNullOrEmpty(userAgent)) headers.Add("User-Agent: " + userAgent);
                if (!string.IsNullOrEmpty(referer)) headers.Add("Referer: " + referer);
                if (headers.Count > 0)
                {
                    options["header"] = headers.ToArray();
                }

                var payload = new { jsonrpc = "2.0", id = Guid.NewGuid().ToString(), method = "aria2.addUri", @params = new object[] { new string[] { url }, options } };
                byte[] data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(payload));
                req.ContentLength = data.Length;
                using (var stream = req.GetRequestStream()) stream.Write(data, 0, data.Length);
                using (var resp = req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                {
                    var dict = JsonHelper.ParseDict(reader.ReadToEnd());
                    if (dict.ContainsKey("result")) return dict["result"].ToString();
                }
            }
            catch { }
            return null;
        }

        public List<Dictionary<string, object>> GetAriaTasks()
        {
            List<Dictionary<string, object>> tasks = new List<Dictionary<string, object>>();
            try
            {
                string[] methods = new string[] { "aria2.tellActive", "aria2.tellWaiting", "aria2.tellStopped" };
                foreach (var method in methods)
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6800/jsonrpc");
                    req.Method = "POST";
                    req.ContentType = "application/json";
                    req.Timeout = 1200;
                    object[] prms = method == "aria2.tellActive" ? new object[] { } : new object[] { 0, 100 };
                    var payload = new { jsonrpc = "2.0", id = "poll", method = method, @params = prms };
                    byte[] data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(payload));
                    req.ContentLength = data.Length;
                    using (var stream = req.GetRequestStream()) stream.Write(data, 0, data.Length);
                    using (var resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                    {
                        var dict = JsonHelper.ParseDict(reader.ReadToEnd());
                        if (dict.ContainsKey("result"))
                        {
                            var list = dict["result"] as ArrayList;
                            if (list != null)
                            {
                                foreach (var item in list)
                                {
                                    if (item is Dictionary<string, object>) tasks.Add((Dictionary<string, object>)item);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return tasks;
        }

        public void PauseTask(string gid) { SendAriaAction("aria2.pause", gid); }
        public void ResumeTask(string gid) { SendAriaAction("aria2.unpause", gid); }
        public void RemoveTask(string gid)
        {
            SendAriaAction("aria2.remove", gid);
            SendAriaAction("aria2.removeDownloadResult", gid);
            lock (ytLock)
            {
                var yt = YtTasks.Find(t => t.Gid == gid);
                if (yt != null)
                {
                    try { if (yt.Process != null && !yt.Process.HasExited) yt.Process.Kill(); } catch { }
                    YtTasks.Remove(yt);
                }
            }
        }
        public void PauseAll() { SendAriaActionWithoutParams("aria2.pauseAll"); }
        public void ResumeAll() { SendAriaActionWithoutParams("aria2.unpauseAll"); }

        public void SetSpeedLimit(string limit)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6800/jsonrpc");
                req.Method = "POST";
                req.ContentType = "application/json";
                var options = new Dictionary<string, string> { { "max-overall-download-limit", limit } };
                var payload = new { jsonrpc = "2.0", id = "speed_limit", method = "aria2.changeGlobalOption", @params = new object[] { options } };
                byte[] data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(payload));
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = req.GetResponse()) { }
            }
            catch { }
        }

        public void SetMaxConcurrentDownloads(int max)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6800/jsonrpc");
                req.Method = "POST";
                req.ContentType = "application/json";
                var options = new Dictionary<string, string> { { "max-concurrent-downloads", max.ToString() } };
                var payload = new { jsonrpc = "2.0", id = "max_active", method = "aria2.changeGlobalOption", @params = new object[] { options } };
                byte[] data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(payload));
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = req.GetResponse()) { }
            }
            catch { }
        }

        private void SendAriaAction(string method, string gid)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6800/jsonrpc");
                req.Method = "POST";
                req.ContentType = "application/json";
                var payload = new { jsonrpc = "2.0", id = "action", method = method, @params = new object[] { gid } };
                byte[] data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(payload));
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = req.GetResponse()) { }
            }
            catch { }
        }

        private void SendAriaActionWithoutParams(string method)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6800/jsonrpc");
                req.Method = "POST";
                req.ContentType = "application/json";
                var payload = new { jsonrpc = "2.0", id = "action_all", method = method, @params = new object[] { } };
                byte[] data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(payload));
                req.ContentLength = data.Length;
                using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = req.GetResponse()) { }
            }
            catch { }
        }

        public string StartYtDlp(string url, string quality, string queue, string cookies = null, string userAgent = null, string referer = null)
        {
            string gid = "yt_" + Guid.NewGuid().ToString().Substring(0, 8);
            string ytExe = Path.Combine(AppDir, "bin", "yt-dlp.exe");
            if (!File.Exists(ytExe)) ytExe = Path.Combine(AppDir, "yt-dlp.exe");
            string ffmpegDir = Path.Combine(AppDir, "bin");

            string formatArg = "-f \"bv*[vcodec^=avc1]+ba[acodec^=mp4a]/bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b\" --merge-output-format mp4";
            if (quality == "720p") formatArg = "-f \"bv*[height<=720][vcodec^=avc1]+ba[acodec^=mp4a]/bv*[height<=720][ext=mp4]+ba[ext=m4a]/b[height<=720]/bv*[height<=720]+ba/b\" --merge-output-format mp4";
            if (quality == "audio") formatArg = "-x --audio-format mp3";

            string extraHeaders = "";
            if (!string.IsNullOrEmpty(cookies)) extraHeaders += " --add-header \"Cookie:" + cookies.Replace("\"", "\\\"") + "\"";
            if (!string.IsNullOrEmpty(referer)) extraHeaders += " --referer \"" + referer.Replace("\"", "\\\"") + "\"";
            if (!string.IsNullOrEmpty(userAgent)) extraHeaders += " --user-agent \"" + userAgent.Replace("\"", "\\\"") + "\"";

            YtTaskInfo taskInfo = new YtTaskInfo
            {
                Gid = gid,
                Url = url,
                Name = "Online Video: " + (url.Length > 45 ? url.Substring(0, 45) + "..." : url),
                Quality = quality,
                Queue = queue,
                Progress = 0,
                Status = "downloading"
            };

            lock (ytLock)
            {
                YtTasks.Insert(0, taskInfo);
            }

            Thread t = new Thread(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ytExe,
                        Arguments = string.Format("--ffmpeg-location \"{0}\" {1}{2} --external-downloader aria2c --downloader-args \"aria2c:-x 16 -s 16 -k 1M --disable-ipv6=true --async-dns-server=8.8.8.8,1.1.1.1\" -P \"{3}\" --newline \"{4}\"",
                            ffmpegDir, formatArg, extraHeaders, DownloadsFolder, url),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (Process p = Process.Start(psi))
                    {
                        taskInfo.Process = p;
                        string line;
                        while ((line = p.StandardOutput.ReadLine()) != null)
                        {
                            if (line.Contains("[download]") && line.Contains("%"))
                            {
                                int idx = line.IndexOf('%');
                                if (idx > 0)
                                {
                                    string part = line.Substring(0, idx);
                                    int spaceIdx = part.LastIndexOf(' ');
                                    if (spaceIdx >= 0)
                                    {
                                        int pct;
                                        if (int.TryParse(part.Substring(spaceIdx + 1).Trim().Split('.')[0], out pct))
                                        {
                                            taskInfo.Progress = pct;
                                        }
                                    }
                                }
                                taskInfo.Status = "downloading";
                            }
                            else if (line.Contains("[Merger]") || line.Contains("[ExtractAudio]"))
                            {
                                taskInfo.Progress = 99;
                                taskInfo.Status = "processing";
                            }
                        }
                        p.WaitForExit();
                        taskInfo.Progress = 100;
                        taskInfo.Status = p.ExitCode == 0 ? "completed" : "error";
                    }
                }
                catch
                {
                    taskInfo.Status = "error";
                }
            });
            t.IsBackground = true;
            t.Start();

            return gid;
        }
    }

    public class HttpBridgeServer
    {
        private TcpListener listener;
        private Thread serverThread;
        private bool isRunning;
        private EngineManager engine;
        private Action<string> onNotification;
        private DateTime lastExtensionPing = DateTime.MinValue;

        public HttpBridgeServer(EngineManager engine, Action<string> onNotification)
        {
            this.engine = engine;
            this.onNotification = onNotification;
        }

        public void Start()
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, 6801);
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start();
                isRunning = true;
                serverThread = new Thread(ListenLoop);
                serverThread.IsBackground = true;
                serverThread.Start();
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(engine.AppDir, "bridge_error.log"), ex.ToString()); } catch { }
            }
        }

        public void Stop()
        {
            isRunning = false;
            try { listener.Stop(); } catch { }
        }

        private void ListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(ProcessClient, client);
                }
                catch
                {
                    if (!isRunning) break;
                }
            }
        }

        private void ProcessClient(object obj)
        {
            TcpClient client = (TcpClient)obj;
            try
            {
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
                {
                    string firstLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(firstLine)) return;

                    string[] parts = firstLine.Split(' ');
                    string method = parts[0];
                    string urlPath = parts.Length > 1 ? parts[1] : "/";

                    int contentLength = 0;
                    string headerLine;
                    while (!string.IsNullOrEmpty(headerLine = reader.ReadLine()))
                    {
                        if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(headerLine.Substring(15).Trim(), out contentLength);
                        }
                    }

                    if (method == "OPTIONS")
                    {
                        SendHeaders(writer, 200, "OK", "text/plain", 0);
                        return;
                    }

                    // READ POST BODY
                    string requestBody = "";
                    if (contentLength > 0)
                    {
                        char[] buf = new char[contentLength];
                        int read = 0;
                        while (read < contentLength)
                        {
                            int r = reader.Read(buf, read, contentLength - read);
                            if (r <= 0) break;
                            read += r;
                        }
                        requestBody = new string(buf, 0, read);
                    }

                    if (urlPath == "/favicon.ico")
                    {
                        string favPath = Path.Combine(engine.AppDir, "ui", "favicon.ico");
                        if (!File.Exists(favPath)) favPath = Path.Combine(engine.AppDir, "app.ico");
                        if (File.Exists(favPath))
                        {
                            byte[] icoBytes = File.ReadAllBytes(favPath);
                            SendHeaders(writer, 200, "OK", "image/x-icon", icoBytes.Length);
                            writer.BaseStream.Write(icoBytes, 0, icoBytes.Length);
                            writer.BaseStream.Flush();
                            return;
                        }
                        SendHeaders(writer, 204, "No Content", "image/x-icon", 0);
                        return;
                    }

                    // 1. SERVE UI & STATIC ASSETS
                    if (urlPath == "/" || urlPath == "/index.html")
                    {
                        string uiPath = Path.Combine(engine.AppDir, "ui", "index.html");
                        if (File.Exists(uiPath))
                        {
                            byte[] htmlBytes = File.ReadAllBytes(uiPath);
                            SendHeaders(writer, 200, "OK", "text/html; charset=utf-8", htmlBytes.Length);
                            writer.BaseStream.Write(htmlBytes, 0, htmlBytes.Length);
                            writer.BaseStream.Flush();
                            return;
                        }
                    }

                    if (urlPath.StartsWith("/ui/"))
                    {
                        string subFile = urlPath.Substring(4).TrimStart('/', '\\');
                        string filePath = Path.Combine(engine.AppDir, "ui", subFile);
                        if (File.Exists(filePath))
                        {
                            string mime = "application/octet-stream";
                            string ext = Path.GetExtension(filePath).ToLowerInvariant();
                            if (ext == ".html" || ext == ".htm") mime = "text/html; charset=utf-8";
                            else if (ext == ".png") mime = "image/png";
                            else if (ext == ".jpg" || ext == ".jpeg") mime = "image/jpeg";
                            else if (ext == ".ico") mime = "image/x-icon";
                            else if (ext == ".css") mime = "text/css; charset=utf-8";
                            else if (ext == ".js") mime = "application/javascript; charset=utf-8";
                            else if (ext == ".svg") mime = "image/svg+xml";

                            byte[] fileBytes = File.ReadAllBytes(filePath);
                            SendHeaders(writer, 200, "OK", mime, fileBytes.Length);
                            writer.BaseStream.Write(fileBytes, 0, fileBytes.Length);
                            writer.BaseStream.Flush();
                            return;
                        }
                    }

                    // 2. STATUS API
                    if (urlPath == "/api/status")
                    {
                        bool extConnected = (DateTime.UtcNow - lastExtensionPing).TotalSeconds < 35;
                        var statusObj = new
                        {
                            status = "running",
                            engine = "aria2+yt-dlp",
                            downloads = engine.DownloadsFolder,
                            extensionConnected = extConnected,
                            autoCapture = engine.AutoCaptureEnabled
                        };
                        string json = JsonHelper.Serialize(statusObj);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 3. GET TASKS API (UNIFIED)
                    if (urlPath == "/api/tasks")
                    {
                        var ariaTasks = engine.GetAriaTasks();
                        var unifiedList = new List<Dictionary<string, object>>();

                        foreach (var t in ariaTasks)
                        {
                            if (!t.ContainsKey("gid")) continue;
                            string gid = t["gid"].ToString();
                            string status = t.ContainsKey("status") ? t["status"].ToString() : "unknown";

                            long totalLength = 0;
                            if (t.ContainsKey("totalLength") && t["totalLength"] != null) long.TryParse(t["totalLength"].ToString(), out totalLength);

                            long completedLength = 0;
                            if (t.ContainsKey("completedLength") && t["completedLength"] != null) long.TryParse(t["completedLength"].ToString(), out completedLength);

                            long downloadSpeed = 0;
                            if (t.ContainsKey("downloadSpeed") && t["downloadSpeed"] != null) long.TryParse(t["downloadSpeed"].ToString(), out downloadSpeed);

                            long progress = totalLength > 0 ? (completedLength * 100) / totalLength : 0;
                            long eta = (downloadSpeed > 0 && totalLength > completedLength) ? (totalLength - completedLength) / downloadSpeed : 0;

                            string filename = "";
                            if (t.ContainsKey("bittorrent"))
                            {
                                var bt = t["bittorrent"] as Dictionary<string, object>;
                                if (bt != null && bt.ContainsKey("info"))
                                {
                                    var info = bt["info"] as Dictionary<string, object>;
                                    if (info != null && info.ContainsKey("name")) filename = info["name"].ToString();
                                }
                            }
                            if (string.IsNullOrEmpty(filename) && t.ContainsKey("files"))
                            {
                                var files = t["files"] as ArrayList;
                                if (files != null && files.Count > 0)
                                {
                                    var firstFile = files[0] as Dictionary<string, object>;
                                    if (firstFile != null)
                                    {
                                        if (firstFile.ContainsKey("path") && firstFile["path"] != null)
                                        {
                                            string p = firstFile["path"].ToString();
                                            if (!string.IsNullOrEmpty(p)) filename = Path.GetFileName(p);
                                        }
                                        if (string.IsNullOrEmpty(filename) && firstFile.ContainsKey("uris"))
                                        {
                                            var uris = firstFile["uris"] as ArrayList;
                                            if (uris != null && uris.Count > 0)
                                            {
                                                var uObj = uris[0] as Dictionary<string, object>;
                                                if (uObj != null && uObj.ContainsKey("uri"))
                                                {
                                                    string uriStr = uObj["uri"].ToString();
                                                    try
                                                    {
                                                        Uri u = new Uri(uriStr);
                                                        filename = Path.GetFileName(u.LocalPath);
                                                        if (string.IsNullOrEmpty(filename)) filename = u.Host;
                                                    }
                                                    catch { filename = uriStr; }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(filename)) filename = "Direct Download (" + gid + ")";

                            var item = new Dictionary<string, object>();
                            item["gid"] = gid;
                            item["name"] = filename;
                            item["totalLength"] = totalLength;
                            item["completedLength"] = completedLength;
                            item["downloadSpeed"] = downloadSpeed;
                            item["progress"] = progress;
                            item["eta"] = eta;
                            item["status"] = status;
                            item["engine"] = "aria2";
                            unifiedList.Add(item);
                        }

                        // Add YT-DLP Tasks
                        lock (engine.YtTasks)
                        {
                            foreach (var yt in engine.YtTasks)
                            {
                                var item = new Dictionary<string, object>();
                                item["gid"] = yt.Gid;
                                item["name"] = yt.Name;
                                item["totalLength"] = yt.TotalLength;
                                item["completedLength"] = yt.CompletedLength;
                                item["downloadSpeed"] = yt.DownloadSpeed;
                                item["progress"] = yt.Progress;
                                item["eta"] = yt.Eta;
                                item["status"] = yt.Status;
                                item["engine"] = "yt-dlp";
                                item["url"] = yt.Url;
                                unifiedList.Insert(0, item);
                            }
                        }

                        var result = new Dictionary<string, object> { { "tasks", unifiedList } };
                        string json = JsonHelper.Serialize(result);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 4. ADD DOWNLOAD API
                    if (urlPath == "/api/download" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string dlUrl = data.ContainsKey("url") ? data["url"].ToString().Trim() : "";
                        string quality = data.ContainsKey("quality") ? data["quality"].ToString() : "best";
                        string queue = data.ContainsKey("queue") ? data["queue"].ToString() : "MainQueue";
                        string cookies = data.ContainsKey("cookies") ? data["cookies"].ToString() : "";
                        string userAgent = data.ContainsKey("userAgent") ? data["userAgent"].ToString() : "";
                        string referer = data.ContainsKey("referer") ? data["referer"].ToString() : "";
                        string filename = data.ContainsKey("filename") ? data["filename"].ToString() : "";
                        bool startNow = true;
                        if (data.ContainsKey("startNow")) bool.TryParse(data["startNow"].ToString(), out startNow);

                        if (!string.IsNullOrEmpty(dlUrl))
                        {
                            bool isVideo = dlUrl.Contains("youtube.com") || dlUrl.Contains("youtu.be") ||
                                           dlUrl.Contains("tiktok.com") || dlUrl.Contains("twitter.com") || dlUrl.Contains("x.com");

                            string assignedGid;
                            if (isVideo)
                            {
                                assignedGid = engine.StartYtDlp(dlUrl, quality, queue, cookies, userAgent, referer);
                            }
                            else
                            {
                                assignedGid = engine.AddAriaUri(dlUrl, startNow, cookies, userAgent, referer, filename);
                            }

                            if (onNotification != null) onNotification("Download added: " + dlUrl);

                            string ok = "{\"ok\":true,\"gid\":\"" + assignedGid + "\"}";
                            byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                            SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                            writer.Write(ok);
                            writer.Flush();
                            return;
                        }
                    }

                    // 5. TASK ACTION API
                    if (urlPath == "/api/action" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string gid = data.ContainsKey("gid") ? data["gid"].ToString() : "";
                        string action = data.ContainsKey("action") ? data["action"].ToString() : "";

                        if (action == "pause" && !string.IsNullOrEmpty(gid)) engine.PauseTask(gid);
                        if (action == "resume" && !string.IsNullOrEmpty(gid)) engine.ResumeTask(gid);
                        if (action == "remove" && !string.IsNullOrEmpty(gid)) engine.RemoveTask(gid);
                        if (action == "pauseAll") engine.PauseAll();
                        if (action == "resumeAll") engine.ResumeAll();

                        string ok = "{\"ok\":true}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 6. SPEED LIMIT API
                    if (urlPath == "/api/speedlimit" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string limit = data.ContainsKey("limit") ? data["limit"].ToString() : "0";
                        engine.SetSpeedLimit(limit);

                        string ok = "{\"ok\":true}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 7. QUEUE CONFIG API
                    if (urlPath == "/api/queue/config" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string q = data.ContainsKey("queue") ? data["queue"].ToString() : "MainQueue";
                        if (data.ContainsKey("config"))
                        {
                            var cfgDict = data["config"] as Dictionary<string, object>;
                            if (cfgDict != null)
                            {
                                QueueConfig cfg;
                                if (!engine.Queues.TryGetValue(q, out cfg))
                                {
                                    cfg = new QueueConfig();
                                    engine.Queues[q] = cfg;
                                }
                                if (cfgDict.ContainsKey("enabled")) cfg.Enabled = Convert.ToBoolean(cfgDict["enabled"]);
                                if (cfgDict.ContainsKey("startTime")) cfg.StartTime = cfgDict["startTime"].ToString();
                                if (cfgDict.ContainsKey("stopTime")) cfg.StopTime = cfgDict["stopTime"].ToString();
                                if (cfgDict.ContainsKey("maxConcurrent"))
                                {
                                    cfg.MaxConcurrent = Convert.ToInt32(cfgDict["maxConcurrent"]);
                                    engine.SetMaxConcurrentDownloads(cfg.MaxConcurrent);
                                }
                                if (cfgDict.ContainsKey("shutdown")) cfg.Shutdown = Convert.ToBoolean(cfgDict["shutdown"]);
                            }
                        }

                        string ok = "{\"ok\":true}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 8. OPEN FOLDER API
                    if (urlPath.StartsWith("/api/openfolder") && method == "POST")
                    {
                        string targetFolder = engine.DownloadsFolder;
                        if (urlPath.Contains("target=extension"))
                        {
                            targetFolder = Path.Combine(engine.AppDir, "extension");
                        }
                        Process.Start("explorer.exe", targetFolder);

                        string ok = "{\"ok\":true}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 9. EXTENSION HEARTBEAT PING API
                    if (urlPath == "/api/extension/ping")
                    {
                        lastExtensionPing = DateTime.UtcNow;
                        var pingResp = new
                        {
                            ok = true,
                            connected = true,
                            autoCapture = engine.AutoCaptureEnabled
                        };
                        string ok = JsonHelper.Serialize(pingResp);
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 10. DETECT BROWSERS API
                    if (urlPath == "/api/browsers" && method == "GET")
                    {
                        var list = BrowserInstaller.DetectBrowsers();
                        var browserData = new
                        {
                            browsers = list,
                            extensionId = BrowserInstaller.ExtensionId,
                            extensionPath = Path.Combine(engine.AppDir, "extension"),
                            connected = (DateTime.UtcNow - lastExtensionPing).TotalSeconds < 35
                        };
                        string json = JsonHelper.Serialize(browserData);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 11. 1-CLICK INSTALL EXTENSION TO BROWSER API
                    if (urlPath == "/api/extension/install" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string bId = data.ContainsKey("browserId") ? data["browserId"].ToString() : "edge";
                        string bName;
                        string err;
                        bool success = BrowserInstaller.InstallToBrowser(bId, engine.AppDir, out bName, out err);
                        var installData = new
                        {
                            ok = success,
                            browser = bName,
                            error = err
                        };
                        string json = JsonHelper.Serialize(installData);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 12. SETTINGS API (GET / POST)
                    if (urlPath == "/api/settings")
                    {
                        if (method == "POST")
                        {
                            try
                            {
                                var data = JsonHelper.ParseDict(requestBody);
                                if (data != null && data.ContainsKey("autoCapture"))
                                {
                                    engine.AutoCaptureEnabled = Convert.ToBoolean(data["autoCapture"]);
                                    engine.SaveSettings();
                                    if (onNotification != null)
                                    {
                                        onNotification(engine.AutoCaptureEnabled ? "🎯 Auto-Grab activated" : "⏸️ Auto-Grab paused");
                                    }
                                }
                            }
                            catch { }
                        }

                        var resp = new
                        {
                            autoCapture = engine.AutoCaptureEnabled
                        };
                        string json = JsonHelper.Serialize(resp);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    SendHeaders(writer, 404, "Not Found", "text/plain", 0);
                }
            }
            catch { }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private void SendHeaders(StreamWriter writer, int code, string status, string contentType, int length)
        {
            writer.Write(string.Format("HTTP/1.1 {0} {1}\r\n", code, status));
            writer.Write("Access-Control-Allow-Origin: *\r\n");
            writer.Write("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            writer.Write("Access-Control-Allow-Headers: Content-Type\r\n");
            writer.Write("Content-Type: " + contentType + "\r\n");
            writer.Write("Content-Length: " + length + "\r\n");
            writer.Write("Connection: close\r\n");
            writer.Write("\r\n");
            writer.Flush();
        }
    }

    public class ClipboardListener : NativeWindow, IDisposable
    {
        private Action onClipboard;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        public ClipboardListener(Action onClipboard)
        {
            this.onClipboard = onClipboard;
            CreateParams cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            this.CreateHandle(cp);
            AddClipboardFormatListener(this.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                if (onClipboard != null) onClipboard();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            try
            {
                RemoveClipboardFormatListener(this.Handle);
                this.DestroyHandle();
            }
            catch { }
        }
    }

    public static class WindowManager
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        public static bool FocusExistingWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, 512);
                string title = sb.ToString();
                if (!string.IsNullOrEmpty(title) && title.IndexOf("FastDownloader", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        Process proc = Process.GetProcessById((int)pid);
                        string procName = proc.ProcessName.ToLowerInvariant();
                        if (procName != "code" && procName != "explorer" && procName != "devenv")
                        {
                            found = hWnd;
                            return false;
                        }
                    }
                    catch
                    {
                        found = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (found != IntPtr.Zero)
            {
                if (IsIconic(found))
                {
                    ShowWindow(found, SW_RESTORE);
                }
                else
                {
                    ShowWindow(found, SW_SHOW);
                }
                BringWindowToTop(found);
                SetForegroundWindow(found);
                try { SwitchToThisWindow(found, true); } catch { }
                return true;
            }
            return false;
        }
    }

    public class DownloaderAppContext : ApplicationContext
    {
        private EngineManager engine;
        private HttpBridgeServer bridge;
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer schedulerTimer;
        private ClipboardListener clipWindow;
        private string lastClipboardUrl = "";
        private MenuItem autoCaptureMenuItem;

        public DownloaderAppContext()
        {
            engine = new EngineManager();
            engine.StartAria2();

            // Setup System Tray
            Icon appIcon = SystemIcons.Application;
            try
            {
                string icoPath = Path.Combine(engine.AppDir, "app.ico");
                if (File.Exists(icoPath))
                {
                    appIcon = new Icon(icoPath);
                }
            }
            catch { }

            try
            {
                trayIcon = new NotifyIcon
                {
                    Icon = appIcon,
                    Text = "⚡ FastDownloader Pro",
                    Visible = true
                };
                trayIcon.DoubleClick += (s, e) => LaunchEdgeApp();
                trayIcon.BalloonTipClicked += (s, e) => LaunchEdgeApp();
            }
            catch { }

            autoCaptureMenuItem = new MenuItem(engine.AutoCaptureEnabled ? "✔ Auto-Grab (Clipboard & Browser)" : "   Auto-Grab (Clipboard & Browser)");
            autoCaptureMenuItem.Click += (s, e) =>
            {
                engine.AutoCaptureEnabled = !engine.AutoCaptureEnabled;
                engine.SaveSettings();
                UpdateTrayAutoCaptureText();
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, "⚡ FastDownloader",
                        engine.AutoCaptureEnabled ? "🎯 Auto-Grab is now ON" : "⏸️ Auto-Grab is now OFF",
                        ToolTipIcon.Info);
                }
            };

            ContextMenu trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("⚡ Open FastDownloader Dashboard", (s, e) => LaunchEdgeApp());
            trayMenu.MenuItems.Add("📁 Open Downloads Folder", (s, e) => Process.Start("explorer.exe", engine.DownloadsFolder));
            trayMenu.MenuItems.Add(autoCaptureMenuItem);
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Exit FastDownloader", (s, e) =>
            {
                if (trayIcon != null) trayIcon.Visible = false;
                engine.StopAria2();
                if (bridge != null) bridge.Stop();
                Application.Exit();
            });
            if (trayIcon != null) trayIcon.ContextMenu = trayMenu;

            // Setup HTTP Bridge Server
            var syncContext = SynchronizationContext.Current;
            bridge = new HttpBridgeServer(engine, (msg) =>
            {
                if (syncContext != null)
                {
                    syncContext.Post((state) =>
                    {
                        try
                        {
                            UpdateTrayAutoCaptureText();
                            if (trayIcon != null)
                            {
                                trayIcon.ShowBalloonTip(3000, "⚡ FastDownloader", msg, ToolTipIcon.Info);
                            }
                        }
                        catch { }
                    }, null);
                }
                else
                {
                    try
                    {
                        UpdateTrayAutoCaptureText();
                        if (trayIcon != null)
                        {
                            trayIcon.ShowBalloonTip(3000, "⚡ FastDownloader", msg, ToolTipIcon.Info);
                        }
                    }
                    catch { }
                }
            });
            bridge.Start();

            // Setup Clipboard Listener
            try
            {
                clipWindow = new ClipboardListener(OnClipboardUpdated);
            }
            catch { }

            // Background Queue Scheduler Timer
            schedulerTimer = new System.Windows.Forms.Timer();
            schedulerTimer.Interval = 1000;
            schedulerTimer.Tick += (s, e) => CheckSchedulerTick();
            schedulerTimer.Start();

            // Auto-launch Edge in App Mode
            LaunchEdgeApp();
        }

        private void UpdateTrayAutoCaptureText()
        {
            try
            {
                if (autoCaptureMenuItem != null)
                {
                    autoCaptureMenuItem.Text = engine.AutoCaptureEnabled ? "✔ Auto-Grab (Clipboard & Browser)" : "   Auto-Grab (Clipboard & Browser)";
                }
            }
            catch { }
        }

        private void OnClipboardUpdated()
        {
            try
            {
                if (!engine.AutoCaptureEnabled) return;
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim();
                    if (!string.IsNullOrEmpty(text) && text != lastClipboardUrl && IsDownloadableUrl(text))
                    {
                        lastClipboardUrl = text;
                        trayIcon.ShowBalloonTip(4000, "⚡ Link Intercepted!",
                            "Found downloadable link on clipboard:\n" + (text.Length > 60 ? text.Substring(0, 60) + "..." : text) + "\nClick to download!",
                            ToolTipIcon.Info);
                    }
                }
            }
            catch { }
        }

        private bool IsDownloadableUrl(string text)
        {
            if (text.StartsWith("magnet:?xt=", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string lower = text.ToLower();
                string[] exts = new string[] { ".zip", ".exe", ".iso", ".rar", ".7z", ".tar", ".gz", ".mp4", ".mkv", ".pdf", ".dmg", ".msi" };
                foreach (var ext in exts) if (lower.Contains(ext)) return true;
                if (lower.Contains("youtube.com/watch") || lower.Contains("youtu.be/") ||
                    lower.Contains("tiktok.com/") || lower.Contains("twitter.com/") || lower.Contains("x.com/"))
                {
                    return true;
                }
            }
            return false;
        }

        public void LaunchEdgeApp()
        {
            if (WindowManager.FocusExistingWindow()) return;
            LaunchDashboard(engine.AppDir);
        }

        public static void LaunchDashboard(string appDir)
        {
            string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            if (!File.Exists(edgePath)) edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";

            string profileDir = Path.Combine(appDir, ".edge_profile");

            if (File.Exists(edgePath))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = edgePath,
                        Arguments = string.Format("--app=http://127.0.0.1:6801/ --user-data-dir=\"{0}\" --window-size=1180,760 --no-first-run --no-default-browser-check", profileDir),
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                    return;
                }
                catch { }
            }

            // Fallback: Default Browser
            try
            {
                Process.Start("http://127.0.0.1:6801/");
            }
            catch { }
        }

        private void CheckSchedulerTick()
        {
            DateTime now = DateTime.Now;
            string nowTimeStr = now.ToString("HH:mm");

            foreach (var kvp in engine.Queues)
            {
                string queueName = kvp.Key;
                QueueConfig cfg = kvp.Value;
                if (!cfg.Enabled) continue;

                if (nowTimeStr == cfg.StartTime && !cfg.Active)
                {
                    cfg.Active = true;
                    trayIcon.ShowBalloonTip(3000, "⏱️ Scheduler Triggered", queueName + " started automatically!", ToolTipIcon.Info);
                    engine.ResumeAll();
                }

                if (nowTimeStr == cfg.StopTime && cfg.Active)
                {
                    cfg.Active = false;
                    trayIcon.ShowBalloonTip(3000, "⏱️ Scheduler Triggered", queueName + " paused by schedule.", ToolTipIcon.Info);
                    engine.PauseAll();
                }

                // Check auto-shutdown
                if (cfg.Shutdown && cfg.Active)
                {
                    var tasks = engine.GetAriaTasks();
                    bool anyActive = false;
                    foreach (var t in tasks)
                    {
                        if (t.ContainsKey("status") && t["status"].ToString() == "active")
                        {
                            anyActive = true;
                            break;
                        }
                    }

                    if (!anyActive && tasks.Count > 0)
                    {
                        cfg.Shutdown = false;
                        trayIcon.ShowBalloonTip(5000, "Auto-Shutdown", "All downloads finished! PC will shut down in 60 seconds.", ToolTipIcon.Warning);
                        Process.Start("shutdown.exe", "/s /t 60");
                    }
                }
            }
        }
    }

    static class Program
    {
        private static Mutex appMutex;

        [STAThread]
        static void Main()
        {
            bool createdNew = false;
            try
            {
                appMutex = new Mutex(true, "FastDownloader_SingleInstance_Mutex_98a72b", out createdNew);
            }
            catch
            {
                createdNew = false;
            }

            if (!createdNew)
            {
                if (!WindowManager.FocusExistingWindow())
                {
                    DownloaderAppContext.LaunchDashboard(AppDomain.CurrentDomain.BaseDirectory);
                }
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var ctx = new DownloaderAppContext();
                Application.Run(ctx);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString()); } catch { }
            }
            finally
            {
                if (appMutex != null)
                {
                    try { appMutex.ReleaseMutex(); } catch { }
                    appMutex.Close();
                }
            }
        }
    }
}
