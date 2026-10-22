using AddressablesTools;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ResourceModLoader.Module
{
    class AddressableUpdator
    {
        GameModder mod;
        bool hasBackup = false;
        private Process? gameProcess;

        public AddressableUpdator(GameModder gameModder)
        {
            this.mod = gameModder;
        }

        public void startCollectUrl()
        {
            string setting = Path.Combine(mod.basePath, mod.appName + "_Data", "StreamingAssets", "aa", "settings.json");
            string content = File.ReadAllText(setting);
            content = content.Replace("http://127.0.0.1:17549/", "");
            int pIdx = content.IndexOf("AddressablesMainContentCatalogRemoteHash");
            const string FL1 = "m_InternalId\":\"";
            pIdx = content.IndexOf(FL1, pIdx) + FL1.Length;
            
            File.WriteAllText(setting + ".backup", content);
            File.WriteAllText(setting, content.Substring(0, pIdx) + "http://127.0.0.1:17549/" + content.Substring(pIdx));
            hasBackup = true;

            gameProcess = Process.Start(mod.shell.shell,mod.shell.args);
            WaitForData();
            recoveryBackup();
        }
        private void recoveryBackup()
        {
            if (!hasBackup) return;

            string setting = Path.Combine(mod.basePath, mod.appName + "_Data", "StreamingAssets", "aa", "settings.json");
            File.Copy(setting + ".backup", setting, true);
            File.Delete(setting + ".backup");
            hasBackup = false;
        }

        public async Task<bool> UpdateAddressable(string urlHash,string? lastHash,string hash)
        {
            string catalogBase = urlHash.Substring(0,urlHash.LastIndexOf("/"));
            bool result = false;
            HttpClient httpClient = new HttpClient();
            byte[] resp;
            if (lastHash != hash)
            {
                Log.SuccessPartial("已更新" + Path.GetFileNameWithoutExtension(urlHash) + ".json");
                resp = await httpClient.GetByteArrayAsync(urlHash.Replace(".hash", ".json"));
                await File.WriteAllBytesAsync(Path.Combine(mod.presistDir, Path.GetFileNameWithoutExtension(urlHash) + ".json"), resp);
                result = true;
            }else{
                resp = File.ReadAllBytes(Path.Combine(mod.presistDir, Path.GetFileNameWithoutExtension(urlHash) + ".json"));
            }

            ContentCatalogData ccd = AddressablesCatalogFileParser.FromJsonString(Encoding.UTF8.GetString(resp));
            HashSet<string> seen = new HashSet<string>();
            Log.SetupProgress(0);
            foreach(var rll in ccd.Resources)
            {
                foreach (var rl in rll.Value)
                {
                    if (rl.ProviderId != "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider")
                        continue;
                    if (!rl.InternalId.StartsWith("{App.WebServerConfig.Path}"))
                        continue;
                    if (seen.Contains(rl.PrimaryKey))
                        continue;
                    if (rl.Data is WrappedSerializedObject wo && wo.Object is AssetBundleRequestOptions abro)
                    {
                        Log.StepProgress(abro.BundleName,0);
                        string targetURL = catalogBase + rl.InternalId.Replace("{App.WebServerConfig.Path}", "");
                        string targetInfo = Path.Combine(mod.presistDir, "AssetBundles", abro.BundleName, abro.Hash, "__info");
                        string targetPath = Path.Combine(mod.presistDir, "AssetBundles", abro.BundleName, abro.Hash, "__data");

                        if(!File.Exists(targetInfo) || !File.Exists(targetPath))
                        {
                            Log.SuccessPartial("开始下载" + rl.PrimaryKey);
                            byte[] bytes = await httpClient.GetByteArrayAsync(targetURL.Replace("\\","/"));
                            string ts = DateTime.Now.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds.ToString("F0");
                            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                            File.WriteAllBytes(targetPath, bytes);
                            File.WriteAllText(targetInfo, $"-1\n{ts}\n1\n__data\n");
                            Log.SuccessAll("结束 " + rl.PrimaryKey);
                            result = true;
                        }
                    }
                }
            }
            Log.FinalizeProgress();
            File.WriteAllText(Path.Combine(mod.presistDir, Path.GetFileName(urlHash)), hash);
            return result;
        }


        public bool WaitForData(int timeoutSeconds = 60)
        {
            using (HttpListener listener = new HttpListener())
            {
                // 添加监听前缀（必须以 / 结尾）
                listener.Prefixes.Add("http://127.0.0.1:17549/");

                try
                {
                    listener.Start();
                }
                catch (Exception ex)
                {
                    // 启动失败（如端口被占用、权限不足）时返回 null
                    return false;
                }

                // 异步开始等待请求，并同步等待完成或超时
                while (true)
                {
                    IAsyncResult asyncResult = listener.BeginGetContext(null, null);
                    bool received = asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(timeoutSeconds));
                    if (received)
                    {
                        // 获取请求上下文
                        HttpListenerContext context = listener.EndGetContext(asyncResult);
                        // 从 URL 路径提取 data（例如 "/hello" -> "hello"）
                        string data = context.Request.Url.AbsolutePath.TrimStart('/');
                        string oPath = Path.Combine(mod.presistDir, Path.GetFileName(data));
                        string original = File.Exists(oPath) ? File.ReadAllText(oPath) : "";
                        HttpClient httpClient = new HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(5);
                        Stream s = Stream.Null;
                        try
                        {
                            var resp = httpClient.Send(new HttpRequestMessage { RequestUri = new Uri(data) });
                            s = resp.Content.ReadAsStream();
                        }
                        catch (TimeoutException)
                        {
                            Log.Error("无法获取Addressable hash文件，你可以在游戏UI上点击重试来重新获取");
                            context.Response.ContentLength64 = 0;
                            context.Response.OutputStream.Close();
                            continue;
                        }
                        byte[] bytes = new byte[s.Length];
                        s.Read(bytes, 0, bytes.Length);
                        processUrl(data, bytes, original).Wait() ;
                        context.Response.ContentLength64 = s.Length;
                        context.Response.OutputStream.Write(bytes);
                        context.Response.OutputStream.Close();
                        listener.Stop();
                        return true;
                    }
                    else
                    {
                        // 超时，未收到请求
                        listener.Stop();
                        Log.Error("未收到游戏的网络请求");
                        return false;
                    }
                }
            }
        }
        private async Task processUrl(string url, byte[] data, string lastHash)
        {
            recoveryBackup();
            int offset1 = url.IndexOf("/catalog_") + 9;
            int offset2 = url.IndexOf(".hash", offset1);
            string version = url.Substring(offset1, offset2 - offset1);
            string newHashContent = Encoding.ASCII.GetString(data);
            bool requireReboot = false;
            bool isRunning = true;
            Log.Info($"最新版本{version}@{newHashContent}");
            if (lastHash != newHashContent)
            {
                if (gameProcess != null && !gameProcess.HasExited)
                    gameProcess.Kill();
                else
                    foreach (var item in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(mod.executable))) item.Kill();
                isRunning = false;
                requireReboot = true;
            }

            requireReboot |= await UpdateAddressable(url, lastHash, newHashContent);

            if (requireReboot && isRunning)
            {
                if (gameProcess != null && !gameProcess.HasExited)
                    gameProcess.Kill();
                else
                    foreach (var item in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(mod.executable))) item.Kill();
            }

            mod.ReinitAddressableMgr(version);
            mod.ProcessMods(false);
            if (requireReboot)
            {
                Process.Start(mod.shell.shell,mod.shell.args);
            }
        }
    }
}