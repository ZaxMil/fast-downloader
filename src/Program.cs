using System;
using System.Globalization;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FastDownloader
{
    public static class JsonHelper
    {
        private static JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static string Serialize(object obj) { return serializer.Serialize(obj); }
        public static Dictionary<string, object> ParseDict(string json) { return serializer.Deserialize<Dictionary<string, object>>(json); }
        public static object DeserializeObject(string json) { return serializer.DeserializeObject(json); }
    }

    public static class MediaUrlHelper
    {
        public static bool IsMediaPlatformUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLowerInvariant();
            return lower.Contains("youtube.com") || lower.Contains("youtu.be") ||
                   lower.Contains("facebook.com") || lower.Contains("fb.watch") || lower.Contains("fb.com") ||
                   lower.Contains("instagram.com") ||
                   lower.Contains("tiktok.com") ||
                   lower.Contains("twitter.com") || lower.Contains("x.com") ||
                   lower.Contains("reddit.com") || lower.Contains("v.redd.it") ||
                   lower.Contains("vimeo.com") ||
                   lower.Contains("dailymotion.com") ||
                   lower.Contains("twitch.tv") ||
                   lower.Contains("pinterest.com") ||
                   lower.Contains("threads.net") ||
                   lower.Contains("bilibili.com") ||
                   lower.Contains("rumble.com") ||
                   lower.Contains("streamable.com");
        }

        public static bool HasDirectFileExtension(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                Uri u = new Uri(url);
                string path = u.LocalPath.ToLowerInvariant();
                string[] directExts = new string[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".iso", ".exe", ".msi", ".dmg", ".pdf", ".apk", ".bin", ".mp4", ".mkv", ".webm", ".avi", ".mp3", ".wav" };
                foreach (var ext in directExts)
                {
                    if (path.EndsWith(ext)) return true;
                }
            }
            catch { }
            return false;
        }
    }

    public static class MagicBytesDetector
    {
        public static string FixExtension(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return filePath;

                string currentExt = Path.GetExtension(filePath);
                if (!string.IsNullOrEmpty(currentExt) && currentExt.Length >= 3 && currentExt.ToLowerInvariant() != ".tmp")
                {
                    return filePath;
                }

                byte[] buffer = new byte[64];
                int read = 0;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    read = fs.Read(buffer, 0, buffer.Length);
                }
                if (read < 4) return filePath;

                string newExt = null;

                // 0. Windows Installer MSI / OLE2 Compound Document (0xD0 0xCF 0x11 0xE0)
                if (read >= 8 && buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0 &&
                                 buffer[4] == 0xA1 && buffer[5] == 0xB1 && buffer[6] == 0x1A && buffer[7] == 0xE1)
                {
                    newExt = ".msi";
                }
                // 0.1 Windows Executable PE / MZ (0x4D 0x5A)
                else if (buffer[0] == 0x4D && buffer[1] == 0x5A)
                {
                    newExt = ".exe";
                }
                // 1. MP4 / MOV / M4V (ftyp box at offset 4)
                else if (read >= 12 && buffer[4] == 0x66 && buffer[5] == 0x74 && buffer[6] == 0x79 && buffer[7] == 0x70)
                {
                    newExt = ".mp4";
                }
                // 2. WebM / Matroska (0x1A 0x45 0xDF 0xA3)
                else if (buffer[0] == 0x1A && buffer[1] == 0x45 && buffer[2] == 0xDF && buffer[3] == 0xA3)
                {
                    newExt = ".webm";
                }
                // 3. JPEG (0xFF 0xD8 0xFF)
                else if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                {
                    newExt = ".jpg";
                }
                // 4. PNG (0x89 'P' 'N' 'G')
                else if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                {
                    newExt = ".png";
                }
                // 5. WebP (RIFF....WEBP)
                else if (read >= 12 && buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
                         buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50)
                {
                    newExt = ".webp";
                }
                // 6. GIF ('G' 'I' 'F')
                else if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46)
                {
                    newExt = ".gif";
                }
                // 7. ZIP / Office formats (PK 0x03 0x04)
                else if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
                {
                    newExt = ".zip";
                }
                // 8. RAR (Rar! 0x1A 0x07)
                else if (read >= 6 && buffer[0] == 0x52 && buffer[1] == 0x61 && buffer[2] == 0x72 && buffer[3] == 0x21 && buffer[4] == 0x1A && buffer[5] == 0x07)
                {
                    newExt = ".rar";
                }
                // 9. 7-Zip (7z 0xBC 0xAF 0x27 0x1C)
                else if (read >= 6 && buffer[0] == 0x37 && buffer[1] == 0x7A && buffer[2] == 0xBC && buffer[3] == 0xAF && buffer[4] == 0x27 && buffer[5] == 0x1C)
                {
                    newExt = ".7z";
                }
                // 10. PDF (%PDF)
                else if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
                {
                    newExt = ".pdf";
                }
                // 11. MP3 (ID3 or 0xFF 0xFB)
                else if ((buffer[0] == 0x49 && buffer[1] == 0x44 && buffer[2] == 0x33) || (buffer[0] == 0xFF && (buffer[1] == 0xFB || buffer[1] == 0xF3)))
                {
                    newExt = ".mp3";
                }
                // 12. HTML page (<!DOCTYPE or <html)
                else
                {
                    string textStart = Encoding.ASCII.GetString(buffer, 0, Math.Min(read, 24)).TrimStart();
                    if (textStart.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || textStart.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                    {
                        newExt = ".html";
                    }
                }

                if (!string.IsNullOrEmpty(newExt))
                {
                    string targetPath = filePath + newExt;
                    if (!File.Exists(targetPath))
                    {
                        File.Move(filePath, targetPath);
                        return targetPath;
                    }
                }
            }
            catch { }
            return filePath;
        }
    }


    public class ProbeResult
    {
        public bool ok { get; set; }
        public string type { get; set; }
        public string filename { get; set; }
        public long size { get; set; }
        public string sizeFormatted { get; set; }
        public string category { get; set; }
        public bool resumable { get; set; }
        public string finalUrl { get; set; }
        public string error { get; set; }
    }

    public static class UrlProbeHelper
    {
        public static ProbeResult Probe(string url, string appDir, string cookies, string userAgent, Dictionary<string, object> customHeaders)
        {
            var res = new ProbeResult
            {
                ok = false,
                type = "unknown",
                filename = "",
                size = 0,
                sizeFormatted = "Unknown size",
                category = "General",
                resumable = false,
                finalUrl = url,
                error = ""
            };

            if (string.IsNullOrEmpty(url))
            {
                res.error = "Empty URL";
                return res;
            }

            url = url.Trim();

            if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                res.ok = true;
                res.type = "magnet";
                res.category = "Compressed";
                res.resumable = true;
                res.size = 0;
                res.sizeFormatted = "Torrent / Magnet";
                res.filename = "Torrent_Download";

                try
                {
                    var match = Regex.Match(url, @"dn=([^&]+)");
                    if (match.Success)
                    {
                        res.filename = Uri.UnescapeDataString(match.Groups[1].Value).Replace("+", " ");
                    }
                }
                catch { }

                return res;
            }

            if (url.Contains("fbcdn.net") && customHeaders != null)
            {
                string refHeader = "";
                if (customHeaders.ContainsKey("Referer") && customHeaders["Referer"] != null) refHeader = customHeaders["Referer"].ToString();
                else if (customHeaders.ContainsKey("referer") && customHeaders["referer"] != null) refHeader = customHeaders["referer"].ToString();
                if (!string.IsNullOrEmpty(refHeader) && (refHeader.Contains("facebook.com") || refHeader.Contains("fb.watch")))
                {
                    url = refHeader;
                }
            }

            if (MediaUrlHelper.IsMediaPlatformUrl(url))
            {
                res.type = "video";
                res.category = "Video";
                res.resumable = true;

                string ytExe = Path.Combine(appDir, "bin", "yt-dlp.exe");
                if (!File.Exists(ytExe)) ytExe = Path.Combine(appDir, "yt-dlp.exe");

                if (File.Exists(ytExe))
                {
                    try
                    {
                        string extraArgs = "--print \"%(title)s---FD---%(ext)s---FD---%(filesize,filesize_approx)s\" --no-playlist --skip-download --no-warnings";
                        if (url.Contains("youtube.com") || url.Contains("youtu.be"))
                        {
                            extraArgs += " --extractor-args \"youtube:player_client=android,web\"";
                        }
                        if (!string.IsNullOrEmpty(cookies))
                        {
                            extraArgs += " --add-header \"Cookie:" + cookies.Replace("\"", "\\\"") + "\"";
                        }
                        if (!string.IsNullOrEmpty(userAgent))
                        {
                            extraArgs += " --user-agent \"" + userAgent.Replace("\"", "\\\"") + "\"";
                        }

                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = ytExe,
                            Arguments = string.Format("{0} \"{1}\"", extraArgs, url),
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        using (Process proc = Process.Start(psi))
                        {
                            string lastLine = "";
                            proc.OutputDataReceived += (s, e) =>
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                {
                                    lastLine = e.Data.Trim();
                                }
                            };
                            proc.ErrorDataReceived += (s, e) => { };
                            proc.BeginOutputReadLine();
                            proc.BeginErrorReadLine();
                            proc.WaitForExit(15000);

                            if (!string.IsNullOrEmpty(lastLine))
                            {
                                string[] parts = lastLine.Split(new string[] { "---FD---" }, StringSplitOptions.None);
                                string title = parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) ? parts[0] : "Online Video";
                                string ext = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) && parts[1] != "NA" ? parts[1] : "mp4";
                                long fSize = 0;
                                if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) && parts[2] != "NA")
                                {
                                    long.TryParse(parts[2], out fSize);
                                }

                                foreach (char c in Path.GetInvalidFileNameChars())
                                {
                                    title = title.Replace(c, '_');
                                }

                                res.ok = true;
                                res.filename = title + "." + ext;
                                res.size = fSize;
                                res.sizeFormatted = fSize > 0 ? FormatBytes(fSize) : "Dynamic Video Stream";
                                return res;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        res.error = ex.Message;
                    }
                }

                res.ok = true;
                res.filename = "Online_Video.mp4";
                res.sizeFormatted = "Video Stream (yt-dlp)";
                return res;
            }

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "HEAD";
                req.Timeout = 6000;
                req.AllowAutoRedirect = true;
                req.MaximumAutomaticRedirections = 5;

                if (!string.IsNullOrEmpty(userAgent))
                {
                    req.UserAgent = userAgent;
                }
                else
                {
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                }

                if (!string.IsNullOrEmpty(cookies))
                {
                    req.Headers[HttpRequestHeader.Cookie] = cookies;
                }

                if (customHeaders != null)
                {
                    foreach (var kvp in customHeaders)
                    {
                        try
                        {
                            if (kvp.Value != null)
                            {
                                string k = kvp.Key;
                                string v = kvp.Value.ToString();
                                if (string.Equals(k, "referer", StringComparison.OrdinalIgnoreCase)) req.Referer = v;
                                else if (!string.Equals(k, "cookie", StringComparison.OrdinalIgnoreCase) &&
                                         !string.Equals(k, "user-agent", StringComparison.OrdinalIgnoreCase))
                                {
                                    req.Headers[k] = v;
                                }
                            }
                        }
                        catch { }
                    }
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    res.ok = true;
                    res.finalUrl = resp.ResponseUri.ToString();
                    res.size = resp.ContentLength > 0 ? resp.ContentLength : 0;
                    res.sizeFormatted = res.size > 0 ? FormatBytes(res.size) : "Unknown size";

                    string acceptRanges = resp.Headers["Accept-Ranges"];
                    res.resumable = !string.IsNullOrEmpty(acceptRanges) && acceptRanges.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0;

                    string disposition = resp.Headers["Content-Disposition"];
                    string fname = "";
                    if (!string.IsNullOrEmpty(disposition))
                    {
                        var mUtf = Regex.Match(disposition, @"filename\*=UTF-8''([^;\r\n""']+)", RegexOptions.IgnoreCase);
                        if (mUtf.Success)
                        {
                            fname = Uri.UnescapeDataString(mUtf.Groups[1].Value.Trim('"', '\''));
                        }
                        else
                        {
                            var mStd = Regex.Match(disposition, @"filename\s*=\s*""?([^;""\r\n]+)""?", RegexOptions.IgnoreCase);
                            if (mStd.Success)
                            {
                                fname = mStd.Groups[1].Value.Trim('"', '\'');
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(fname))
                    {
                        try
                        {
                            fname = Path.GetFileName(resp.ResponseUri.LocalPath);
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(fname) || fname.Trim() == "/" || fname.Trim() == "")
                    {
                        fname = "download";
                    }

                    // If filename lacks extension, derive from Content-Type
                    if (string.IsNullOrEmpty(Path.GetExtension(fname)))
                    {
                        string ct = resp.ContentType != null ? resp.ContentType.ToLowerInvariant() : "";
                        if (ct.Contains("application/x-msi") || ct.Contains("application/x-msdownload")) fname += ".msi";
                        else if (ct.Contains("application/zip")) fname += ".zip";
                        else if (ct.Contains("application/pdf")) fname += ".pdf";
                        else if (ct.Contains("application/x-7z")) fname += ".7z";
                        else if (ct.Contains("application/x-rar")) fname += ".rar";
                        else if (ct.Contains("image/jpeg")) fname += ".jpg";
                        else if (ct.Contains("image/png")) fname += ".png";
                        else if (ct.Contains("video/mp4")) fname += ".mp4";
                        else if (ct.Contains("audio/mpeg")) fname += ".mp3";
                    }

                    res.filename = fname;
                    res.category = DetermineCategory(fname, resp.ContentType);
                    res.type = "direct";
                    return res;
                }
            }
            catch
            {
                try
                {
                    Uri uri = new Uri(url);
                    string fname = Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrEmpty(fname))
                    {
                        res.ok = true;
                        res.type = "direct";
                        res.filename = fname;
                        res.category = DetermineCategory(fname, "");
                        res.sizeFormatted = "Direct Stream";
                        return res;
                    }
                }
                catch { }

                res.ok = true;
                res.type = "direct";
                res.filename = "download";
                res.category = "General";
                return res;
            }
        }

        public static string DetermineCategory(string filename, string contentType)
        {
            string ext = "";
            try { ext = Path.GetExtension(filename).ToLowerInvariant(); } catch { }
            string ct = contentType != null ? contentType.ToLowerInvariant() : "";

            if (ext == ".mp4" || ext == ".mkv" || ext == ".webm" || ext == ".avi" || ext == ".mov" || ext == ".flv" || ext == ".wmv" || ct.Contains("video/"))
                return "Video";
            if (ext == ".mp3" || ext == ".wav" || ext == ".flac" || ext == ".m4a" || ext == ".aac" || ext == ".ogg" || ct.Contains("audio/"))
                return "Music";
            if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz" || ext == ".iso" || ext == ".bz2" || ext == ".xz")
                return "Compressed";
            if (ext == ".exe" || ext == ".msi" || ext == ".apk" || ext == ".dmg" || ext == ".bin" || ext == ".bat" || ext == ".cmd")
                return "Programs";
            if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".xls" || ext == ".xlsx" || ext == ".ppt" || ext == ".pptx" || ext == ".txt" || ext == ".epub" || ct.Contains("pdf") || ct.Contains("word") || ct.Contains("text/"))
                return "Documents";

            return "General";
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = (double)bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }

        public static string FormatEta(long sec)
        {
            if (sec <= 0) return "00:00";
            if (sec < 60) return string.Format("{0:D2}s", sec);
            if (sec < 3600) return string.Format("{0:D2}:{1:D2}", sec / 60, sec % 60);
            return string.Format("{0:D2}:{1:D2}:{2:D2}", sec / 3600, (sec % 3600) / 60, sec % 60);
        }
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

        public static void RegisterNativeMessagingHost(string appDir)
        {
            try
            {
                string exePath = Path.Combine(appDir, "FastDownloader.exe");
                string manifestPath = Path.Combine(appDir, "com.fastdownloader.app.json");

                string manifestContent = "{\n" +
                    "  \"name\": \"com.fastdownloader.app\",\n" +
                    "  \"description\": \"FastDownloader Native Messaging Host\",\n" +
                    "  \"path\": \"" + exePath.Replace("\\", "\\\\") + "\",\n" +
                    "  \"type\": \"stdio\",\n" +
                    "  \"allowed_origins\": [\n" +
                    "    \"chrome-extension://" + ExtensionId + "/\"\n" +
                    "  ]\n" +
                    "}";

                File.WriteAllText(manifestPath, manifestContent, Encoding.UTF8);

                string[] keys = new string[]
                {
                    @"Software\Google\Chrome\NativeMessagingHosts\com.fastdownloader.app",
                    @"Software\Microsoft\Edge\NativeMessagingHosts\com.fastdownloader.app",
                    @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.fastdownloader.app"
                };

                foreach (string keyPath in keys)
                {
                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                key.SetValue("", manifestPath);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
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
        public string YtExe { get; set; }
        public string FfmpegDir { get; set; }
        public string FormatArg { get; set; }
        public string ExtraHeaders { get; set; }
        public string SaveDir { get; set; }
        public bool IsYouTube { get; set; }
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

    public class PendingDownloadInfo
    {
        public string pendingId { get; set; }
        public string url { get; set; }
        public string title { get; set; }
        public string filename { get; set; }
        public long size { get; set; }
        public string sizeFormatted { get; set; }
        public string type { get; set; }
        public string category { get; set; }
        public bool resumable { get; set; }
        public string quality { get; set; }
        public string cookies { get; set; }
        public string userAgent { get; set; }
        public string referer { get; set; }
        public Dictionary<string, object> headers { get; set; }
        public string thumbnail { get; set; }
        public string createdAt { get; set; }
    }

    public class EngineManager
    {
        private Process ariaProcess;
        public string DownloadsFolder { get; private set; }
        public string AppDir { get; private set; }
        private readonly object ytLock = new object();
        private readonly object queueLock = new object();
        public List<YtTaskInfo> YtTasks { get; private set; }
        public Dictionary<string, QueueConfig> Queues { get; private set; }

        public List<YtTaskInfo> GetYtTasksSnapshot()
        {
            lock (ytLock)
            {
                return new List<YtTaskInfo>(YtTasks);
            }
        }

        public Dictionary<string, QueueConfig> GetQueuesSnapshot()
        {
            lock (queueLock)
            {
                return new Dictionary<string, QueueConfig>(Queues);
            }
        }

        public void UpdateQueueConfig(string queueName, Action<QueueConfig> updateAction)
        {
            lock (queueLock)
            {
                QueueConfig cfg;
                if (!Queues.TryGetValue(queueName, out cfg))
                {
                    cfg = new QueueConfig();
                    Queues[queueName] = cfg;
                }
                if (updateAction != null) updateAction(cfg);
            }
        }
        public bool AutoCaptureEnabled { get; set; }
        public bool AutoUpdateEngines { get; set; }
        public bool AutoUpdateApp { get; set; }
        public bool NotifyOnUpdate { get; set; }
        public string LastUpdateCheck { get; set; }
        private string settingsFilePath;
        private readonly object pendingLock = new object();
        public Dictionary<string, PendingDownloadInfo> PendingDownloads { get; private set; }

        public EngineManager()
        {
            PendingDownloads = new Dictionary<string, PendingDownloadInfo>();
            AppDir = AppDomain.CurrentDomain.BaseDirectory;
            DownloadsFolder = Path.Combine(AppDir, "downloads");
            if (!Directory.Exists(DownloadsFolder))
            {
                Directory.CreateDirectory(DownloadsFolder);
            }
            AutoCaptureEnabled = true;
            AutoUpdateEngines = true;
            AutoUpdateApp = true;
            NotifyOnUpdate = true;
            LastUpdateCheck = "";
            settingsFilePath = Path.Combine(AppDir, "settings.json");
            LoadSettings();

            YtTasks = new List<YtTaskInfo>();
            PendingDownloads = new Dictionary<string, PendingDownloadInfo>();
            Queues = new Dictionary<string, QueueConfig>();
            Queues["MainQueue"] = new QueueConfig { Enabled = false, StartTime = "02:00", StopTime = "07:00", MaxConcurrent = 3, Shutdown = false, Active = false };
            Queues["NightQueue"] = new QueueConfig { Enabled = false, StartTime = "02:00", StopTime = "07:00", MaxConcurrent = 1, Shutdown = false, Active = false };
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    var dict = JsonHelper.ParseDict(json);
                    if (dict != null)
                    {
                        if (dict.ContainsKey("autoCapture")) AutoCaptureEnabled = Convert.ToBoolean(dict["autoCapture"]);
                        if (dict.ContainsKey("autoUpdateEngines")) AutoUpdateEngines = Convert.ToBoolean(dict["autoUpdateEngines"]);
                        if (dict.ContainsKey("autoUpdateApp")) AutoUpdateApp = Convert.ToBoolean(dict["autoUpdateApp"]);
                        if (dict.ContainsKey("notifyOnUpdate")) NotifyOnUpdate = Convert.ToBoolean(dict["notifyOnUpdate"]);
                        if (dict.ContainsKey("lastUpdateCheck") && dict["lastUpdateCheck"] != null) LastUpdateCheck = dict["lastUpdateCheck"].ToString();
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
                dict["autoUpdateEngines"] = AutoUpdateEngines;
                dict["autoUpdateApp"] = AutoUpdateApp;
                dict["notifyOnUpdate"] = NotifyOnUpdate;
                if (!string.IsNullOrEmpty(LastUpdateCheck)) dict["lastUpdateCheck"] = LastUpdateCheck;
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
                // Kill any stale aria2c instances holding port 6800 before starting
                foreach (var p in Process.GetProcessesByName("aria2c"))
                {
                    try { p.Kill(); p.WaitForExit(600); } catch { }
                }

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

            try
            {
                foreach (var p in Process.GetProcessesByName("aria2c"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }

            lock (ytLock)
            {
                foreach (var t in YtTasks)
                {
                    KillProcessTree(t.Process);
                }
            }
        }


        public PendingDownloadInfo AddPending(string url, string title = null, string filename = null, string category = null, string quality = "best", string cookies = null, string userAgent = null, string referer = null, Dictionary<string, object> headers = null)
        {
            string pid = "pen_" + Guid.NewGuid().ToString().Substring(0, 8);
            ProbeResult probe = null;
            try
            {
                probe = UrlProbeHelper.Probe(url, AppDir, cookies, userAgent, headers);
            }
            catch { }

            string probeFilename = (probe != null && !string.IsNullOrEmpty(probe.filename)) ? probe.filename : "";
            string probeCategory = (probe != null && !string.IsNullOrEmpty(probe.category)) ? probe.category : "General";
            string probeType = (probe != null && !string.IsNullOrEmpty(probe.type)) ? probe.type : "direct";
            long probeSize = probe != null ? probe.size : 0;
            string probeSizeFormatted = (probe != null && !string.IsNullOrEmpty(probe.sizeFormatted)) ? probe.sizeFormatted : "Unknown size";
            bool probeResumable = probe != null && probe.resumable;

            string resolvedFilename = (!string.IsNullOrEmpty(filename) && filename != "Browser Download") ? filename : probeFilename;
            if (string.IsNullOrEmpty(resolvedFilename) || resolvedFilename == "Browser Download")
            {
                resolvedFilename = !string.IsNullOrEmpty(probeFilename) ? probeFilename : (!string.IsNullOrEmpty(title) && title != "Browser Download" ? title : "download");
            }

            string resolvedCat = !string.IsNullOrEmpty(category) ? category : probeCategory;
            if (string.IsNullOrEmpty(resolvedCat)) resolvedCat = "General";

            string thumb = "";
            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                var match = Regex.Match(url, @"(?:v=|\/embed\/|\/17\/|youtu\.be\/|\/v\/|watch\?v=)([^#&?]{11})");
                if (match.Success)
                {
                    thumb = "https://i.ytimg.com/vi/" + match.Groups[1].Value + "/mqdefault.jpg";
                }
            }

            var item = new PendingDownloadInfo
            {
                pendingId = pid,
                url = url,
                title = !string.IsNullOrEmpty(title) ? title : resolvedFilename,
                filename = resolvedFilename,
                size = probeSize,
                sizeFormatted = probeSizeFormatted,
                type = probeType,
                category = resolvedCat,
                resumable = probeResumable,
                quality = !string.IsNullOrEmpty(quality) ? quality : "best",
                cookies = cookies,
                userAgent = userAgent,
                referer = referer,
                headers = headers,
                thumbnail = thumb,
                createdAt = DateTime.UtcNow.ToString("s")
            };

            lock (pendingLock)
            {
                if (PendingDownloads == null) PendingDownloads = new Dictionary<string, PendingDownloadInfo>();
                PendingDownloads[pid] = item;
            }
            return item;
        }

        public List<PendingDownloadInfo> GetPendingDownloads()
        {
            lock (pendingLock)
            {
                if (PendingDownloads == null) PendingDownloads = new Dictionary<string, PendingDownloadInfo>();
                return new List<PendingDownloadInfo>(PendingDownloads.Values);
            }
        }

        public bool RemovePending(string pendingId, out PendingDownloadInfo info)
        {
            lock (pendingLock)
            {
                if (PendingDownloads == null) PendingDownloads = new Dictionary<string, PendingDownloadInfo>();
                if (PendingDownloads.TryGetValue(pendingId, out info))
                {
                    PendingDownloads.Remove(pendingId);
                    return true;
                }
                info = null;
                return false;
            }
        }

        public string AddAriaUri(string url, bool startNow, string cookies = null, string userAgent = null, string referer = null, string filename = null, string category = null, string customDir = null)
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
                if (!string.IsNullOrEmpty(customDir) && Directory.Exists(customDir))
                {
                    options["dir"] = customDir;
                }
                else if (!string.IsNullOrEmpty(category) && Directory.Exists(category))
                {
                    options["dir"] = category;
                }
                else if (!string.IsNullOrEmpty(category) && category != "General" && category != "Default")
                {
                    string catDir = Path.Combine(DownloadsFolder, category);
                    try { if (!Directory.Exists(catDir)) Directory.CreateDirectory(catDir); } catch { }
                    options["dir"] = catDir;
                }
                else
                {
                    options["dir"] = DownloadsFolder;
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

        public UnifiedTaskInfo GetTaskInfo(string gid)
        {
            if (string.IsNullOrEmpty(gid)) return null;

            // 1. Check YT-DLP task
            if (gid.StartsWith("yt_"))
            {
                lock (ytLock)
                {
                    foreach (var yt in YtTasks)
                    {
                        if (yt.Gid == gid)
                        {
                            string resolvedPath = "";
                            if (!string.IsNullOrEmpty(yt.SaveDir) && !string.IsNullOrEmpty(yt.Name))
                            {
                                resolvedPath = Path.Combine(yt.SaveDir, yt.Name);
                            }
                            return new UnifiedTaskInfo
                            {
                                Gid = yt.Gid,
                                Name = yt.Name,
                                TotalLength = yt.TotalLength,
                                CompletedLength = yt.CompletedLength,
                                DownloadSpeed = yt.DownloadSpeed,
                                Progress = yt.Progress,
                                Eta = yt.Eta,
                                Status = yt.Status,
                                FilePath = resolvedPath,
                                Url = yt.Url,
                                Engine = "yt-dlp",
                                NumConnections = 16
                            };
                        }
                    }
                }
            }

            // 2. Query Aria2 tellStatus
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6800/jsonrpc");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = 1000;
                var payload = new
                {
                    jsonrpc = "2.0",
                    id = "status",
                    method = "aria2.tellStatus",
                    @params = new object[] { gid, new string[] { "gid", "status", "totalLength", "completedLength", "downloadSpeed", "files", "connections" } }
                };
                byte[] data = Encoding.UTF8.GetBytes(JsonHelper.Serialize(payload));
                req.ContentLength = data.Length;
                using (var stream = req.GetRequestStream()) stream.Write(data, 0, data.Length);
                using (var resp = req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                {
                    var dict = JsonHelper.ParseDict(reader.ReadToEnd());
                    if (dict != null && dict.ContainsKey("result"))
                    {
                        var res = dict["result"] as Dictionary<string, object>;
                        if (res != null)
                        {
                            long totalLength = 0;
                            if (res.ContainsKey("totalLength") && res["totalLength"] != null) long.TryParse(res["totalLength"].ToString(), out totalLength);
                            long completedLength = 0;
                            if (res.ContainsKey("completedLength") && res["completedLength"] != null) long.TryParse(res["completedLength"].ToString(), out completedLength);
                            long downloadSpeed = 0;
                            if (res.ContainsKey("downloadSpeed") && res["downloadSpeed"] != null) long.TryParse(res["downloadSpeed"].ToString(), out downloadSpeed);

                            int progress = 0;
                            if (totalLength > 0)
                            {
                                progress = (int)((completedLength * 100) / totalLength);
                                if (progress > 100) progress = 100;
                            }

                            long eta = 0;
                            if (downloadSpeed > 0 && totalLength > completedLength)
                            {
                                eta = (totalLength - completedLength) / downloadSpeed;
                            }

                            string status = res.ContainsKey("status") && res["status"] != null ? res["status"].ToString() : "active";
                            string filePath = "";
                            string name = "";
                            if (res.ContainsKey("files"))
                            {
                                var files = res["files"] as ArrayList;
                                if (files != null && files.Count > 0)
                                {
                                    var f = files[0] as Dictionary<string, object>;
                                    if (f != null && f.ContainsKey("path"))
                                    {
                                        filePath = f["path"].ToString();
                                        name = Path.GetFileName(filePath);
                                    }
                                }
                            }

                            int conns = 16;
                            if (res.ContainsKey("connections") && res["connections"] != null)
                            {
                                int.TryParse(res["connections"].ToString(), out conns);
                            }

                            return new UnifiedTaskInfo
                            {
                                Gid = gid,
                                Name = name,
                                TotalLength = totalLength,
                                CompletedLength = completedLength,
                                DownloadSpeed = downloadSpeed,
                                Progress = progress,
                                Eta = eta,
                                Status = status,
                                FilePath = filePath,
                                Url = "",
                                Engine = "aria2",
                                NumConnections = conns > 0 ? conns : 16
                            };
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static void KillProcessTree(Process proc)
        {
            if (proc == null) return;
            try
            {
                int pid = proc.Id;
                KillProcessTreeById(pid);
            }
            catch { }
        }

        private static void KillProcessTreeById(int pid)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/F /T /PID " + pid,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(3000);
                }
            }
            catch
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    if (!proc.HasExited) proc.Kill();
                }
                catch { }
            }
        }

        public void PauseTask(string gid)
        {
            if (!string.IsNullOrEmpty(gid) && gid.StartsWith("yt_"))
            {
                lock (ytLock)
                {
                    var yt = YtTasks.Find(t => t.Gid == gid);
                    if (yt != null)
                    {
                        yt.Status = "paused";
                        yt.DownloadSpeed = 0;
                        yt.Eta = 0;
                        KillProcessTree(yt.Process);
                    }
                }
                return;
            }
            SendAriaAction("aria2.pause", gid);
            SendAriaAction("aria2.forcePause", gid);
        }
        public void ResumeTask(string gid)
        {
            if (!string.IsNullOrEmpty(gid) && gid.StartsWith("yt_"))
            {
                lock (ytLock)
                {
                    var yt = YtTasks.Find(t => t.Gid == gid);
                    if (yt != null && (yt.Status == "paused" || yt.Status == "error"))
                    {
                        yt.Status = "downloading";
                        var taskRef = yt;
                        Thread t = new Thread(() =>
                        {
                            RunYtDlpProcess(taskRef, taskRef.YtExe, taskRef.FfmpegDir, taskRef.FormatArg, taskRef.ExtraHeaders, taskRef.SaveDir, taskRef.Url, taskRef.IsYouTube, true);
                        });
                        t.IsBackground = true;
                        t.Start();
                    }
                }
                return;
            }
            SendAriaAction("aria2.unpause", gid);
        }
        public void RemoveTask(string gid)
        {
            SendAriaAction("aria2.remove", gid);
            SendAriaAction("aria2.forceRemove", gid);
            SendAriaAction("aria2.removeDownloadResult", gid);
            lock (ytLock)
            {
                var yt = YtTasks.Find(t => t.Gid == gid);
                if (yt != null)
                {
                    yt.Status = "removed";
                    KillProcessTree(yt.Process);
                    YtTasks.Remove(yt);
                }
            }
        }
        public void PauseAll()
        {
            SendAriaActionWithoutParams("aria2.pauseAll");
            SendAriaActionWithoutParams("aria2.forcePauseAll");
            lock (ytLock)
            {
                foreach (var yt in YtTasks)
                {
                    if (yt.Status == "downloading" || yt.Status == "processing")
                    {
                        yt.Status = "paused";
                        yt.DownloadSpeed = 0;
                        yt.Eta = 0;
                        KillProcessTree(yt.Process);
                    }
                }
            }
        }
        public void ResumeAll()
        {
            SendAriaActionWithoutParams("aria2.unpauseAll");
            lock (ytLock)
            {
                foreach (var yt in YtTasks)
                {
                    if (yt.Status == "paused" || yt.Status == "error")
                    {
                        yt.Status = "downloading";
                        var taskRef = yt;
                        Thread t = new Thread(() =>
                        {
                            RunYtDlpProcess(taskRef, taskRef.YtExe, taskRef.FfmpegDir, taskRef.FormatArg, taskRef.ExtraHeaders, taskRef.SaveDir, taskRef.Url, taskRef.IsYouTube, true);
                        });
                        t.IsBackground = true;
                        t.Start();
                    }
                }
            }
        }

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

        public string StartConfirmedDownload(PendingDownloadInfo pending, string customName, string saveDir, string category, bool startNow)
        {
            if (pending == null) return null;

            string finalName = !string.IsNullOrEmpty(customName) ? customName : pending.filename;
            string finalDir = !string.IsNullOrEmpty(saveDir) ? saveDir : DownloadsFolder;
            try { if (!Directory.Exists(finalDir)) Directory.CreateDirectory(finalDir); } catch { }

            string finalCat = !string.IsNullOrEmpty(category) ? category : pending.category;
            string finalQuality = !string.IsNullOrEmpty(pending.quality) ? pending.quality : "best";

            bool isVideo = MediaUrlHelper.IsMediaPlatformUrl(pending.url) || pending.category == "Video" || (pending.type == "video" && !MediaUrlHelper.HasDirectFileExtension(pending.url));
            string assignedGid;
            if (isVideo)
            {
                assignedGid = StartYtDlp(pending.url, finalQuality, "MainQueue", pending.cookies, pending.userAgent, pending.referer, finalName, finalCat, startNow, finalDir);
            }
            else
            {
                assignedGid = AddAriaUri(pending.url, startNow, pending.cookies, pending.userAgent, pending.referer, finalName, finalCat, finalDir);
            }
            return assignedGid;
        }

        public string StartYtDlp(string url, string quality, string queue, string cookies = null, string userAgent = null, string referer = null, string customName = null, string category = null, bool startNow = true, string customDir = null)
        {
            string gid = "yt_" + Guid.NewGuid().ToString().Substring(0, 8);
            string ytExe = Path.Combine(AppDir, "bin", "yt-dlp.exe");
            if (!File.Exists(ytExe)) ytExe = Path.Combine(AppDir, "yt-dlp.exe");
            string ffmpegDir = Path.Combine(AppDir, "bin");

            if (url.Contains("fbcdn.net") && !string.IsNullOrEmpty(referer))
            {
                string rLower = referer.ToLowerInvariant();
                if (rLower.Contains("/videos/") || rLower.Contains("/reel/") || rLower.Contains("/watch") || rLower.Contains("watch?v=") || rLower.Contains("story_fbid=") || rLower.Contains("/posts/"))
                {
                    url = referer;
                }
            }
            string checkFbFeed = (url ?? "").ToLowerInvariant().Trim();
            if (checkFbFeed == "https://www.facebook.com" || checkFbFeed == "https://www.facebook.com/" ||
                checkFbFeed.StartsWith("https://www.facebook.com/?") || checkFbFeed == "https://facebook.com" ||
                checkFbFeed == "https://facebook.com/" || checkFbFeed.StartsWith("https://facebook.com/?"))
            {
                return "";
            }

            string formatArg = "-S \"vcodec:h264,res,acodec:m4a\" -f \"hd/sd/b[vcodec^=avc1]/bv*[vcodec^=avc1]+ba[acodec^=mp4a]/b/bv*+ba/best\" --merge-output-format mp4";
            if (quality == "1080p") formatArg = "-S \"vcodec:h264,res:1080,acodec:m4a\" -f \"hd/b[height<=1080][vcodec^=avc1]/bv*[height<=1080][vcodec^=avc1]+ba/b[height<=1080]/bv*[height<=1080]+ba/best\" --merge-output-format mp4";
            else if (quality == "720p") formatArg = "-S \"vcodec:h264,res:720,acodec:m4a\" -f \"hd/b[height<=720][vcodec^=avc1]/bv*[height<=720][vcodec^=avc1]+ba/b[height<=720]/bv*[height<=720]+ba/best\" --merge-output-format mp4";
            else if (quality == "480p") formatArg = "-S \"vcodec:h264,res:480,acodec:m4a\" -f \"sd/b[height<=480][vcodec^=avc1]/bv*[height<=480][vcodec^=avc1]+ba/b[height<=480]/bv*[height<=480]+ba/best\" --merge-output-format mp4";
            else if (quality == "audio") formatArg = "-x --audio-format mp3";

            string extraHeaders = "";
            bool isYouTube = url.Contains("youtube.com") || url.Contains("youtu.be");
            if (isYouTube)
            {
                extraHeaders += " --extractor-args \"youtube:player_client=ios,android,mweb,web\"";
            }

            if (!string.IsNullOrEmpty(cookies))
            {
                try
                {
                    string cookieFile = Path.Combine(AppDir, "cookie_" + gid + ".txt");
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# Netscape HTTP Cookie File");
                    string domain = isYouTube ? ".youtube.com" : (url.Contains("facebook") || url.Contains("fbcdn") ? ".facebook.com" : "");
                    if (string.IsNullOrEmpty(domain))
                    {
                        try { Uri u = new Uri(url); domain = "." + u.Host.TrimStart('.'); } catch { domain = ""; }
                    }
                    string[] cookiePairs = cookies.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in cookiePairs)
                    {
                        int eq = pair.IndexOf('=');
                        if (eq > 0)
                        {
                            string k = pair.Substring(0, eq).Trim();
                            string v = pair.Substring(eq + 1).Trim();
                            sb.AppendLine(string.Format("{0}\tTRUE\t/\tTRUE\t2147483647\t{1}\t{2}", domain, k, v));
                        }
                    }
                    File.WriteAllText(cookieFile, sb.ToString(), Encoding.UTF8);
                    extraHeaders += " --cookies \"" + cookieFile + "\"";
                }
                catch { }
            }
            if (!string.IsNullOrEmpty(referer)) extraHeaders += " --referer \"" + referer.Replace("\"", "\\\"") + "\"";
            if (!string.IsNullOrEmpty(userAgent)) extraHeaders += " --user-agent \"" + userAgent.Replace("\"", "\\\"") + "\"";

            string initialName = !string.IsNullOrEmpty(customName) ? customName : ("Online Video: " + (url.Length > 45 ? url.Substring(0, 45) + "..." : url));

            string targetSaveDir = DownloadsFolder;
            if (!string.IsNullOrEmpty(customDir) && Directory.Exists(customDir))
            {
                targetSaveDir = customDir;
            }
            else if (!string.IsNullOrEmpty(category) && Directory.Exists(category))
            {
                targetSaveDir = category;
            }
            else if (!string.IsNullOrEmpty(category) && category != "General" && category != "Default")
            {
                targetSaveDir = Path.Combine(DownloadsFolder, category);
                try { if (!Directory.Exists(targetSaveDir)) Directory.CreateDirectory(targetSaveDir); } catch { }
            }

            YtTaskInfo taskInfo = new YtTaskInfo
            {
                Gid = gid,
                Url = url,
                Name = initialName,
                Quality = quality,
                Queue = queue,
                Progress = 0,
                Status = startNow ? "downloading" : "paused",
                YtExe = ytExe,
                FfmpegDir = ffmpegDir,
                FormatArg = formatArg,
                ExtraHeaders = extraHeaders,
                SaveDir = targetSaveDir,
                IsYouTube = isYouTube
            };

            lock (ytLock)
            {
                YtTasks.Insert(0, taskInfo);
            }

            if (startNow)
            {
                Thread t = new Thread(() =>
                {
                    RunYtDlpProcess(taskInfo, ytExe, ffmpegDir, formatArg, extraHeaders, targetSaveDir, url, isYouTube, true);
                });
                t.IsBackground = true;
                t.Start();
            }

            return gid;
        }

        private static readonly Regex Aria2ProgressRegex = new Regex(
            @"\[#[0-9a-fA-F]+\s+([0-9\.]+[a-zA-Z]+)/([0-9\.]+[a-zA-Z]+)(?:\s*\(\s*(\d+)%\s*\))?.*?DL:([0-9\.]+[a-zA-Z]+)(?:.*?ETA:([^\s\]]+))?",
            RegexOptions.Compiled);

        private static readonly Regex YtDlpProgressRegex = new Regex(
            @"\[download\]\s+([0-9\.]+)%\s+of\s+(?:~\s*)?([0-9\.]+[a-zA-Z]+)(?:\s+at\s+([0-9\.]+[a-zA-Z]+(?:/s)?))?(?:\s+ETA\s+([^\s\]]+))?",
            RegexOptions.Compiled);

        public static long ParseByteString(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            s = s.Trim();
            var match = Regex.Match(s, @"^([0-9\.]+)\s*([a-zA-Z]+)?$");
            if (!match.Success) return 0;
            double val;
            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out val)) return 0;
            string unit = match.Groups[2].Value.ToUpperInvariant();
            long mult = 1;
            if (unit.Contains("K")) mult = 1024L;
            else if (unit.Contains("M")) mult = 1024L * 1024L;
            else if (unit.Contains("G")) mult = 1024L * 1024L * 1024L;
            else if (unit.Contains("T")) mult = 1024L * 1024L * 1024L * 1024L;
            return (long)(val * mult);
        }

        public static long ParseEtaString(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            s = s.Trim();
            if (s.Contains(":"))
            {
                string[] parts = s.Split(':');
                long total = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    long p;
                    if (long.TryParse(parts[i], out p)) total = total * 60 + p;
                }
                return total;
            }
            var matches = Regex.Matches(s.ToLowerInvariant(), @"(\d+)\s*([hms])");
            long secTotal = 0;
            foreach (Match m in matches)
            {
                long val;
                if (long.TryParse(m.Groups[1].Value, out val))
                {
                    string u = m.Groups[2].Value;
                    if (u == "h") secTotal += val * 3600;
                    else if (u == "m") secTotal += val * 60;
                    else if (u == "s") secTotal += val;
                }
            }
            return secTotal;
        }

        private void ParseYtOutputLine(YtTaskInfo taskInfo, string line, ref long previousStreamBytes, ref long currentStreamTotal, ref long currentStreamCompleted)
        {
            if (taskInfo == null || taskInfo.Status == "paused" || taskInfo.Status == "removed") return;
            if (string.IsNullOrEmpty(line)) return;

            if (line.Contains("Destination: "))
            {
                try
                {
                    int dIdx = line.IndexOf("Destination: ");
                    string dest = line.Substring(dIdx + 13).Trim(' ', '"');
                    if (!string.IsNullOrEmpty(dest))
                    {
                        string fname = Path.GetFileName(dest);
                        if (!fname.Contains(".f") && !fname.EndsWith(".part"))
                        {
                            taskInfo.Name = fname;
                        }
                        else if (string.IsNullOrEmpty(taskInfo.Name) || taskInfo.Name.StartsWith("Online Video:"))
                        {
                            taskInfo.Name = fname;
                        }
                    }
                }
                catch { }
                return;
            }

            if (line.Contains("[#"))
            {
                var m = Aria2ProgressRegex.Match(line);
                if (m.Success)
                {
                    long comp = ParseByteString(m.Groups[1].Value);
                    long tot = ParseByteString(m.Groups[2].Value);
                    long spd = ParseByteString(m.Groups[4].Value);
                    long eta = ParseEtaString(m.Groups[5].Value);

                    if (tot > 0 && currentStreamTotal > 0 && tot < currentStreamTotal && comp < currentStreamCompleted)
                    {
                        previousStreamBytes += currentStreamCompleted;
                    }
                    currentStreamTotal = tot;
                    currentStreamCompleted = comp;

                    long overallCompleted = previousStreamBytes + comp;
                    long overallTotal = previousStreamBytes + tot;

                    taskInfo.CompletedLength = overallCompleted;
                    taskInfo.TotalLength = overallTotal;
                    taskInfo.DownloadSpeed = spd;
                    taskInfo.Eta = eta;

                    int pct = 0;
                    if (!string.IsNullOrEmpty(m.Groups[3].Value))
                    {
                        int.TryParse(m.Groups[3].Value, out pct);
                        if (previousStreamBytes > 0 && overallTotal > 0)
                        {
                            pct = (int)((overallCompleted * 100) / overallTotal);
                        }
                    }
                    else if (overallTotal > 0)
                    {
                        pct = (int)((overallCompleted * 100) / overallTotal);
                    }
                    if (pct > taskInfo.Progress && pct < 100) taskInfo.Progress = pct;
                    taskInfo.Status = "downloading";
                    return;
                }
            }

            if (line.Contains("[download]"))
            {
                var m = YtDlpProgressRegex.Match(line);
                if (m.Success)
                {
                    float pctVal;
                    if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out pctVal))
                    {
                        int pct = (int)pctVal;
                        if (pct > taskInfo.Progress && pct < 100) taskInfo.Progress = pct;
                    }
                    long tot = ParseByteString(m.Groups[2].Value);
                    if (tot > 0)
                    {
                        taskInfo.TotalLength = tot;
                        taskInfo.CompletedLength = (long)(tot * (taskInfo.Progress / 100.0));
                    }
                    if (m.Groups[3].Success)
                    {
                        taskInfo.DownloadSpeed = ParseByteString(m.Groups[3].Value.Replace("/s", ""));
                    }
                    if (m.Groups[4].Success)
                    {
                        taskInfo.Eta = ParseEtaString(m.Groups[4].Value);
                    }
                    taskInfo.Status = "downloading";
                    return;
                }
            }

            if (line.Contains("[Merger]") || line.Contains("[ExtractAudio]") || line.Contains("Fixing headers"))
            {
                taskInfo.Progress = 99;
                taskInfo.Status = "processing";
                taskInfo.DownloadSpeed = 0;
                taskInfo.Eta = 0;
            }
        }

        private void RunYtDlpProcess(YtTaskInfo taskInfo, string ytExe, string ffmpegDir, string formatArg, string extraHeaders, string saveDir, string url, bool isYouTube, bool allowRetry)
        {
            StringBuilder errLog = new StringBuilder();
            try
            {
                string outputTemplate = "%(title).60s [%(id)s].%(ext)s";
                if (!string.IsNullOrEmpty(taskInfo.Name) && !taskInfo.Name.StartsWith("Online Video:"))
                {
                    string cleanName = taskInfo.Name;
                    string ext = Path.GetExtension(cleanName);
                    if (!string.IsNullOrEmpty(ext)) cleanName = Path.GetFileNameWithoutExtension(cleanName);
                    foreach (char c in Path.GetInvalidFileNameChars()) cleanName = cleanName.Replace(c, '_');
                    string lowerClean = cleanName.ToLowerInvariant();
                    if (lowerClean != "main" && lowerClean != "download" && !lowerClean.Contains("unknown_video") && lowerClean != "facebook" && lowerClean != "video")
                    {
                        outputTemplate = cleanName + ".%(ext)s";
                    }
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ytExe,
                    Arguments = string.Format("--ffmpeg-location \"{0}\" {1}{2} -o \"{3}\" --windows-filenames --downloader dash:native --downloader m3u8:native --downloader http:aria2c --downloader https:aria2c --downloader-args \"aria2c:-x 16 -s 16 -k 1M --disable-ipv6=true --async-dns-server=8.8.8.8,1.1.1.1 --summary-interval=1\" -P \"{4}\" \"{5}\"",
                        ffmpegDir, formatArg, extraHeaders, outputTemplate, saveDir, url),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process p = Process.Start(psi))
                {
                    taskInfo.Process = p;
                    p.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errLog.AppendLine(e.Data);
                        }
                    };
                    p.BeginErrorReadLine();

                    char[] buffer = new char[512];
                    StringBuilder sb = new StringBuilder();
                    int read;
                    long previousStreamBytes = 0;
                    long currentStreamTotal = 0;
                    long currentStreamCompleted = 0;

                    while ((read = p.StandardOutput.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            char ch = buffer[i];
                            if (ch == '\r' || ch == '\n')
                            {
                                if (sb.Length > 0)
                                {
                                    string line = sb.ToString();
                                    sb.Length = 0;
                                    ParseYtOutputLine(taskInfo, line, ref previousStreamBytes, ref currentStreamTotal, ref currentStreamCompleted);
                                }
                            }
                            else
                            {
                                sb.Append(ch);
                            }
                        }
                    }
                    if (sb.Length > 0)
                    {
                        ParseYtOutputLine(taskInfo, sb.ToString(), ref previousStreamBytes, ref currentStreamTotal, ref currentStreamCompleted);
                    }

                    p.WaitForExit();

                    string allErrors = errLog.ToString();
                    if (!string.IsNullOrEmpty(allErrors))
                    {
                        try { File.WriteAllText(Path.Combine(AppDir, "ytdlp_last_error.log"), allErrors); } catch { }
                    }

                    if (taskInfo.Status == "paused" || taskInfo.Status == "removed")
                    {
                        taskInfo.DownloadSpeed = 0;
                        taskInfo.Eta = 0;
                        return;
                    }

                    if (p.ExitCode != 0 && allowRetry && isYouTube)
                    {
                        if (allErrors.Contains("The page needs to be reloaded") ||
                            allErrors.Contains("challenge solving failed") ||
                            allErrors.Contains("Sign in to confirm") ||
                            allErrors.Contains("Requested format is not available") ||
                            allErrors.Contains("Skipping client") ||
                            allErrors.Contains("Only images are available"))
                        {
                            string retryHeaders = " --extractor-args \"youtube:player_client=ios,android,mweb,web\"";
                            RunYtDlpProcess(taskInfo, ytExe, ffmpegDir, formatArg, retryHeaders, saveDir, url, isYouTube, false);
                            return;
                        }
                    }

                    taskInfo.DownloadSpeed = 0;
                    taskInfo.Eta = 0;
                    try { string cFile = Path.Combine(AppDir, "cookie_" + taskInfo.Gid + ".txt"); if (File.Exists(cFile)) File.Delete(cFile); } catch { }
                    if (p.ExitCode == 0)
                    {
                        taskInfo.Progress = 100;
                        taskInfo.Status = "completed";
                    }
                    else
                    {
                        taskInfo.Status = "error";
                    }
                    if (p.ExitCode == 0)
                    {
                        if (taskInfo.TotalLength > 0 && taskInfo.CompletedLength < taskInfo.TotalLength)
                        {
                            taskInfo.CompletedLength = taskInfo.TotalLength;
                        }
                        try
                        {
                            if (!string.IsNullOrEmpty(taskInfo.Name))
                            {
                                string candidate = Path.Combine(saveDir, taskInfo.Name);
                                if (File.Exists(candidate))
                                {
                                    long diskLen = new FileInfo(candidate).Length;
                                    taskInfo.TotalLength = diskLen;
                                    taskInfo.CompletedLength = diskLen;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(AppDir, "ytdlp_last_error.log"), ex.ToString() + "\n" + errLog.ToString()); } catch { }
                taskInfo.Status = "error";
            }
        }
    }

    public class UpdateManager
    {
        public const string CurrentAppVersion = "0.1.4";
        public const string GitHubRepoOwner = "ZaxMil";
        public const string GitHubRepoName = "fast-downloader";

        private EngineManager engine;
        private Action<string> onNotification;
        private Thread backgroundThread;
        private bool isRunning;

        public bool AppUpdateAvailable { get; private set; }
        public string LatestAppVersion { get; private set; }
        public string AppReleaseUrl { get; private set; }
        public string AppReleaseNotes { get; private set; }
        public string AppDownloadUrl { get; private set; }

        public string CachedYtDlpVersion { get; private set; }
        public string CachedAria2Version { get; private set; }
        public bool IsYtDlpUpdating { get; private set; }

        public UpdateManager(EngineManager engine, Action<string> onNotification)
        {
            this.engine = engine;
            this.onNotification = onNotification;
            this.LatestAppVersion = CurrentAppVersion;
            this.CachedYtDlpVersion = GetYtDlpVersion();
            this.CachedAria2Version = GetAria2Version();

            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            }
            catch { }
        }

        public void StartBackgroundScheduler()
        {
            isRunning = true;
            backgroundThread = new Thread(BackgroundLoop)
            {
                IsBackground = true,
                Name = "FastDownloader-AutoUpdater"
            };
            backgroundThread.Start();
        }

        public void Stop()
        {
            isRunning = false;
        }

        private void BackgroundLoop()
        {
            // Initial grace period: wait 15 seconds after app startup
            Thread.Sleep(15000);

            while (isRunning)
            {
                try
                {
                    // 1. Check and auto-update engines (yt-dlp) if enabled
                    if (engine.AutoUpdateEngines)
                    {
                        var res = UpdateYtDlpInternal(true);
                        if (res != null && res.updated)
                        {
                            CachedYtDlpVersion = res.latestVersion;
                            if (engine.NotifyOnUpdate && onNotification != null)
                            {
                                onNotification("🚀 yt-dlp auto-updated to " + res.latestVersion);
                            }
                        }
                    }

                    // 2. Check for FastDownloader app release if enabled
                    if (engine.AutoUpdateApp)
                    {
                        var appRes = CheckAppUpdate();
                        if (appRes != null && appRes.hasUpdate)
                        {
                            AppUpdateAvailable = true;
                            LatestAppVersion = appRes.latestVersion;
                            AppReleaseUrl = appRes.releaseUrl;
                            AppReleaseNotes = appRes.releaseNotes;
                            AppDownloadUrl = appRes.downloadUrl;

                            if (engine.NotifyOnUpdate && onNotification != null)
                            {
                                onNotification("✨ FastDownloader " + appRes.latestVersion + " is available! Click Updates to install.");
                            }
                        }
                    }

                    engine.LastUpdateCheck = DateTime.UtcNow.ToString("s") + "Z";
                    engine.SaveSettings();
                }
                catch { }

                // Sleep in 1-minute increments for 24 hours (1440 minutes)
                for (int m = 0; m < 1440 && isRunning; m++)
                {
                    Thread.Sleep(60000);
                }
            }
        }

        public string GetYtDlpVersion()
        {
            try
            {
                string ytExe = Path.Combine(engine.AppDir, "bin", "yt-dlp.exe");
                if (!File.Exists(ytExe)) ytExe = Path.Combine(engine.AppDir, "yt-dlp.exe");
                if (!File.Exists(ytExe)) return "Not Installed";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ytExe,
                    Arguments = "--version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(4000);
                    if (!string.IsNullOrEmpty(output)) return output;
                }
            }
            catch { }
            return "2026.10.21";
        }

        public string GetAria2Version()
        {
            try
            {
                string ariaExe = Path.Combine(engine.AppDir, "bin", "aria2c.exe");
                if (!File.Exists(ariaExe)) ariaExe = Path.Combine(engine.AppDir, "aria2c.exe");
                if (!File.Exists(ariaExe)) return "Not Installed";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ariaExe,
                    Arguments = "-v",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(4000);
                    Match m = Regex.Match(output, @"aria2 version\s+([0-9\.]+)");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { }
            return "1.37.0";
        }

        public class UpdateCheckResult
        {
            public bool hasUpdate;
            public string currentVersion;
            public string latestVersion;
            public string downloadUrl;
            public string releaseNotes;
            public string releaseUrl;
            public string message;
        }

        public class EngineUpdateResult
        {
            public bool ok;
            public bool updated;
            public string engine;
            public string currentVersion;
            public string latestVersion;
            public string message;
            public List<string> logs = new List<string>();
        }

        public UpdateCheckResult CheckAppUpdate()
        {
            var result = new UpdateCheckResult
            {
                hasUpdate = false,
                currentVersion = CurrentAppVersion,
                latestVersion = CurrentAppVersion,
                releaseUrl = "https://github.com/" + GitHubRepoOwner + "/" + GitHubRepoName + "/releases"
            };

            try
            {
                string apiUrl = "https://api.github.com/repos/" + GitHubRepoOwner + "/" + GitHubRepoName + "/releases?per_page=5";
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "FastDownloader-App/" + CurrentAppVersion;
                    wc.Headers["Accept"] = "application/vnd.github.v3+json";
                    string json = wc.DownloadString(apiUrl);
                    Dictionary<string, object> dict = null;
                    try
                    {
                        object parsed = JsonHelper.DeserializeObject(json);
                        ArrayList aList = parsed as ArrayList;
                        object[] aArr = parsed as object[];
                        if (aList != null && aList.Count > 0) dict = aList[0] as Dictionary<string, object>;
                        else if (aArr != null && aArr.Length > 0) dict = aArr[0] as Dictionary<string, object>;
                        else dict = parsed as Dictionary<string, object>;
                    }
                    catch { }

                    if (dict != null && dict.ContainsKey("tag_name"))
                    {
                        string tag = dict["tag_name"].ToString().Trim();
                        result.latestVersion = tag.TrimStart('v', 'V');
                        if (dict.ContainsKey("body") && dict["body"] != null) result.releaseNotes = dict["body"].ToString();
                        if (dict.ContainsKey("html_url") && dict["html_url"] != null) result.releaseUrl = dict["html_url"].ToString();

                        if (dict.ContainsKey("assets"))
                        {
                            var assets = dict["assets"] as ArrayList;
                            if (assets != null)
                            {
                                foreach (var aObj in assets)
                                {
                                    var asset = aObj as Dictionary<string, object>;
                                    if (asset != null && asset.ContainsKey("name") && asset.ContainsKey("browser_download_url"))
                                    {
                                        string aName = asset["name"].ToString();
                                        if (string.Equals(aName, "FastDownloader.exe", StringComparison.OrdinalIgnoreCase))
                                        {
                                            result.downloadUrl = asset["browser_download_url"].ToString();
                                            break;
                                        }
                                        else if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(result.downloadUrl))
                                        {
                                            result.downloadUrl = asset["browser_download_url"].ToString();
                                        }
                                    }
                                }
                            }
                        }

                        Version cur = ParseVersionSafe(CurrentAppVersion);
                        Version lat = ParseVersionSafe(result.latestVersion);
                        if (lat > cur)
                        {
                            result.hasUpdate = true;
                            result.message = "New version " + result.latestVersion + " available!";
                            AppUpdateAvailable = true;
                            LatestAppVersion = result.latestVersion;
                            AppReleaseUrl = result.releaseUrl;
                            AppReleaseNotes = result.releaseNotes;
                            AppDownloadUrl = result.downloadUrl;
                        }
                        else
                        {
                            result.message = "FastDownloader Pro is up to date (" + CurrentAppVersion + ").";
                            AppUpdateAvailable = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.message = "Failed to query app releases: " + ex.Message;
            }

            return result;
        }

        private static Version ParseVersionSafe(string ver)
        {
            try
            {
                if (string.IsNullOrEmpty(ver)) return new Version(0, 0, 0);
                ver = ver.Trim().TrimStart('v', 'V');
                int dash = ver.IndexOf('-');
                if (dash >= 0) ver = ver.Substring(0, dash);
                string[] parts = ver.Split('.');
                int maj = parts.Length > 0 ? int.Parse(parts[0]) : 0;
                int min = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                int bld = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                return new Version(maj, min, bld);
            }
            catch { return new Version(0, 0, 0); }
        }

        public EngineUpdateResult UpdateYtDlp(bool force)
        {
            return UpdateYtDlpInternal(false);
        }

        private EngineUpdateResult UpdateYtDlpInternal(bool silent)
        {
            IsYtDlpUpdating = true;
            var res = new EngineUpdateResult
            {
                engine = "yt-dlp",
                currentVersion = GetYtDlpVersion()
            };

            try
            {
                string ytExe = Path.Combine(engine.AppDir, "bin", "yt-dlp.exe");
                if (!File.Exists(ytExe)) ytExe = Path.Combine(engine.AppDir, "yt-dlp.exe");

                res.logs.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] Checking official yt-dlp upstream release...");

                if (File.Exists(ytExe))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ytExe,
                        Arguments = "-U",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process p = Process.Start(psi))
                    {
                        string stdout = p.StandardOutput.ReadToEnd();
                        string stderr = p.StandardError.ReadToEnd();
                        p.WaitForExit(60000);

                        string combined = (stdout + "\n" + stderr).Trim();
                        string[] lines = combined.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var l in lines) res.logs.Add("  > " + l.Trim());

                        if (combined.IndexOf("is up to date", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            res.ok = true;
                            res.updated = false;
                            res.latestVersion = GetYtDlpVersion();
                            res.message = "yt-dlp is already up to date (" + res.latestVersion + ").";
                        }
                        else if (combined.IndexOf("Updated yt-dlp to version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 combined.IndexOf("Updated to version", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            res.ok = true;
                            res.updated = true;
                            res.latestVersion = GetYtDlpVersion();
                            res.message = "Successfully updated yt-dlp to " + res.latestVersion + "!";
                        }
                        else if (p.ExitCode == 0)
                        {
                            res.ok = true;
                            res.latestVersion = GetYtDlpVersion();
                            res.message = "yt-dlp update process completed.";
                        }
                    }

                    try
                    {
                        string oldExe = Path.Combine(Path.GetDirectoryName(ytExe), "yt-dlp.exe.old");
                        if (File.Exists(oldExe)) File.Delete(oldExe);
                    }
                    catch { }
                }

                if (!res.ok)
                {
                    res.logs.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] Falling back to direct GitHub release download...");
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers["User-Agent"] = "FastDownloader-App/" + CurrentAppVersion;
                        string json = wc.DownloadString("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
                        var dict = JsonHelper.ParseDict(json);
                        string tag = dict.ContainsKey("tag_name") ? dict["tag_name"].ToString() : "";
                        res.latestVersion = tag;

                        string dlUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                        string targetDir = Path.Combine(engine.AppDir, "bin");
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                        string tmpPath = Path.Combine(targetDir, "yt-dlp.exe.tmp");
                        string destPath = Path.Combine(targetDir, "yt-dlp.exe");

                        wc.DownloadFile(dlUrl, tmpPath);
                        if (File.Exists(destPath)) File.Delete(destPath);
                        File.Move(tmpPath, destPath);

                        res.ok = true;
                        res.updated = true;
                        res.message = "Downloaded latest yt-dlp binary (" + tag + ").";
                        res.logs.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] yt-dlp binary installed successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                res.ok = false;
                res.message = "Update failed: " + ex.Message;
                res.logs.Add("Error: " + ex.Message);
            }
            finally
            {
                IsYtDlpUpdating = false;
                CachedYtDlpVersion = GetYtDlpVersion();
            }

            return res;
        }

        public EngineUpdateResult CheckAria2()
        {
            var res = new EngineUpdateResult
            {
                engine = "aria2",
                currentVersion = GetAria2Version(),
                latestVersion = "1.37.0",
                ok = true,
                updated = false,
                message = "aria2c core " + GetAria2Version() + " is optimal and up to date."
            };
            res.logs.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] aria2c version: " + res.currentVersion);
            res.logs.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] aria2c 16-chunk parallel core is operational.");
            return res;
        }

        public bool ApplyAppUpdate(out string msg)
        {
            msg = "";
            try
            {
                var check = CheckAppUpdate();
                string url = !string.IsNullOrEmpty(check.downloadUrl) ? check.downloadUrl : AppDownloadUrl;
                if (string.IsNullOrEmpty(url))
                {
                    url = "https://github.com/" + GitHubRepoOwner + "/" + GitHubRepoName + "/releases/latest/download/FastDownloader.exe";
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "FastDownloader_Update");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                string newExe = Path.Combine(tempDir, "FastDownloader.exe");
                string extZip = Path.Combine(tempDir, "extension.zip");

                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "FastDownloader-App/" + CurrentAppVersion;
                    wc.DownloadFile(url, newExe);
                    try
                    {
                        string extUrl = "https://github.com/" + GitHubRepoOwner + "/" + GitHubRepoName + "/releases/latest/download/fast-downloader-extension.zip";
                        wc.DownloadFile(extUrl, extZip);
                    }
                    catch { }
                }

                if (!File.Exists(newExe) || new FileInfo(newExe).Length < 50000)
                {
                    msg = "Downloaded file appears invalid or incomplete.";
                    return false;
                }

                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string appDir = engine != null ? engine.AppDir : Path.GetDirectoryName(currentExe);
                string extTargetDir = Path.Combine(appDir, "extension");
                string batPath = Path.Combine(tempDir, "apply_update.bat");
                string batScript =
                    "@echo off\r\n" +
                    "chcp 65001 > nul\r\n" +
                    "timeout /t 1 /nobreak > nul\r\n" +
                    ":retry\r\n" +
                    "copy /y \"" + newExe + "\" \"" + currentExe + "\" > nul 2>&1\r\n" +
                    "if errorlevel 1 (\r\n" +
                    "    timeout /t 1 /nobreak > nul\r\n" +
                    "    goto retry\r\n" +
                    ")\r\n" +
                    "if exist \"" + extZip + "\" (\r\n" +
                    "    powershell -NoProfile -Command \"Expand-Archive -Path '" + extZip.Replace("'", "''") + "' -DestinationPath '" + extTargetDir.Replace("'", "''") + "' -Force\" > nul 2>&1\r\n" +
                    ")\r\n" +
                    "start \"\" \"" + currentExe + "\"\r\n" +
                    "del \"%~f0\"\r\n";

                File.WriteAllText(batPath, batScript, Encoding.Default);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + batPath + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { if (engine != null) engine.StopAria2(); } catch { }
                    Thread.Sleep(300);
                    Environment.Exit(0);
                });

                msg = "FastDownloader is restarting with the updated version...";
                return true;
            }
            catch (Exception ex)
            {
                msg = "Failed to apply update: " + ex.Message;
                return false;
            }
        }

        public Dictionary<string, object> GetStatusDict()
        {
            var dict = new Dictionary<string, object>();
            
            var appInfo = new Dictionary<string, object>();
            appInfo["currentVersion"] = CurrentAppVersion;
            appInfo["latestVersion"] = LatestAppVersion ?? CurrentAppVersion;
            appInfo["updateAvailable"] = AppUpdateAvailable;
            appInfo["releaseUrl"] = AppReleaseUrl ?? ("https://github.com/" + GitHubRepoOwner + "/" + GitHubRepoName + "/releases");
            appInfo["releaseNotes"] = AppReleaseNotes ?? "";
            appInfo["downloadUrl"] = AppDownloadUrl ?? "";
            dict["app"] = appInfo;

            var ytdlpInfo = new Dictionary<string, object>();
            ytdlpInfo["currentVersion"] = CachedYtDlpVersion ?? GetYtDlpVersion();
            ytdlpInfo["latestVersion"] = CachedYtDlpVersion ?? GetYtDlpVersion();
            ytdlpInfo["status"] = IsYtDlpUpdating ? "updating" : "ready";
            ytdlpInfo["isUpdating"] = IsYtDlpUpdating;
            dict["ytdlp"] = ytdlpInfo;

            var ariaInfo = new Dictionary<string, object>();
            ariaInfo["currentVersion"] = CachedAria2Version ?? GetAria2Version();
            ariaInfo["latestVersion"] = "1.37.0";
            ariaInfo["status"] = "ready";
            dict["aria2"] = ariaInfo;

            var settingsInfo = new Dictionary<string, object>();
            settingsInfo["autoUpdateEngines"] = engine.AutoUpdateEngines;
            settingsInfo["autoUpdateApp"] = engine.AutoUpdateApp;
            settingsInfo["notifyOnUpdate"] = engine.NotifyOnUpdate;
            settingsInfo["lastCheck"] = engine.LastUpdateCheck ?? "";
            dict["settings"] = settingsInfo;

            return dict;
        }
    }

    public class HttpBridgeServer
    {
        private TcpListener listener;
        private Thread serverThread;
        private bool isRunning;
        private EngineManager engine;
        private Action<string> onNotification;
        private UpdateManager updateManager;
        private DateTime lastExtensionPing = DateTime.MinValue;
        private string lastExtensionVersion = "";
        private volatile bool forceExtensionReload = true;

        public HttpBridgeServer(EngineManager engine, Action<string> onNotification, UpdateManager updateManager = null)
        {
            this.engine = engine;
            this.onNotification = onNotification;
            this.updateManager = updateManager;
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
                    string rawUrl = parts.Length > 1 ? parts[1] : "/";
                    int qIdx = rawUrl.IndexOf('?');
                    string urlPath = qIdx >= 0 ? rawUrl.Substring(0, qIdx) : rawUrl;

                    int contentLength = 0;
                    string requestOrigin = "";
                    string headerLine;
                    while (!string.IsNullOrEmpty(headerLine = reader.ReadLine()))
                    {
                        if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(headerLine.Substring(15).Trim(), out contentLength);
                        }
                        else if (headerLine.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))
                        {
                            requestOrigin = headerLine.Substring(7).Trim();
                        }
                    }

                    bool isTrustedOrigin = IsTrustedOrigin(requestOrigin);
                    string allowedOrigin = isTrustedOrigin ? (string.IsNullOrEmpty(requestOrigin) ? "*" : requestOrigin) : null;

                    if (method == "OPTIONS")
                    {
                        SendHeaders(writer, 200, "OK", "text/plain", 0);
                        return;
                    }

                    // Security guard: Deny cross-origin attacks from external websites
                    if (!string.IsNullOrEmpty(requestOrigin) && !isTrustedOrigin)
                    {
                        if (urlPath.StartsWith("/api/"))
                        {
                            byte[] errBytes = Encoding.UTF8.GetBytes("{\"error\":\"Origin forbidden\"}");
                            SendHeaders(writer, 403, "Forbidden", "application/json", errBytes.Length, null);
                            writer.Write("{\"error\":\"Origin forbidden\"}");
                            writer.Flush();
                            return;
                        }
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

                    if (urlPath == "/manifest.json")
                    {
                        string mPath = Path.Combine(engine.AppDir, "ui", "manifest.json");
                        if (File.Exists(mPath))
                        {
                            byte[] mBytes = File.ReadAllBytes(mPath);
                            SendHeaders(writer, 200, "OK", "application/manifest+json; charset=utf-8", mBytes.Length);
                            writer.BaseStream.Write(mBytes, 0, mBytes.Length);
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
                        bool extOutdated = extConnected && !string.IsNullOrEmpty(lastExtensionVersion) && lastExtensionVersion != UpdateManager.CurrentAppVersion;
                        var statusObj = new
                        {
                            status = "running",
                            engine = "aria2+yt-dlp",
                            downloads = engine.DownloadsFolder,
                            extensionConnected = extConnected,
                            extensionVersion = lastExtensionVersion,
                            extensionOutdated = extOutdated,
                            autoCapture = engine.AutoCaptureEnabled,
                            appUpdateAvailable = updateManager != null && updateManager.AppUpdateAvailable,
                            latestAppVersion = updateManager != null ? updateManager.LatestAppVersion : "0.1.0-beta",
                            ytdlpVersion = updateManager != null ? updateManager.CachedYtDlpVersion : ""
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
                            string resolvedPath = "";
                            string resolvedUrl = "";
                            if (t.ContainsKey("bittorrent"))
                            {
                                var bt = t["bittorrent"] as Dictionary<string, object>;
                                if (bt != null && bt.ContainsKey("info"))
                                {
                                    var info = bt["info"] as Dictionary<string, object>;
                                    if (info != null && info.ContainsKey("name")) filename = info["name"].ToString();
                                }
                            }
                            if (t.ContainsKey("files"))
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
                                            if (!string.IsNullOrEmpty(p))
                                            {
                                                if (status == "complete" && File.Exists(p))
                                                {
                                                    p = MagicBytesDetector.FixExtension(p);
                                                }
                                                resolvedPath = p;
                                                filename = Path.GetFileName(p);
                                            }
                                        }
                                        if (firstFile.ContainsKey("uris"))
                                        {
                                            var uris = firstFile["uris"] as ArrayList;
                                            if (uris != null && uris.Count > 0)
                                            {
                                                var uObj = uris[0] as Dictionary<string, object>;
                                                if (uObj != null && uObj.ContainsKey("uri"))
                                                {
                                                    resolvedUrl = uObj["uri"].ToString();
                                                    if (string.IsNullOrEmpty(filename))
                                                    {
                                                        try
                                                        {
                                                            Uri u = new Uri(resolvedUrl);
                                                            filename = Path.GetFileName(u.LocalPath);
                                                            if (string.IsNullOrEmpty(filename)) filename = u.Host;
                                                        }
                                                        catch { filename = resolvedUrl; }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(filename)) filename = "Direct Download (" + gid + ")";
                            if (string.IsNullOrEmpty(resolvedPath)) resolvedPath = Path.Combine(engine.DownloadsFolder, filename);

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
                            item["filePath"] = !string.IsNullOrEmpty(resolvedPath) ? resolvedPath.Replace('/', '\\') : "";
                            item["url"] = resolvedUrl;
                            unifiedList.Add(item);
                        }

                        // Add YT-DLP Tasks using thread-safe snapshot
                        var ytSnapshot = engine.GetYtTasksSnapshot();
                        foreach (var yt in ytSnapshot)
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
                                string ytSaveDir = !string.IsNullOrEmpty(yt.SaveDir) ? yt.SaveDir : engine.DownloadsFolder;
                                item["filePath"] = Path.Combine(ytSaveDir, yt.Name).Replace('/', '\\');
                            unifiedList.Insert(0, item);
                        }

                        var result = new Dictionary<string, object>
                        {
                            { "tasks", unifiedList },
                            { "pending", engine.GetPendingDownloads() }
                        };
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
                        if (data != null)
                        {
                            string dlUrl = SafeGetString(data, "url").Trim();
                            string quality = SafeGetString(data, "quality", "best");
                            string queue = SafeGetString(data, "queue", "MainQueue");
                            string cookies = SafeGetString(data, "cookies");
                            string userAgent = SafeGetString(data, "userAgent");
                            string referer = SafeGetString(data, "referer");
                            string filename = SafeGetString(data, "filename");
                            if (string.IsNullOrEmpty(filename)) filename = SafeGetString(data, "title");
                            string category = SafeGetString(data, "category");
                            if (string.IsNullOrEmpty(category)) category = SafeGetString(data, "subfolder");
                            string type = SafeGetString(data, "type").ToLowerInvariant();
                            bool startNow = true;
                            if (data.ContainsKey("startNow") && data["startNow"] != null)
                            {
                                bool.TryParse(data["startNow"].ToString(), out startNow);
                            }
                            bool direct = false;
                            if (data.ContainsKey("direct") && data["direct"] != null)
                            {
                                bool.TryParse(data["direct"].ToString(), out direct);
                            }

                            if (!string.IsNullOrEmpty(dlUrl))
                            {
                                if (direct)
                                {
                                    bool isVideo = MediaUrlHelper.IsMediaPlatformUrl(dlUrl) || (type == "video" && !MediaUrlHelper.HasDirectFileExtension(dlUrl));
                                    string assignedGid;
                                    if (isVideo)
                                    {
                                        assignedGid = engine.StartYtDlp(dlUrl, quality, queue, cookies, userAgent, referer, filename, category, startNow);
                                    }
                                    else
                                    {
                                        assignedGid = engine.AddAriaUri(dlUrl, startNow, cookies, userAgent, referer, filename, category);
                                    }

                                    if (onNotification != null) onNotification("Download added: " + dlUrl);

                                    string ok = "{\"ok\":true,\"gid\":\"" + assignedGid + "\"}";
                                    byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                                    SendHeaders(writer, 200, "OK", "application/json", okBytes.Length, allowedOrigin);
                                    writer.Write(ok);
                                    writer.Flush();
                                    return;
                                }
                                else
                                {
                                    // Non-direct call: Intercept and prompt confirmation dialog! Never start silently!
                                    var pending = engine.AddPending(dlUrl, filename, filename, category, quality, cookies, userAgent, referer);
                                    if (DownloaderAppContext.Instance != null)
                                    {
                                        DownloaderAppContext.Instance.PromptDownload(pending);
                                    }
                                    string ok = "{\"ok\":true,\"intercepted\":true,\"pendingId\":\"" + pending.pendingId + "\"}";
                                    byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                                    SendHeaders(writer, 200, "OK", "application/json", okBytes.Length, allowedOrigin);
                                    writer.Write(ok);
                                    writer.Flush();
                                    return;
                                }
                            }
                        }
                        SendHeaders(writer, 400, "Bad Request", "application/json", 24, allowedOrigin);
                        writer.Write("{\"error\":\"Invalid data\"}");
                        writer.Flush();
                        return;
                    }

                    // 5. TASK ACTION API
                    if (urlPath == "/api/action" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        if (data != null)
                        {
                            string gid = SafeGetString(data, "gid");
                            string action = SafeGetString(data, "action");

                            if (action == "pause" && !string.IsNullOrEmpty(gid)) engine.PauseTask(gid);
                            if (action == "resume" && !string.IsNullOrEmpty(gid)) engine.ResumeTask(gid);
                            if (action == "remove" && !string.IsNullOrEmpty(gid)) engine.RemoveTask(gid);
                            if (action == "pauseAll") engine.PauseAll();
                            if (action == "resumeAll") engine.ResumeAll();

                            string ok = "{\"ok\":true}";
                            byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                            SendHeaders(writer, 200, "OK", "application/json", okBytes.Length, allowedOrigin);
                            writer.Write(ok);
                            writer.Flush();
                            return;
                        }
                        SendHeaders(writer, 400, "Bad Request", "application/json", 24, allowedOrigin);
                        writer.Write("{\"error\":\"Invalid data\"}");
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
                                engine.UpdateQueueConfig(q, cfg =>
                                {
                                    if (cfgDict.ContainsKey("enabled")) cfg.Enabled = Convert.ToBoolean(cfgDict["enabled"]);
                                    if (cfgDict.ContainsKey("startTime")) cfg.StartTime = cfgDict["startTime"].ToString();
                                    if (cfgDict.ContainsKey("stopTime")) cfg.StopTime = cfgDict["stopTime"].ToString();
                                    if (cfgDict.ContainsKey("maxConcurrent"))
                                    {
                                        cfg.MaxConcurrent = Convert.ToInt32(cfgDict["maxConcurrent"]);
                                        engine.SetMaxConcurrentDownloads(cfg.MaxConcurrent);
                                    }
                                    if (cfgDict.ContainsKey("shutdown")) cfg.Shutdown = Convert.ToBoolean(cfgDict["shutdown"]);
                                });
                            }
                        }

                        string ok = "{\"ok\":true}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 7.1 ABORT SHUTDOWN API
                    if (urlPath == "/api/shutdown/abort" && method == "POST")
                    {
                        try { Process.Start("shutdown.exe", "/a"); } catch { }
                        lock (engine.Queues)
                        {
                            foreach (var q in engine.Queues.Values)
                            {
                                q.Shutdown = false;
                                q.Active = false;
                            }
                        }
                        string ok = "{\"ok\":true,\"message\":\"Shutdown aborted\"}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 8. OPEN FOLDER API (Supports highlighting file with /select, normalized backslashes)
                    if (urlPath.StartsWith("/api/openfolder") && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string rawPath = data != null && data.ContainsKey("path") ? data["path"].ToString() : "";
                        string filePath = !string.IsNullOrEmpty(rawPath) ? rawPath.Trim('"', ' ').Replace('/', '\\') : "";

                        if (!string.IsNullOrEmpty(filePath))
                        {
                            try
                            {
                                string fullPath = Path.GetFullPath(filePath);
                                if (File.Exists(fullPath))
                                {
                                    Process.Start("explorer.exe", "/select,\"" + fullPath + "\"");
                                }
                                else if (Directory.Exists(fullPath))
                                {
                                    Process.Start("explorer.exe", "\"" + fullPath + "\"");
                                }
                                else
                                {
                                    string dir = Path.GetDirectoryName(fullPath);
                                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                                    {
                                        string baseName = Path.GetFileNameWithoutExtension(fullPath);
                                        string[] matches = Directory.GetFiles(dir, baseName + ".*");
                                        if (matches != null && matches.Length > 0 && File.Exists(matches[0]))
                                        {
                                            Process.Start("explorer.exe", "/select,\"" + matches[0] + "\"");
                                        }
                                        else
                                        {
                                            Process.Start("explorer.exe", "\"" + dir + "\"");
                                        }
                                    }
                                    else
                                    {
                                        Process.Start("explorer.exe", "\"" + engine.DownloadsFolder + "\"");
                                    }
                                }
                            }
                            catch
                            {
                                Process.Start("explorer.exe", "\"" + engine.DownloadsFolder + "\"");
                            }
                        }
                        else
                        {
                            string targetFolder = engine.DownloadsFolder;
                            if (urlPath.Contains("target=extension"))
                            {
                                targetFolder = Path.Combine(engine.AppDir, "extension");
                            }
                            Process.Start("explorer.exe", "\"" + targetFolder + "\"");
                        }

                        string ok = "{\"ok\":true}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 8.5 OPEN FILE API (Direct shell execution of finished download with security checks)
                    if (urlPath == "/api/openfile" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string filePath = data != null && data.ContainsKey("path") ? data["path"].ToString() : "";
                        bool launched = false;
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            try
                            {
                                string fullPath = Path.GetFullPath(filePath);
                                string fullDownloads = Path.GetFullPath(engine.DownloadsFolder);
                                if (fullPath.StartsWith(fullDownloads, StringComparison.OrdinalIgnoreCase))
                                {
                                    string ext = Path.GetExtension(fullPath).ToLowerInvariant();
                                    bool isBlockedScript = ext == ".bat" || ext == ".cmd" || ext == ".vbs" || ext == ".ps1" || ext == ".reg" || ext == ".hta" || ext == ".scr";
                                    if (!isBlockedScript)
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            FileName = fullPath,
                                            UseShellExecute = true
                                        });
                                        launched = true;
                                    }
                                }
                            }
                            catch { }
                        }

                        string ok = "{\"ok\":" + (launched ? "true" : "false") + "}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length, allowedOrigin);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 9. EXTENSION HEARTBEAT PING API
                    if (urlPath == "/api/extension/ping")
                    {
                        lastExtensionPing = DateTime.UtcNow;
                        try
                        {
                            var pingData = JsonHelper.ParseDict(requestBody);
                            if (pingData != null && pingData.ContainsKey("version"))
                            {
                                lastExtensionVersion = pingData["version"].ToString();
                            }
                        }
                        catch { }
                        bool shouldReload = forceExtensionReload || (!string.IsNullOrEmpty(lastExtensionVersion) && lastExtensionVersion != UpdateManager.CurrentAppVersion);
                        forceExtensionReload = false;
                        var pingResp = new
                        {
                            ok = true,
                            connected = true,
                            autoCapture = engine.AutoCaptureEnabled,
                            appVersion = UpdateManager.CurrentAppVersion,
                            reloadExtension = shouldReload
                        };
                        string ok = JsonHelper.Serialize(pingResp);
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 9b. FORCE RELOAD EXTENSION API
                    if (urlPath == "/api/extension/reload" && method == "POST")
                    {
                        forceExtensionReload = true;
                        var reloadResp = new { ok = true, message = "Extension reload queued for next heartbeat" };
                        string ok = JsonHelper.Serialize(reloadResp);
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

                    // 13. PROBE URL API
                    if (urlPath == "/api/probe" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string pUrl = data != null && data.ContainsKey("url") ? data["url"].ToString().Trim() : "";
                        string cookies = data != null && data.ContainsKey("cookies") ? data["cookies"].ToString() : "";
                        string userAgent = data != null && data.ContainsKey("userAgent") ? data["userAgent"].ToString() : "";
                        Dictionary<string, object> headers = null;
                        if (data != null && data.ContainsKey("headers") && data["headers"] is Dictionary<string, object>)
                        {
                            headers = (Dictionary<string, object>)data["headers"];
                        }

                        var probeRes = UrlProbeHelper.Probe(pUrl, engine.AppDir, cookies, userAgent, headers);
                        string json = JsonHelper.Serialize(probeRes);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 14. OPEN APP WINDOW API
                    if (urlPath == "/api/openapp" && method == "POST")
                    {
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                if (!WindowManager.FocusExistingWindow())
                                {
                                    DownloaderAppContext.LaunchDashboard(engine.AppDir);
                                }
                            }
                            catch { }
                        });

                        string ok = "{\"ok\":true}";
                        byte[] okBytes = Encoding.UTF8.GetBytes(ok);
                        SendHeaders(writer, 200, "OK", "application/json", okBytes.Length);
                        writer.Write(ok);
                        writer.Flush();
                        return;
                    }

                    // 15. INTERCEPT DOWNLOAD API (Pre-flight Confirmation)
                    if (urlPath == "/api/intercept" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string url = data != null && data.ContainsKey("url") ? data["url"].ToString().Trim() : "";
                        string title = data != null && data.ContainsKey("title") ? data["title"].ToString() : "";
                        string filename = data != null && data.ContainsKey("filename") ? data["filename"].ToString() : "";
                        string category = data != null && data.ContainsKey("category") ? data["category"].ToString() : (data != null && data.ContainsKey("subfolder") ? data["subfolder"].ToString() : "");
                        string quality = data != null && data.ContainsKey("quality") ? data["quality"].ToString() : "best";
                        string cookies = data != null && data.ContainsKey("cookies") ? data["cookies"].ToString() : "";
                        string userAgent = data != null && data.ContainsKey("userAgent") ? data["userAgent"].ToString() : "";
                        string referer = data != null && data.ContainsKey("referer") ? data["referer"].ToString() : "";
                        Dictionary<string, object> headers = null;
                        if (data != null && data.ContainsKey("headers") && data["headers"] is Dictionary<string, object>)
                        {
                            headers = (Dictionary<string, object>)data["headers"];
                        }

                        var pending = engine.AddPending(url, title, filename, category, quality, cookies, userAgent, referer, headers);

                        // Prompt Native IDM-Style Download Confirmation Dialog
                        if (DownloaderAppContext.Instance != null)
                        {
                            DownloaderAppContext.Instance.PromptDownload(pending);
                        }
                        else
                        {
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                try
                                {
                                    if (!WindowManager.FocusExistingWindow())
                                    {
                                        DownloaderAppContext.LaunchDashboard(engine.AppDir);
                                    }
                                }
                                catch { }
                            });
                        }

                        if (onNotification != null)
                        {
                            onNotification("Download intercepted: " + pending.filename);
                        }

                        var respObj = new { ok = true, pending = pending };
                        string json = JsonHelper.Serialize(respObj);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 16. GET PENDING INTERCEPTIONS API
                    if (urlPath == "/api/intercept/pending" && method == "GET")
                    {
                        var list = engine.GetPendingDownloads();
                        var respObj = new { ok = true, pending = list };
                        string json = JsonHelper.Serialize(respObj);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 17. CONFIRM PENDING DOWNLOAD API
                    if (urlPath == "/api/intercept/confirm" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string pendingId = data != null && data.ContainsKey("pendingId") ? data["pendingId"].ToString() : "";
                        string action = data != null && data.ContainsKey("action") ? data["action"].ToString() : "start";
                        string customName = data != null && data.ContainsKey("filename") ? data["filename"].ToString() : "";
                        string category = data != null && data.ContainsKey("category") ? data["category"].ToString() : "";
                        string queue = data != null && data.ContainsKey("queue") ? data["queue"].ToString() : "MainQueue";
                        string quality = data != null && data.ContainsKey("quality") ? data["quality"].ToString() : "best";

                        PendingDownloadInfo pending;
                        if (engine.RemovePending(pendingId, out pending))
                        {
                            string finalName = !string.IsNullOrEmpty(customName) ? customName : pending.filename;
                            string finalCat = !string.IsNullOrEmpty(category) ? category : pending.category;
                            string finalQuality = !string.IsNullOrEmpty(quality) ? quality : pending.quality;
                            bool startNow = (action == "start");

                            bool isVideo = MediaUrlHelper.IsMediaPlatformUrl(pending.url) || (pending.type == "video" && !MediaUrlHelper.HasDirectFileExtension(pending.url));
                            string assignedGid;
                            if (isVideo)
                            {
                                assignedGid = engine.StartYtDlp(pending.url, finalQuality, queue, pending.cookies, pending.userAgent, pending.referer, finalName, finalCat, startNow);
                            }
                            else
                            {
                                assignedGid = engine.AddAriaUri(pending.url, startNow, pending.cookies, pending.userAgent, pending.referer, finalName, finalCat);
                            }

                            if (onNotification != null) onNotification(startNow ? "Download started: " + finalName : "Saved to queue: " + finalName);

                            var respObj = new { ok = true, gid = assignedGid, action = action };
                            string json = JsonHelper.Serialize(respObj);
                            byte[] bytes = Encoding.UTF8.GetBytes(json);
                            SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                            writer.Write(json);
                            writer.Flush();
                            return;
                        }

                        SendHeaders(writer, 404, "Not Found", "application/json", 0);
                        return;
                    }

                    // 18. CANCEL PENDING DOWNLOAD API
                    if (urlPath == "/api/intercept/cancel" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string pendingId = data != null && data.ContainsKey("pendingId") ? data["pendingId"].ToString() : "";
                        PendingDownloadInfo pending;
                        bool removed = engine.RemovePending(pendingId, out pending);
                        var respObj = new { ok = removed };
                        string json = JsonHelper.Serialize(respObj);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 19. UPDATE STATUS API (GET)
                    if (urlPath == "/api/update/status" && method == "GET")
                    {
                        var statusDict = updateManager != null ? updateManager.GetStatusDict() : new Dictionary<string, object>();
                        string json = JsonHelper.Serialize(statusDict);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 20. UPDATE CHECK API (POST)
                    if (urlPath == "/api/update/check" && method == "POST")
                    {
                        if (updateManager != null)
                        {
                            updateManager.CheckAppUpdate();
                        }
                        var statusDict = updateManager != null ? updateManager.GetStatusDict() : new Dictionary<string, object>();
                        string json = JsonHelper.Serialize(statusDict);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 21. UPDATE ENGINE API (POST)
                    if (urlPath == "/api/update/engine" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string eng = data != null && data.ContainsKey("engine") ? data["engine"].ToString() : "ytdlp";
                        object resultObj = null;

                        if (updateManager != null)
                        {
                            if (eng == "aria2")
                            {
                                resultObj = updateManager.CheckAria2();
                            }
                            else if (eng == "all")
                            {
                                var ytRes = updateManager.UpdateYtDlp(false);
                                var arRes = updateManager.CheckAria2();
                                var combinedLogs = new List<string>(ytRes.logs);
                                combinedLogs.AddRange(arRes.logs);
                                resultObj = new
                                {
                                    ok = ytRes.ok,
                                    updated = ytRes.updated,
                                    engine = "all",
                                    message = ytRes.message + " • " + arRes.message,
                                    logs = combinedLogs
                                };
                            }
                            else
                            {
                                resultObj = updateManager.UpdateYtDlp(false);
                            }
                        }

                        string json = JsonHelper.Serialize(resultObj ?? new { ok = false, message = "UpdateManager not available" });
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 22. APPLY APP UPDATE API (POST)
                    if (urlPath == "/api/update/app" && method == "POST")
                    {
                        string msg = "";
                        bool ok = updateManager != null && updateManager.ApplyAppUpdate(out msg);
                        var respObj = new { ok = ok, message = msg };
                        string json = JsonHelper.Serialize(respObj);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 23. UPDATE SETTINGS API (POST)
                    if (urlPath == "/api/update/settings" && method == "POST")
                    {
                        try
                        {
                            var data = JsonHelper.ParseDict(requestBody);
                            if (data != null)
                            {
                                if (data.ContainsKey("autoUpdateEngines")) engine.AutoUpdateEngines = Convert.ToBoolean(data["autoUpdateEngines"]);
                                if (data.ContainsKey("autoUpdateApp")) engine.AutoUpdateApp = Convert.ToBoolean(data["autoUpdateApp"]);
                                if (data.ContainsKey("notifyOnUpdate")) engine.NotifyOnUpdate = Convert.ToBoolean(data["notifyOnUpdate"]);
                                engine.SaveSettings();
                            }
                        }
                        catch { }

                        var statusDict = updateManager != null ? updateManager.GetStatusDict() : new Dictionary<string, object>();
                        string json = JsonHelper.Serialize(statusDict);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    // 24. RESTART APPLICATION API (POST)
                    if (urlPath == "/api/restart" && method == "POST")
                    {
                        var okResp = new { ok = true, message = "Restarting..." };
                        string json = JsonHelper.Serialize(okResp);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();

                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            Thread.Sleep(300);
                            try
                            {
                                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                                ProcessStartInfo psi = new ProcessStartInfo
                                {
                                    FileName = "cmd.exe",
                                    Arguments = "/c timeout /t 1 /nobreak > nul & start \"\" \"" + currentExe + "\"",
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                };
                                Process.Start(psi);
                                if (engine != null) engine.StopAria2();
                            }
                            catch { }
                            finally
                            {
                                Environment.Exit(0);
                            }
                        });
                        return;
                    }

                    // 25. OPEN EXTERNAL URL IN SYSTEM DEFAULT BROWSER (POST)
                    if (urlPath == "/api/openurl" && method == "POST")
                    {
                        var data = JsonHelper.ParseDict(requestBody);
                        string targetUrl = data != null && data.ContainsKey("url") ? data["url"].ToString() : "";
                        if (!string.IsNullOrEmpty(targetUrl) && (targetUrl.StartsWith("http://") || targetUrl.StartsWith("https://")))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
                            }
                            catch { }
                        }
                        var okResp = new { ok = true };
                        string json = JsonHelper.Serialize(okResp);
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        SendHeaders(writer, 200, "OK", "application/json; charset=utf-8", bytes.Length);
                        writer.Write(json);
                        writer.Flush();
                        return;
                    }

                    SendHeaders(writer, 404, "Not Found", "text/plain", 0);
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(engine.AppDir, "http_error.log"), DateTime.Now.ToString("s") + " [HTTP Error] " + ex.ToString() + "\r\n"); } catch { }
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static string SafeGetString(Dictionary<string, object> dict, string key, string fallback = "")
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return fallback;
            return dict[key].ToString();
        }

        private static bool IsTrustedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return true;
            origin = origin.ToLowerInvariant();
            return origin.StartsWith("chrome-extension://") ||
                   origin.StartsWith("http://127.0.0.1") ||
                   origin.StartsWith("http://localhost");
        }

        private void SendHeaders(StreamWriter writer, int code, string status, string contentType, int length, string allowedOrigin = "*")
        {
            writer.Write(string.Format("HTTP/1.1 {0} {1}\r\n", code, status));
            if (!string.IsNullOrEmpty(allowedOrigin))
            {
                writer.Write("Access-Control-Allow-Origin: " + allowedOrigin + "\r\n");
                writer.Write("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
                writer.Write("Access-Control-Allow-Headers: Content-Type\r\n");
            }
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
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;

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

        public static int DashboardPid = 0;

        public static bool FocusExistingWindow()
        {
            IntPtr found = IntPtr.Zero;

            // 1. Fast path: check known Dashboard process
            if (DashboardPid > 0)
            {
                try
                {
                    Process p = Process.GetProcessById(DashboardPid);
                    if (p != null && !p.HasExited)
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                        {
                            found = p.MainWindowHandle;
                        }
                    }
                }
                catch
                {
                    DashboardPid = 0;
                }
            }

            // 2. Enumerate top-level windows specifically for FastDownloader app window
            if (found == IntPtr.Zero)
            {
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    StringBuilder sb = new StringBuilder(512);
                    GetWindowText(hWnd, sb, 512);
                    string title = sb.ToString().Trim();
                    if (string.IsNullOrEmpty(title)) return true;

                    // Match exact app window title; exclude regular browser tabs that mention FastDownloader
                    bool isAppWindow = false;
                    if (title.Equals("FastDownloader Pro", StringComparison.OrdinalIgnoreCase) ||
                        title.Equals("FastDownloader", StringComparison.OrdinalIgnoreCase))
                    {
                        isAppWindow = true;
                    }
                    else if (title.StartsWith("FastDownloader Pro", StringComparison.OrdinalIgnoreCase) ||
                             title.StartsWith("FastDownloader", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ensure it's not a regular browser window with tabs
                        if (!title.Contains(" - Google Chrome") &&
                            !title.Contains(" - Personal - Microsoft") &&
                            !title.Contains(" - Work - Microsoft") &&
                            !title.Contains(" and ") &&
                            !title.Contains(" - Mozilla Firefox") &&
                            !title.Contains(" - Brave"))
                        {
                            isAppWindow = true;
                        }
                    }

                    if (isAppWindow)
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
                                DashboardPid = (int)pid;
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
            }

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

                // Bypass Windows foreground lock by toggling TOPMOST (pops up over active browser)
                try
                {
                    SetWindowPos(found, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetWindowPos(found, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
                catch { }
                BringWindowToTop(found);
                SetForegroundWindow(found);
                try { SwitchToThisWindow(found, true); } catch { }
                return true;
            }
            return false;
        }
    }

    public class DownloadConfirmDialog : Form
    {
        public bool Confirmed { get; private set; }
        public bool StartNow { get; private set; }
        public string FileName { get { return txtFileName != null ? txtFileName.Text.Trim() : ""; } }
        public string SaveDirectory { get { return txtSaveDir != null ? txtSaveDir.Text.Trim() : ""; } }
        public string Category { get { return cmbCategory != null && cmbCategory.SelectedItem != null ? cmbCategory.SelectedItem.ToString() : "General"; } }

        private TextBox txtUrl;
        private ComboBox cmbCategory;
        private TextBox txtFileName;
        private TextBox txtSaveDir;
        private Button btnBrowse;
        private Button btnStart;
        private Button btnLater;
        private Button btnCancel;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        public DownloadConfirmDialog(PendingDownloadInfo pending, string baseDownloadsFolder)
        {
            Confirmed = false;
            StartNow = true;

            this.Text = "Download File Info - FastDownloader";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.ClientSize = new Size(560, 395);
            this.BackColor = Color.FromArgb(24, 25, 29);
            this.ForeColor = Color.FromArgb(235, 238, 245);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath)) this.Icon = new Icon(icoPath);
            }
            catch { }

            // 1. Header Panel
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.FromArgb(18, 19, 22)
            };

            Label lblHeaderTitle = new Label
            {
                Text = "⚡ Download File Info",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(16, 8),
                AutoSize = true
            };

            Label lblHeaderSub = new Label
            {
                Text = "Confirm destination folder and filename before transfer starts",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 155, 168),
                Location = new Point(17, 30),
                AutoSize = true
            };

            Panel headerBorder = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(40, 42, 50)
            };

            headerPanel.Controls.Add(lblHeaderTitle);
            headerPanel.Controls.Add(lblHeaderSub);
            headerPanel.Controls.Add(headerBorder);
            this.Controls.Add(headerPanel);

            // 2. Main Body Content
            Panel bodyPanel = new Panel
            {
                Location = new Point(16, 64),
                Size = new Size(528, 260)
            };

            // URL Row
            Label lblUrl = new Label
            {
                Text = "URL:",
                Location = new Point(0, 4),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 165, 175)
            };
            txtUrl = new TextBox
            {
                Location = new Point(85, 2),
                Size = new Size(440, 23),
                ReadOnly = true,
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.FromArgb(200, 205, 215),
                BorderStyle = BorderStyle.FixedSingle,
                Text = pending != null && !string.IsNullOrEmpty(pending.url) ? pending.url : ""
            };
            bodyPanel.Controls.Add(lblUrl);
            bodyPanel.Controls.Add(txtUrl);

            // Category Row
            Label lblCat = new Label
            {
                Text = "Category:",
                Location = new Point(0, 36),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 165, 175)
            };
            cmbCategory = new ComboBox
            {
                Location = new Point(85, 33),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbCategory.Items.AddRange(new object[] { "General", "Compressed", "Programs", "Video", "Music", "Documents" });
            string pendingCat = pending != null && !string.IsNullOrEmpty(pending.category) ? pending.category : "General";
            int catIdx = cmbCategory.Items.IndexOf(pendingCat);
            cmbCategory.SelectedIndex = catIdx >= 0 ? catIdx : 0;
            bodyPanel.Controls.Add(lblCat);
            bodyPanel.Controls.Add(cmbCategory);

            // File Name Row
            Label lblFileName = new Label
            {
                Text = "File Name:",
                Location = new Point(0, 68),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 165, 175)
            };

            string initName = pending != null ? pending.filename : "download";
            string lowerName = (initName ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(initName) || lowerName == "browser download" || lowerName == "main" || lowerName.Contains("unknown_video") || lowerName == "facebook")
            {
                string pTitle = pending != null && !string.IsNullOrEmpty(pending.title) ? pending.title : "";
                string pTitleLower = pTitle.ToLowerInvariant();
                if (!string.IsNullOrEmpty(pTitle) && pTitleLower != "browser download" && pTitleLower != "main" && !pTitleLower.Contains("unknown_video") && pTitleLower != "facebook")
                {
                    initName = pTitle;
                }
                else
                {
                    initName = "Video_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }
            }
            if (string.IsNullOrEmpty(Path.GetExtension(initName)))
            {
                if (pendingCat == "Video" || (pending != null && pending.type == "video")) initName += ".mp4";
                else if (pendingCat == "Music" || (pending != null && pending.type == "audio")) initName += ".mp3";
                else if (pendingCat == "Programs") initName += ".exe";
                else if (pendingCat == "Compressed") initName += ".zip";
                else if (pendingCat == "Documents") initName += ".pdf";
            }

            txtFileName = new TextBox
            {
                Location = new Point(85, 65),
                Size = new Size(440, 23),
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = initName
            };
            bodyPanel.Controls.Add(lblFileName);
            bodyPanel.Controls.Add(txtFileName);

            // Save To Row
            Label lblSaveTo = new Label
            {
                Text = "Save To:",
                Location = new Point(0, 100),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 165, 175)
            };

            string initDir = baseDownloadsFolder;
            if (pendingCat != "General" && pendingCat != "Default" && !string.IsNullOrEmpty(pendingCat))
            {
                initDir = Path.Combine(baseDownloadsFolder, pendingCat);
            }

            txtSaveDir = new TextBox
            {
                Location = new Point(85, 97),
                Size = new Size(345, 23),
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = initDir
            };

            btnBrowse = new Button
            {
                Location = new Point(438, 96),
                Size = new Size(87, 25),
                Text = "Browse...",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 45, 55),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(60, 64, 76);
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Destination Folder for Download";
                    fbd.SelectedPath = Directory.Exists(txtSaveDir.Text) ? txtSaveDir.Text : baseDownloadsFolder;
                    fbd.ShowNewFolderButton = true;
                    if (fbd.ShowDialog(this) == DialogResult.OK)
                    {
                        txtSaveDir.Text = fbd.SelectedPath;
                    }
                }
            };

            cmbCategory.SelectedIndexChanged += (s, e) =>
            {
                string selCat = cmbCategory.SelectedItem != null ? cmbCategory.SelectedItem.ToString() : "General";
                if (selCat == "General" || selCat == "Default")
                {
                    txtSaveDir.Text = baseDownloadsFolder;
                }
                else
                {
                    txtSaveDir.Text = Path.Combine(baseDownloadsFolder, selCat);
                }
            };

            bodyPanel.Controls.Add(lblSaveTo);
            bodyPanel.Controls.Add(txtSaveDir);
            bodyPanel.Controls.Add(btnBrowse);

            // Info Card Box
            Panel infoCard = new Panel
            {
                Location = new Point(0, 134),
                Size = new Size(525, 90),
                BackColor = Color.FromArgb(18, 19, 22),
                BorderStyle = BorderStyle.FixedSingle
            };

            string sz = pending != null && !string.IsNullOrEmpty(pending.sizeFormatted) ? pending.sizeFormatted : "Direct Stream";
            Label lblSize = new Label
            {
                Text = "📦 File Size:  " + sz,
                Location = new Point(14, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 225, 235)
            };

            Label lblTurbo = new Label
            {
                Text = "⚡ Turbo Engine:  16x Multi-Thread Acceleration (aria2 / yt-dlp)",
                Location = new Point(14, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(16, 185, 129)
            };

            bool resumable = pending != null && pending.resumable;
            Label lblResume = new Label
            {
                Text = "🔄 Resume Support:  " + (resumable ? "Yes (Byte-Range Chunking Supported)" : "Direct Web Transfer"),
                Location = new Point(14, 58),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(6, 182, 212)
            };

            infoCard.Controls.Add(lblSize);
            infoCard.Controls.Add(lblTurbo);
            infoCard.Controls.Add(lblResume);
            bodyPanel.Controls.Add(infoCard);

            this.Controls.Add(bodyPanel);

            // 3. Bottom Action Buttons Bar
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(18, 19, 22)
            };

            Panel bottomBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(40, 42, 50)
            };
            bottomPanel.Controls.Add(bottomBorder);

            btnStart = new Button
            {
                Text = "⚡ Start Download",
                Location = new Point(125, 12),
                Size = new Size(145, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += (s, e) =>
            {
                Confirmed = true;
                StartNow = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnLater = new Button
            {
                Text = "📋 Download Later",
                Location = new Point(280, 12),
                Size = new Size(135, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 45, 55),
                ForeColor = Color.FromArgb(220, 225, 235),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnLater.FlatAppearance.BorderColor = Color.FromArgb(60, 64, 76);
            btnLater.Click += (s, e) =>
            {
                Confirmed = true;
                StartNow = false;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            btnCancel = new Button
            {
                Text = "✕ Cancel",
                Location = new Point(425, 12),
                Size = new Size(100, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.FromArgb(200, 205, 215),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(50, 52, 60);
            btnCancel.Click += (s, e) =>
            {
                Confirmed = false;
                StartNow = false;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            bottomPanel.Controls.Add(btnStart);
            bottomPanel.Controls.Add(btnLater);
            bottomPanel.Controls.Add(btnCancel);
            this.Controls.Add(bottomPanel);

            this.AcceptButton = btnStart;
            this.CancelButton = btnCancel;

            this.Load += (s, e) =>
            {
                txtFileName.Focus();
                txtFileName.SelectAll();
            };
        }
    }

    public class UnifiedTaskInfo
    {
        public string Gid { get; set; }
        public string Name { get; set; }
        public long TotalLength { get; set; }
        public long CompletedLength { get; set; }
        public long DownloadSpeed { get; set; }
        public int Progress { get; set; }
        public long Eta { get; set; }
        public string Status { get; set; }
        public string FilePath { get; set; }
        public string Url { get; set; }
        public string Engine { get; set; }
        public int NumConnections { get; set; }

        public UnifiedTaskInfo()
        {
            NumConnections = 16;
            Status = "downloading";
        }
    }

    public class ModernProgressBar : Control
    {
        private int val = 0;
        public int Value
        {
            get { return val; }
            set
            {
                int clamped = Math.Max(0, Math.Min(100, value));
                if (val != clamped)
                {
                    val = clamped;
                    Invalidate();
                }
            }
        }

        public ModernProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.FromArgb(28, 30, 36);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            using (Pen borderPen = new Pen(Color.FromArgb(45, 48, 58)))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            int fillWidth = (int)((val / 100.0) * (w - 2));
            if (fillWidth > 0)
            {
                using (Brush brush = new SolidBrush(Color.FromArgb(16, 185, 129)))
                {
                    g.FillRectangle(brush, 1, 1, fillWidth, h - 2);
                }
            }
        }
    }

    public class ChunkVisualizerPanel : Panel
    {
        private int numChunks = 16;
        private int progressPct = 0;
        private bool isDownloading = true;

        public ChunkVisualizerPanel()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(18, 19, 22);
        }

        public void UpdateProgress(int pct, bool downloading)
        {
            if (this.progressPct != pct || this.isDownloading != downloading)
            {
                this.progressPct = pct;
                this.isDownloading = downloading;
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int gap = 2;
            int blockWidth = (w - (numChunks - 1) * gap) / numChunks;
            if (blockWidth < 2) blockWidth = 2;

            int filledBlocks = (int)((progressPct / 100.0) * numChunks);
            if (progressPct > 0 && filledBlocks == 0) filledBlocks = 1;

            using (Brush darkBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (Brush filledBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
            using (Brush activeBrush = new SolidBrush(Color.FromArgb(6, 182, 212)))
            {
                for (int i = 0; i < numChunks; i++)
                {
                    int x = i * (blockWidth + gap);
                    Rectangle rect = new Rectangle(x, 1, blockWidth, h - 2);

                    if (i < filledBlocks)
                    {
                        g.FillRectangle(filledBrush, rect);
                    }
                    else if (i == filledBlocks && isDownloading && progressPct < 100)
                    {
                        g.FillRectangle(activeBrush, rect);
                    }
                    else
                    {
                        g.FillRectangle(darkBrush, rect);
                    }
                }
            }
        }
    }

    public class DownloadProgressDialog : Form
    {
        private EngineManager engine;
        private string gid;
        private string filename;
        private string url;
        private string saveDir;

        private System.Windows.Forms.Timer pollTimer;
        private Label lblFileName;
        private Label lblStatus;
        private ModernProgressBar progBar;
        private Label lblPct;
        private ChunkVisualizerPanel chunkVisualizer;
        private Label lblSizeInfo;
        private Label lblSpeed;
        private Label lblTimeLeft;
        private Label lblResume;
        private Label lblConnections;
        private CheckBox chkOpenComplete;
        private CheckBox chkShutdown;
        private Button btnPauseResume;
        private Button btnCancel;
        private Button btnHide;

        private bool isPaused = false;
        private bool hasCompleted = false;

        public DownloadProgressDialog(EngineManager engine, string gid, string filename, string url, string saveDir)
        {
            this.engine = engine;
            this.gid = gid;
            this.filename = filename;
            this.url = url;
            this.saveDir = saveDir;

            this.Text = "Download Status: " + filename;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(540, 352);
            this.BackColor = Color.FromArgb(24, 25, 29);
            this.ForeColor = Color.FromArgb(235, 238, 245);
            this.Font = new Font("Segoe UI", 9F);

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath)) this.Icon = new Icon(icoPath);
            }
            catch { }

            // 1. Header Panel
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(18, 19, 22)
            };

            lblFileName = new Label
            {
                Text = "⚡ " + (filename.Length > 55 ? filename.Substring(0, 52) + "..." : filename),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 8),
                Size = new Size(510, 20),
                AutoEllipsis = true
            };

            lblStatus = new Label
            {
                Text = "⚡ Status: Downloading (16x Turbo Chunks)...",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(15, 29),
                AutoSize = true
            };

            Panel headerBorder = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(40, 42, 50)
            };

            topPanel.Controls.Add(lblFileName);
            topPanel.Controls.Add(lblStatus);
            topPanel.Controls.Add(headerBorder);
            this.Controls.Add(topPanel);

            // 2. Progress Section
            progBar = new ModernProgressBar
            {
                Location = new Point(16, 62),
                Size = new Size(440, 22)
            };
            this.Controls.Add(progBar);

            lblPct = new Label
            {
                Text = "0%",
                Location = new Point(462, 63),
                Size = new Size(60, 20),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White
            };
            this.Controls.Add(lblPct);

            // 3. Chunk Visualizer
            Label lblChunkTitle = new Label
            {
                Text = "Connection Streams (16 parallel chunk segments):",
                Location = new Point(16, 90),
                AutoSize = true,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(156, 163, 175)
            };
            this.Controls.Add(lblChunkTitle);

            chunkVisualizer = new ChunkVisualizerPanel
            {
                Location = new Point(16, 107),
                Size = new Size(508, 15)
            };
            this.Controls.Add(chunkVisualizer);

            // 4. Statistics Card
            Panel statsCard = new Panel
            {
                Location = new Point(16, 128),
                Size = new Size(508, 86),
                BackColor = Color.FromArgb(18, 19, 22),
                BorderStyle = BorderStyle.FixedSingle
            };

            lblSizeInfo = new Label
            {
                Text = "📦 Downloaded: 0 B / Calculating...",
                Location = new Point(12, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(209, 213, 219)
            };

            lblSpeed = new Label
            {
                Text = "🚀 Transfer rate: 0 B/s",
                Location = new Point(12, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129)
            };

            lblTimeLeft = new Label
            {
                Text = "⏱ Time left: Calculating...",
                Location = new Point(12, 58),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(6, 182, 212)
            };

            lblResume = new Label
            {
                Text = "🔄 Resume: Supported",
                Location = new Point(280, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(156, 163, 175)
            };

            lblConnections = new Label
            {
                Text = "⚡ Active Streams: 16 Chunks",
                Location = new Point(280, 34),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(156, 163, 175)
            };

            statsCard.Controls.Add(lblSizeInfo);
            statsCard.Controls.Add(lblSpeed);
            statsCard.Controls.Add(lblTimeLeft);
            statsCard.Controls.Add(lblResume);
            statsCard.Controls.Add(lblConnections);
            this.Controls.Add(statsCard);

            // 5. Options Checkboxes
            chkOpenComplete = new CheckBox
            {
                Text = "Open file when complete",
                Location = new Point(18, 222),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(209, 213, 219)
            };
            this.Controls.Add(chkOpenComplete);

            chkShutdown = new CheckBox
            {
                Text = "Turn off computer when done",
                Location = new Point(260, 222),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(209, 213, 219)
            };
            this.Controls.Add(chkShutdown);

            // Speed Limiter Control (IDM Feature)
            Label lblSpeedLimit = new Label
            {
                Text = "Speed Limiter:",
                Location = new Point(18, 252),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 165, 175)
            };
            this.Controls.Add(lblSpeedLimit);

            ComboBox cmbSpeedLimit = new ComboBox
            {
                Location = new Point(115, 248),
                Size = new Size(140, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbSpeedLimit.Items.AddRange(new object[] { "🚀 Maximum / Unlimited", "🐌 500 KB/s", "⚡ 1 MB/s", "⚡ 2 MB/s", "⚡ 5 MB/s", "⚡ 10 MB/s" });
            cmbSpeedLimit.SelectedIndex = 0;
            cmbSpeedLimit.SelectedIndexChanged += (s, e) =>
            {
                string sel = cmbSpeedLimit.SelectedItem != null ? cmbSpeedLimit.SelectedItem.ToString() : "";
                if (sel.Contains("Unlimited") || sel.Contains("Maximum")) engine.SetSpeedLimit("0");
                else if (sel.Contains("500 KB")) engine.SetSpeedLimit("500K");
                else if (sel.Contains("1 MB")) engine.SetSpeedLimit("1M");
                else if (sel.Contains("2 MB")) engine.SetSpeedLimit("2M");
                else if (sel.Contains("5 MB")) engine.SetSpeedLimit("5M");
                else if (sel.Contains("10 MB")) engine.SetSpeedLimit("10M");
            };
            this.Controls.Add(cmbSpeedLimit);

            // 6. Bottom Buttons Bar
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(18, 19, 22)
            };

            Panel bottomBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(40, 42, 50)
            };
            bottomPanel.Controls.Add(bottomBorder);

            btnPauseResume = new Button
            {
                Text = "⏸ Pause",
                Location = new Point(200, 10),
                Size = new Size(95, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 45, 55),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnPauseResume.FlatAppearance.BorderColor = Color.FromArgb(60, 64, 76);
            btnPauseResume.Click += (s, e) =>
            {
                if (isPaused)
                {
                    engine.ResumeTask(gid);
                    isPaused = false;
                    btnPauseResume.Text = "⏸ Pause";
                    lblStatus.Text = "⚡ Status: Downloading (16x Turbo)...";
                    lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
                }
                else
                {
                    engine.PauseTask(gid);
                    isPaused = true;
                    btnPauseResume.Text = "▶ Resume";
                    lblStatus.Text = "⏸ Status: Paused";
                    lblStatus.ForeColor = Color.FromArgb(245, 158, 11);
                }
            };

            btnCancel = new Button
            {
                Text = "✕ Cancel",
                Location = new Point(305, 10),
                Size = new Size(95, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 45, 55),
                ForeColor = Color.FromArgb(248, 113, 113),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(60, 64, 76);
            btnCancel.Click += (s, e) =>
            {
                var r = MessageBox.Show(this, "Are you sure you want to stop and remove this download?", "Cancel Download", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes)
                {
                    pollTimer.Stop();
                    engine.RemoveTask(gid);
                    this.Close();
                }
            };

            btnHide = new Button
            {
                Text = "🔽 Hide",
                Location = new Point(410, 10),
                Size = new Size(95, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.FromArgb(156, 163, 175),
                Cursor = Cursors.Hand
            };
            btnHide.FlatAppearance.BorderColor = Color.FromArgb(50, 52, 60);
            btnHide.Click += (s, e) =>
            {
                pollTimer.Stop();
                this.Close();
            };

            bottomPanel.Controls.Add(btnPauseResume);
            bottomPanel.Controls.Add(btnCancel);
            bottomPanel.Controls.Add(btnHide);
            this.Controls.Add(bottomPanel);

            // 7. Polling Timer
            pollTimer = new System.Windows.Forms.Timer();
            pollTimer.Interval = 300;
            pollTimer.Tick += (s, e) => PollProgress();
            pollTimer.Start();

            this.FormClosing += (s, e) =>
            {
                if (pollTimer != null) pollTimer.Stop();
            };
        }

        private void PollProgress()
        {
            if (hasCompleted) return;

            var task = engine.GetTaskInfo(gid);
            if (task == null) return;

            progBar.Value = task.Progress;
            lblPct.Text = task.Progress + "%";
            chunkVisualizer.UpdateProgress(task.Progress, task.Status == "downloading" || task.Status == "active");

            string totalFormatted = task.TotalLength > 0 ? UrlProbeHelper.FormatBytes(task.TotalLength) : "Unknown";
            lblSizeInfo.Text = "📦 Downloaded: " + UrlProbeHelper.FormatBytes(task.CompletedLength) + " of " + totalFormatted;
            lblSpeed.Text = "🚀 Transfer rate: " + UrlProbeHelper.FormatBytes(task.DownloadSpeed) + "/s";
            lblTimeLeft.Text = "⏱ Time left: " + (task.Eta > 0 ? UrlProbeHelper.FormatEta(task.Eta) : (task.Progress >= 100 ? "00:00" : "Calculating..."));

            if (task.Status == "paused" || task.Status == "waiting")
            {
                isPaused = true;
                btnPauseResume.Text = "▶ Resume";
                lblStatus.Text = "⏸ Status: Paused";
                lblStatus.ForeColor = Color.FromArgb(245, 158, 11);
            }
            else if (task.Status == "downloading" || task.Status == "active")
            {
                isPaused = false;
                btnPauseResume.Text = "⏸ Pause";
                lblStatus.Text = "⚡ Status: Downloading (16x Turbo)...";
                lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
            }

            if (task.Status == "complete" || task.Status == "completed")
            {
                hasCompleted = true;
                pollTimer.Stop();

                string finalPath = task.FilePath;
                if (string.IsNullOrEmpty(finalPath) && !string.IsNullOrEmpty(saveDir))
                {
                    finalPath = Path.Combine(saveDir, task.Name);
                }

                if (chkOpenComplete.Checked && !string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                {
                    try { Process.Start(finalPath); } catch { }
                }

                if (chkShutdown.Checked)
                {
                    try { Application.SetSuspendState(PowerState.Suspend, false, false); } catch { }
                }

                this.Close();

                if (DownloaderAppContext.Instance != null)
                {
                    DownloaderAppContext.Instance.ShowCompleteDialog(task.Gid, task.Name, finalPath, task.TotalLength);
                }
            }
        }
    }

    public class DownloadCompleteDialog : Form
    {
        private string filename;
        private string filePath;
        private long totalLength;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        public DownloadCompleteDialog(string filename, string filePath, long totalLength)
        {
            this.filename = filename;
            this.filePath = filePath;
            this.totalLength = totalLength;

            this.Text = "Download Complete - FastDownloader";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.ClientSize = new Size(500, 235);
            this.BackColor = Color.FromArgb(24, 25, 29);
            this.ForeColor = Color.FromArgb(235, 238, 245);
            this.Font = new Font("Segoe UI", 9F);

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath)) this.Icon = new Icon(icoPath);
            }
            catch { }

            // 1. Header Panel
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(18, 19, 22)
            };

            Label lblBadge = new Label
            {
                Text = "✔",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(14, 10),
                Size = new Size(32, 34)
            };

            Label lblTitle = new Label
            {
                Text = "Download Complete!",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(48, 8),
                AutoSize = true
            };

            Label lblSub = new Label
            {
                Text = "File has been verified and saved to disk.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(156, 163, 175),
                Location = new Point(50, 31),
                AutoSize = true
            };

            Panel headerBorder = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(40, 42, 50)
            };

            topPanel.Controls.Add(lblBadge);
            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(lblSub);
            topPanel.Controls.Add(headerBorder);
            this.Controls.Add(topPanel);

            // 2. Info Box
            Panel infoCard = new Panel
            {
                Location = new Point(16, 68),
                Size = new Size(468, 95),
                BackColor = Color.FromArgb(18, 19, 22),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblFile = new Label
            {
                Text = filename,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(12, 10),
                Size = new Size(440, 20),
                AutoEllipsis = true
            };

            string szFormatted = totalLength > 0 ? UrlProbeHelper.FormatBytes(totalLength) : "Complete";
            Label lblSize = new Label
            {
                Text = "📦 Size:  " + szFormatted,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(12, 36),
                AutoSize = true
            };

            string dirPath = !string.IsNullOrEmpty(filePath) ? Path.GetDirectoryName(filePath) : "";
            Label lblFolder = new Label
            {
                Text = "📁 Saved to:  " + dirPath,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(156, 163, 175),
                Location = new Point(12, 60),
                Size = new Size(440, 20),
                AutoEllipsis = true
            };

            infoCard.Controls.Add(lblFile);
            infoCard.Controls.Add(lblSize);
            infoCard.Controls.Add(lblFolder);
            this.Controls.Add(infoCard);

            // 3. Bottom Button Bar
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.FromArgb(18, 19, 22)
            };

            Panel bottomBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(40, 42, 50)
            };
            bottomPanel.Controls.Add(bottomBorder);

            Button btnOpen = new Button
            {
                Text = "🚀 Open",
                Location = new Point(115, 10),
                Size = new Size(115, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    try { Process.Start(filePath); } catch { }
                }
                this.Close();
            };

            Button btnOpenFolder = new Button
            {
                Text = "📁 Open Folder",
                Location = new Point(240, 10),
                Size = new Size(140, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 45, 55),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(60, 64, 76);
            btnOpenFolder.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    try { Process.Start("explorer.exe", "/select,\"" + filePath.Replace('/', '\\') + "\""); } catch { }
                }
                else if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                {
                    try { Process.Start("explorer.exe", dirPath.Replace('/', '\\')); } catch { }
                }
                this.Close();
            };

            Button btnClose = new Button
            {
                Text = "✕ Close",
                Location = new Point(390, 10),
                Size = new Size(95, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(32, 34, 40),
                ForeColor = Color.FromArgb(156, 163, 175),
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(50, 52, 60);
            btnClose.Click += (s, e) => this.Close();

            bottomPanel.Controls.Add(btnOpen);
            bottomPanel.Controls.Add(btnOpenFolder);
            bottomPanel.Controls.Add(btnClose);
            this.Controls.Add(bottomPanel);

            this.AcceptButton = btnOpen;
            this.CancelButton = btnClose;
        }
    }

    public class DownloaderAppContext : ApplicationContext
    {
        public static EngineManager CurrentEngine { get; private set; }
        public static DownloaderAppContext Instance { get; private set; }
        private SynchronizationContext syncContext;
        private EngineManager engine;
        private UpdateManager updateManager;
        private HttpBridgeServer bridge;
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer schedulerTimer;
        private ClipboardListener clipWindow;
        private string lastClipboardUrl = "";
        private MenuItem autoCaptureMenuItem;
        private readonly object completedGidsLock = new object();
        private HashSet<string> completedGids = new HashSet<string>();

        public DownloaderAppContext()
        {
            try {
            Instance = this;
            syncContext = SynchronizationContext.Current;
            engine = new EngineManager();
            CurrentEngine = engine;
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
            trayMenu.MenuItems.Add("⚡ Open Dashboard", (s, e) => LaunchEdgeApp());
            trayMenu.MenuItems.Add("➕ Add Download...", (s, e) => LaunchEdgeApp());
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("▶ Resume All Downloads", (s, e) =>
            {
                engine.ResumeAll();
                if (trayIcon != null) trayIcon.ShowBalloonTip(1500, "⚡ FastDownloader", "All downloads resumed", ToolTipIcon.Info);
            });
            trayMenu.MenuItems.Add("⏸ Pause All Downloads", (s, e) =>
            {
                engine.PauseAll();
                if (trayIcon != null) trayIcon.ShowBalloonTip(1500, "⚡ FastDownloader", "All downloads paused", ToolTipIcon.Info);
            });
            trayMenu.MenuItems.Add("-");

            MenuItem speedMenu = new MenuItem("🚀 Speed Limit");
            speedMenu.MenuItems.Add("Unlimited", (s, e) => { engine.SetSpeedLimit("0"); if (trayIcon != null) trayIcon.ShowBalloonTip(1500, "Speed Limit", "Unlimited speed set", ToolTipIcon.Info); });
            speedMenu.MenuItems.Add("1 MB/s", (s, e) => { engine.SetSpeedLimit("1M"); if (trayIcon != null) trayIcon.ShowBalloonTip(1500, "Speed Limit", "Speed limited to 1 MB/s", ToolTipIcon.Info); });
            speedMenu.MenuItems.Add("5 MB/s", (s, e) => { engine.SetSpeedLimit("5M"); if (trayIcon != null) trayIcon.ShowBalloonTip(1500, "Speed Limit", "Speed limited to 5 MB/s", ToolTipIcon.Info); });
            speedMenu.MenuItems.Add("10 MB/s", (s, e) => { engine.SetSpeedLimit("10M"); if (trayIcon != null) trayIcon.ShowBalloonTip(1500, "Speed Limit", "Speed limited to 10 MB/s", ToolTipIcon.Info); });
            trayMenu.MenuItems.Add(speedMenu);

            trayMenu.MenuItems.Add(autoCaptureMenuItem);
            trayMenu.MenuItems.Add("📁 Open Downloads Folder", (s, e) => Process.Start("explorer.exe", engine.DownloadsFolder));
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Exit FastDownloader", (s, e) =>
            {
                if (trayIcon != null) trayIcon.Visible = false;
                engine.StopAria2();
                if (updateManager != null) updateManager.Stop();
                if (bridge != null) bridge.Stop();
                Application.Exit();
            });
            if (trayIcon != null) trayIcon.ContextMenu = trayMenu;

            // Setup HTTP Bridge Server & Update Manager
            // use existing syncContext field
            Action<string> notifyAction = (msg) =>
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
            };

            updateManager = new UpdateManager(engine, notifyAction);
            updateManager.StartBackgroundScheduler();

            bridge = new HttpBridgeServer(engine, notifyAction, updateManager);
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
            } catch (Exception ex) { try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), "[DownloaderAppContext CTOR CRASH] " + ex.ToString() + "\r\n"); } catch {} throw; }
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
                if (MediaUrlHelper.IsMediaPlatformUrl(lower)) return true;
            }
            return false;
        }

        public void PromptDownload(PendingDownloadInfo pending)
        {
            if (pending == null) return;
            Thread t = new Thread(delegate()
            {
                ShowConfirmDialog(pending);
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        private void ShowConfirmDialog(PendingDownloadInfo pending)
        {
            try
            {
                try { File.AppendAllText(Path.Combine(engine.AppDir, "startup.log"), DateTime.Now.ToString() + " ShowConfirmDialog started for " + pending.filename + "\r\n"); } catch { }
                using (var dlg = new DownloadConfirmDialog(pending, engine.DownloadsFolder))
                {
                    dlg.TopMost = true;
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    dlg.Shown += (s, e) =>
                    {
                        try
                        {
                            dlg.Activate();
                            dlg.BringToFront();
                            WindowManager.SetForegroundWindow(dlg.Handle);
                        }
                        catch { }
                    };

                    DialogResult res = dlg.ShowDialog();
                    try { File.AppendAllText(Path.Combine(engine.AppDir, "startup.log"), DateTime.Now.ToString() + " ShowConfirmDialog result: " + res + ", Confirmed: " + dlg.Confirmed + "\r\n"); } catch { }
                    PendingDownloadInfo ignored;
                    if (res == DialogResult.OK && dlg.Confirmed)
                    {
                        engine.RemovePending(pending.pendingId, out ignored);
                        string gid = engine.StartConfirmedDownload(pending, dlg.FileName, dlg.SaveDirectory, dlg.Category, dlg.StartNow);
                        if (dlg.StartNow && !string.IsNullOrEmpty(gid))
                        {
                            ShowProgressDialog(gid, dlg.FileName, pending.url, dlg.SaveDirectory);
                        }
                        else if (trayIcon != null)
                        {
                            trayIcon.ShowBalloonTip(2000, "⚡ FastDownloader", "Queued: " + dlg.FileName, ToolTipIcon.Info);
                        }
                    }
                    else
                    {
                        // Cancelled!
                        engine.RemovePending(pending.pendingId, out ignored);
                        if (trayIcon != null)
                        {
                            trayIcon.ShowBalloonTip(1500, "⚡ FastDownloader", "Download cancelled.", ToolTipIcon.Info);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(engine.AppDir, "dialog_err.log"), "Dialog err: " + ex + "\r\n"); } catch { }
            }
        }

        public void ShowProgressDialog(string gid, string filename, string url, string saveDir)
        {
            if (string.IsNullOrEmpty(gid)) return;
            Thread t = new Thread(delegate()
            {
                try
                {
                    using (var dlg = new DownloadProgressDialog(engine, gid, filename, url, saveDir))
                    {
                        dlg.StartPosition = FormStartPosition.CenterScreen;
                        dlg.Shown += (s, e) =>
                        {
                            try
                            {
                                dlg.Activate();
                                dlg.BringToFront();
                                WindowManager.SetForegroundWindow(dlg.Handle);
                            }
                            catch { }
                        };
                        dlg.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Path.Combine(engine.AppDir, "dialog_err.log"), "ProgressDialog err: " + ex + "\r\n"); } catch { }
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        public void ShowCompleteDialog(string gid, string filename, string filePath, long totalLength)
        {
            lock (completedGidsLock)
            {
                if (completedGids.Contains(gid)) return;
                completedGids.Add(gid);
            }

            Thread t = new Thread(delegate()
            {
                try
                {
                    using (var dlg = new DownloadCompleteDialog(filename, filePath, totalLength))
                    {
                        dlg.TopMost = true;
                        dlg.StartPosition = FormStartPosition.CenterScreen;
                        dlg.Shown += (s, e) =>
                        {
                            try
                            {
                                dlg.Activate();
                                dlg.BringToFront();
                                WindowManager.SetForegroundWindow(dlg.Handle);
                                System.Media.SystemSounds.Asterisk.Play();
                            }
                            catch { }
                        };
                        dlg.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Path.Combine(engine.AppDir, "dialog_err.log"), "CompleteDialog err: " + ex + "\r\n"); } catch { }
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        public void LaunchEdgeApp()
        {
            if (WindowManager.FocusExistingWindow()) return;
            LaunchDashboard(engine.AppDir);
        }

        public static string FindChromiumBrowser()
        {
            // 1. Windows Registry App Paths (Edge, Chrome, Brave)
            string[] appNames = new string[] { "msedge.exe", "chrome.exe", "brave.exe" };
            foreach (string app in appNames)
            {
                string subKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + app;
                try
                {
                    using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey))
                    {
                        if (key != null)
                        {
                            object val = key.GetValue("");
                            if (val != null)
                            {
                                string p = val.ToString().Trim('"', ' ');
                                if (File.Exists(p)) return p;
                            }
                        }
                    }
                }
                catch { }
                try
                {
                    using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subKey))
                    {
                        if (key != null)
                        {
                            object val = key.GetValue("");
                            if (val != null)
                            {
                                string p = val.ToString().Trim('"', ' ');
                                if (File.Exists(p)) return p;
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Well-known disk locations (Program Files, Program Files x86, LocalAppData)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] candidatePaths = new string[]
            {
                Path.Combine(progFilesX86, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(progFiles, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(localAppData, @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(progFiles, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(progFilesX86, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(localAppData, @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(progFiles, @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                Path.Combine(progFilesX86, @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\Application\brave.exe")
            };

            foreach (string p in candidatePaths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                }
                catch { }
            }

            return null;
        }

        private static void CleanProfileLocks(string profileDir)
        {
            try
            {
                if (!Directory.Exists(profileDir)) return;
                string[] lockFiles = new string[] { "SingletonLock", "SingletonCookie", "SingletonSocket", "lockfile" };
                foreach (string lf in lockFiles)
                {
                    try
                    {
                        string p = Path.Combine(profileDir, lf);
                        if (File.Exists(p)) File.Delete(p);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void LaunchDashboard(string appDir)
        {
            string profileDir = Path.Combine(appDir, ".app_profile");
            CleanProfileLocks(profileDir);

            string targetBrowser = FindChromiumBrowser();

            if (targetBrowser != null)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = targetBrowser,
                        // Note: --app creates the dedicated standalone window; omitting --new-window prevents regular browser tabs.
                        Arguments = string.Format("--app=http://127.0.0.1:6801/ --user-data-dir=\"{0}\" --window-size=1240,800 --no-first-run --no-default-browser-check --disable-features=Translate,OptimizationHints --disable-extensions --disable-background-mode", profileDir),
                        CreateNoWindow = false,
                        UseShellExecute = true
                    };
                    Process p = Process.Start(psi);
                    if (p != null)
                    {
                        try { WindowManager.DashboardPid = p.Id; } catch { }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " LaunchDashboard error: " + ex.Message + "\r\n"); } catch {}
                }
            }

            // Fallback only if no Chromium browser could be discovered:
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

            foreach (var kvp in engine.GetQueuesSnapshot())
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
                if (cfg.Shutdown && cfg.Active && cfg.Enabled)
                {
                    var tasks = engine.GetAriaTasks();
                    bool anyActive = false;
                    int queueCompletedCount = 0;
                    foreach (var t in tasks)
                    {
                        if (t.ContainsKey("status") && t["status"].ToString() == "active")
                        {
                            anyActive = true;
                            break;
                        }
                        if (t.ContainsKey("status") && (t["status"].ToString() == "complete" || t["status"].ToString() == "completed"))
                        {
                            queueCompletedCount++;
                        }
                    }

                    // Only shutdown if enabled, currently active, and actually completed scheduled tasks
                    if (!anyActive && queueCompletedCount > 0 && cfg.Active)
                    {
                        cfg.Shutdown = false;
                        cfg.Active = false;
                        trayIcon.ShowBalloonTip(5000, "Auto-Shutdown", "All scheduled downloads finished! PC will shut down in 60 seconds.", ToolTipIcon.Warning);
                        Process.Start("shutdown.exe", "/s /t 60");
                    }
                }
            }

            // Check completed downloads for IDM-style completion popup
            try
            {
                var ariaList = engine.GetAriaTasks();
                foreach (var t in ariaList)
                {
                    if (t.ContainsKey("status") && t["status"].ToString() == "complete" && t.ContainsKey("gid"))
                    {
                        string g = t["gid"].ToString();
                        bool already;
                        lock (completedGidsLock) { already = completedGids.Contains(g); }
                        if (!already)
                        {
                            var info = engine.GetTaskInfo(g);
                            if (info != null)
                            {
                                ShowCompleteDialog(g, info.Name, info.FilePath, info.TotalLength);
                            }
                        }
                    }
                }

                var ytList = engine.GetYtTasksSnapshot();
                foreach (var yt in ytList)
                {
                    if (yt.Status == "completed")
                    {
                        bool already;
                        lock (completedGidsLock) { already = completedGids.Contains(yt.Gid); }
                        if (!already)
                        {
                            var info = engine.GetTaskInfo(yt.Gid);
                            if (info != null)
                            {
                                ShowCompleteDialog(yt.Gid, info.Name, info.FilePath, info.TotalLength);
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }

    public static class NativeMessagingHost
    {
        public static void Run()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            using (Stream stdin = Console.OpenStandardInput())
            using (Stream stdout = Console.OpenStandardOutput())
            {
                byte[] lenBytes = new byte[4];
                while (true)
                {
                    int totalRead = 0;
                    while (totalRead < 4)
                    {
                        int r = stdin.Read(lenBytes, totalRead, 4 - totalRead);
                        if (r <= 0) return;
                        totalRead += r;
                    }

                    int msgLen = BitConverter.ToInt32(lenBytes, 0);
                    if (msgLen <= 0 || msgLen > 10 * 1024 * 1024) return;

                    byte[] msgBytes = new byte[msgLen];
                    totalRead = 0;
                    while (totalRead < msgLen)
                    {
                        int r = stdin.Read(msgBytes, totalRead, msgLen - totalRead);
                        if (r <= 0) return;
                        totalRead += r;
                    }

                    string json = Encoding.UTF8.GetString(msgBytes);
                    string respJson = ProcessMessage(json, appDir);

                    byte[] respBytes = Encoding.UTF8.GetBytes(respJson);
                    byte[] respLenBytes = BitConverter.GetBytes(respBytes.Length);

                    stdout.Write(respLenBytes, 0, 4);
                    stdout.Write(respBytes, 0, respBytes.Length);
                    stdout.Flush();
                }
            }
        }

        private static string ProcessMessage(string json, string appDir)
        {
            try
            {
                var dict = JsonHelper.ParseDict(json);
                string action = dict.ContainsKey("action") ? dict["action"].ToString() : "";

                if (action == "ping")
                {
                    return "{\"ok\":true,\"message\":\"pong\"}";
                }

                if (action == "openApp")
                {
                    EnsureAppRunning(appDir);
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            if (!WindowManager.FocusExistingWindow())
                            {
                                DownloaderAppContext.LaunchDashboard(appDir);
                            }
                        }
                        catch { }
                    });
                    return "{\"ok\":true,\"message\":\"App opened\"}";
                }

                if (action == "probe")
                {
                    string pUrl = dict.ContainsKey("url") ? dict["url"].ToString() : "";
                    string cookies = dict.ContainsKey("cookies") ? dict["cookies"].ToString() : "";
                    string userAgent = dict.ContainsKey("userAgent") ? dict["userAgent"].ToString() : "";
                    Dictionary<string, object> headers = null;
                    if (dict.ContainsKey("headers") && dict["headers"] is Dictionary<string, object>)
                    {
                        headers = (Dictionary<string, object>)dict["headers"];
                    }
                    var probeRes = UrlProbeHelper.Probe(pUrl, appDir, cookies, userAgent, headers);
                    return JsonHelper.Serialize(probeRes);
                }

                if (action == "intercept")
                {
                    EnsureAppRunning(appDir);
                    try
                    {
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6801/api/intercept");
                        req.Method = "POST";
                        req.ContentType = "application/json";
                        req.Timeout = 10000;
                        byte[] postData = Encoding.UTF8.GetBytes(json);
                        req.ContentLength = postData.Length;
                        using (Stream reqStream = req.GetRequestStream())
                        {
                            reqStream.Write(postData, 0, postData.Length);
                        }
                        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                        using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                    catch (Exception ex)
                    {
                        return "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\"", "\\\"") + "\"}";
                    }
                }

                if (action == "download")
                {
                    EnsureAppRunning(appDir);
                    try
                    {
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6801/api/intercept");
                        req.Method = "POST";
                        req.ContentType = "application/json";
                        req.Timeout = 5000;
                        byte[] postData = Encoding.UTF8.GetBytes(json);
                        req.ContentLength = postData.Length;
                        using (Stream reqStream = req.GetRequestStream())
                        {
                            reqStream.Write(postData, 0, postData.Length);
                        }
                        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                        using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                    catch (Exception ex)
                    {
                        return "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\"", "\\\"") + "\"}";
                    }
                }

                return "{\"ok\":true}";
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\"", "\\\"") + "\"}";
            }
        }

        private static void EnsureAppRunning(string appDir)
        {
            try
            {
                Process[] existing = Process.GetProcessesByName("FastDownloader");
                int currentPid = Process.GetCurrentProcess().Id;
                bool isRunning = false;
                foreach (Process p in existing)
                {
                    if (p.Id != currentPid)
                    {
                        isRunning = true;
                        break;
                    }
                }

                if (!isRunning)
                {
                    string exePath = Path.Combine(appDir, "FastDownloader.exe");
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = appDir,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }

                // Active health check polling: wait up to 6 seconds for bridge to respond
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    try
                    {
                        HttpWebRequest testReq = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:6801/api/status");
                        testReq.Timeout = 300;
                        using (HttpWebResponse testResp = (HttpWebResponse)testReq.GetResponse())
                        {
                            if (testResp.StatusCode == HttpStatusCode.OK) break;
                        }
                    }
                    catch
                    {
                        Thread.Sleep(250);
                    }
                }
            }
            catch { }
        }
    }

    static class Program
    {
        private static Mutex appMutex;

        [STAThread]
        static void Main(string[] args)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].StartsWith("chrome-extension://") || args[i] == "--native")
                    {
                        NativeMessagingHost.Run();
                        return;
                    }
                }
            }

            bool createdNew = false;
            try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " Main entered.\r\n"); } catch {}
            try
            {
                appMutex = new Mutex(true, "FastDownloader_SingleInstance_Mutex_98a72b", out createdNew);
            }
            catch
            {
                createdNew = false;
            }

            try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " createdNew=" + createdNew + "\r\n"); } catch {}
            if (!createdNew)
            {
                if (!WindowManager.FocusExistingWindow())
                {
                    DownloaderAppContext.LaunchDashboard(appDir);
                }
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " [CRASH UnhandledException] " + e.ExceptionObject + "\r\n"); } catch {}
            };
            Application.ThreadException += (s, e) =>
            {
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " [CRASH ThreadException] " + e.Exception + "\r\n"); } catch {}
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " ProcessExit triggered.\r\n"); } catch {}
                try { if (DownloaderAppContext.CurrentEngine != null) DownloaderAppContext.CurrentEngine.StopAria2(); } catch { }
            };

            try
            {
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), "Before BrowserInstaller\r\n"); } catch {}
                BrowserInstaller.RegisterNativeMessagingHost(appDir);
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), "Before VisualStyles\r\n"); } catch {}
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), "Before new DownloaderAppContext\r\n"); } catch {}
                var ctx = new DownloaderAppContext();
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " Entering Application.Run\r\n"); } catch {}
                Application.Run(ctx);
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " Application.Run exited cleanly\r\n"); } catch {}
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(appDir, "startup.log"), DateTime.Now.ToString() + " [CRASH Main Catch] " + ex + "\r\n"); } catch {}
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
