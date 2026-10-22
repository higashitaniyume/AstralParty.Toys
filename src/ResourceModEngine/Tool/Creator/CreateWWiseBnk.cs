using ResourceModLoader.Tool.WWiseTool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ResourceModLoader.Mod.Item.ModJsonItem;

namespace ResourceModLoader.Tool.Creator
{
    internal class CreateWWiseBnk
    {
        public static void FindSoundBankAndPatch(CLI cli, ModDescription mod, GameModder modder, string dir)
        {
            string name = cli.WaitInputText("输入要查找的WWise SoundBank文件名，留空取消（使用该工具需要你安装WWise Studio，以及可能需要FFMPEG）");
            if (name == "") return;
            var l = modder.scan.GetAllBundleContainerName();
            WwiseBank? bnk = null;
            string bundleBase = "";
            foreach (var d in l)
            {
                foreach (var (c, n) in d.Value)
                {
                    if (n == name)
                    {
                        var (m, a) = modder.scan.GetBundle(d.Key);

                        foreach (var file in a.file.AssetInfos)
                        {
                            var field = m.GetBaseField(a, file);
                            if (field == null || field["m_Name"].IsDummy)
                                continue;
                            if (field["m_Name"].AsString != name) continue;

                            var dataField = field["RawData.Array"];
                            if (dataField == null || dataField.IsDummy)
                            {
                                continue;
                            }
                            string[] eventNames = [];
                            var eventNamesField = field["eventNames.Array"];
                            if (!eventNamesField.IsDummy)
                            {
                                List<string> tStr = new List<string>();
                                foreach (var cc in eventNamesField.Children)
                                {
                                    tStr.Add(cc.AsString);
                                }
                                eventNames = tStr.ToArray();
                            }
                            bnk = new WwiseBank(dataField.AsByteArray, eventNames);
                            bundleBase = $"{name}@{d.Key}@{c}";
                            break;
                        }
                    }
                    if (bnk != null)
                        break;
                }
                if (bnk != null)
                    break;
            }
            if (bnk == null)
            {
                cli.ShowMessage("文件未找到");
                return;
            }

            List<string> eventNameList = new List<string>();
            eventNameList.Add("取消");
            foreach (var en in bnk.GetAllSoundEvents())
                eventNameList.Add(en.Name);

            int eventNameIdx = cli.WaitSelect("选择要修补的SoundEvent", eventNameList);
            if (eventNameIdx == 0)
                return;

            string eventName = eventNameList[eventNameIdx];
            string path = cli.WaitInputText("输入用于替换的文件路径");

            if (path == "") return;

            bool tmpConvert = false;
            if(Path.GetExtension(path) != ".wav")
            {
                tmpConvert = true;
                path = ConverToWav(cli, path);
                if (path == "") return;
            }
            bool result = ConvertWavToWem(cli, path, Path.Combine(dir, bundleBase + "@" + eventName + ".patch.bnk"));
            if(tmpConvert) {
                File.Delete(path);
            }
            if (result)
            {
                mod.Patch.Add(bundleBase + "@" + eventName + ".patch.bnk");
                cli.ShowMessage("添加成功");
            }
            else
            {
                cli.ShowMessage("添加失败");
            }
        }

        private static string wwiseConsolePath = "";
        private static string ffmpegPath = "ffmpeg";
        public static string ConverToWav(CLI cli,string input)
        {
            cli.Clear();
            if (ffmpegPath == "")
                ffmpegPath = "ffmpeg";
            int ec = 0;
            try
            {
                var processDet = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "",
                        UseShellExecute = true
                    }
                };
                processDet.Start();
                processDet.WaitForExit();
                ec = processDet.ExitCode;
            }
            catch(Exception) {
                ec = -1;
            }
            if (ec!= 0) {
                ffmpegPath = cli.WaitInputText("ffmpeg命令不可用，请输入ffmpeg.exe的路径");
            }
            if (ffmpegPath == "" || !Path.Exists(ffmpegPath))
                return "";

            if (File.Exists($"{input}.auto_convert.wav"))
                File.Delete($"{input}.auto_convert.wav");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{input}\" {input}.auto_convert.wav",
                    UseShellExecute = true
                }
            };
            process.Start();
            process.WaitForExit();
            if(process.ExitCode != 0)
            {
                cli.ShowMessage($"FFMPEG错误 {process.ExitCode}");
                return "";
            }
            return $"{input}.auto_convert.wav";
        }
        public static bool ConvertWavToWem(CLI cli,string wavFilePath, string outputWemFilePath)
        {
            
            if(string.IsNullOrEmpty(wwiseConsolePath))
                wwiseConsolePath = FindWwiseConsole();
            if (string.IsNullOrEmpty(wwiseConsolePath))
            {
                wwiseConsolePath = cli.WaitInputText("未找到WWiseConsole.exe。请手动指定路径");
            }
            if (wwiseConsolePath == "" || !File.Exists(wwiseConsolePath))
            {
                cli.ShowMessage($"未找到WWiseConsole，请检查是否安装了WWise studio");
                return false;
            }

            cli.Clear();
            Log.Info("正在执行WEM转换工作");
            // 2. 创建临时工作目录
            string tempDir = Path.Combine(Path.GetTempPath(), "WavToWem_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 3. 创建 Wwise 项目
                string projectName = "TempProject";
                string projectDir = Path.Combine(tempDir, projectName);
                string projectFile = Path.Combine(projectDir, projectName + ".wproj");
                if (!Directory.Exists(projectDir))
                {
                    var createArgs = $"create-new-project \"{projectFile}\"";
                    if (!RunWwiseConsole(wwiseConsolePath, createArgs))
                    {
                        cli.ShowMessage("错误：创建 Wwise 项目失败。");
                        return false;
                    }
                }

                // 4. 生成 .wsources 文件
                string sourcesFile = Path.Combine(tempDir, "list.wsources");
                string conversionName = "Vorbis Quality High"; // 可根据需要调整
                string sourceFileName = Path.GetFileName(wavFilePath);
                string sourceFileFullPath = Path.GetFullPath(wavFilePath);
                // 注意：wsources 文件中的路径应相对于 Root 属性，或者使用绝对路径
                // 为了简单，我们将 Root 设置为源文件所在目录，并只写入文件名
                string sourceDir = Path.GetDirectoryName(sourceFileFullPath);
                string sourceRelativePath = Path.GetFileName(sourceFileFullPath);

                using (var writer = new StreamWriter(sourcesFile, false, Encoding.UTF8))
                {
                    writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                    writer.WriteLine($"<ExternalSourcesList SchemaVersion=\"1\" Root=\"{sourceDir}\">");
                    writer.WriteLine($"\t<Source Path=\"{sourceRelativePath}\" Conversion=\"{conversionName}\"/>");
                    writer.WriteLine("</ExternalSourcesList>");
                }

                // 5. 执行转换
                string outputDir = Path.Combine(tempDir, "Output");
                Directory.CreateDirectory(outputDir);
                var convertArgs = $"convert-external-source \"{projectFile}\" --source-file \"{sourcesFile}\" --output \"{outputDir}\"";
                if (!RunWwiseConsole(wwiseConsolePath, convertArgs))
                {
                    cli.ShowMessage("错误：Wwise 转换失败。");
                    return false;
                }

                // 6. 移动结果文件
                string windowsDir = Path.Combine(outputDir, "Windows");
                string generatedWem = Path.Combine(windowsDir, sourceRelativePath.Replace(".wav", ".wem"));
                if (!File.Exists(generatedWem))
                {
                    // 尝试查找任何 .wem 文件（如果文件名映射有变化）
                    var wemFiles = Directory.GetFiles(windowsDir, "*.wem");
                    if (wemFiles.Length == 0)
                    {
                        cli.ShowMessage("错误：未找到生成的 WEM 文件。");
                        return false;
                    }
                    generatedWem = wemFiles[0];
                }

                string outputDirFinal = Path.GetDirectoryName(outputWemFilePath);
                if (!string.IsNullOrEmpty(outputDirFinal) && !Directory.Exists(outputDirFinal))
                    Directory.CreateDirectory(outputDirFinal);

                File.Copy(generatedWem, outputWemFilePath, true);
                return true;
            }
            catch (Exception ex)
            {
                cli.ShowMessage($"转换过程中发生异常：{ex.Message}");
                return false;
            }
            finally
            {
                // 7. 清理临时文件（可选，如果调试需要可保留）
                try { Directory.Delete(tempDir, true); }
                catch { /* 忽略清理错误 */ }
            }
        }
        private static string FindWwiseConsole()
        {
            // 优先使用环境变量
            string wwiseRoot = Environment.GetEnvironmentVariable("WWISEROOT");
            if (!string.IsNullOrEmpty(wwiseRoot))
            {
                string candidate = Path.Combine(wwiseRoot, "Authoring", "x64", "Release", "bin", "WwiseConsole.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            // 搜索常见安装位置
            string[] commonPaths = {
            @"C:\Program Files (x86)\Audiokinetic\Wwise",
            @"C:\Program Files\Audiokinetic\Wwise"
        };
            foreach (var basePath in commonPaths)
            {
                if (Directory.Exists(basePath))
                {
                    // 获取最新的版本目录
                    var dirs = Directory.GetDirectories(basePath, "V*");
                    Array.Sort(dirs); // 简单排序，可能不准确，但足够
                    for (int i = dirs.Length - 1; i >= 0; i--)
                    {
                        string candidate = Path.Combine(dirs[i], "Authoring", "x64", "Release", "bin", "WwiseConsole.exe");
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }

            // 尝试从注册表读取（如果 Wwise 注册了路径）
            string regPath = @"SOFTWARE\WOW6432Node\Audiokinetic\Wwise";
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath))
            {
                if (key != null)
                {
                    string installDir = key.GetValue("InstallDir") as string;
                    if (!string.IsNullOrEmpty(installDir))
                    {
                        string candidate = Path.Combine(installDir, "Authoring", "x64", "Release", "bin", "WwiseConsole.exe");
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }
            return null;
        }

        private static bool RunWwiseConsole(string exePath, string arguments)
        {
            Log.Debug("WWISE console: "+exePath + " " + arguments);
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (process.ExitCode != 0)
                {
                    Log.Warn($"WwiseConsole 返回错误码 {process.ExitCode}");
                    Log.Warn(output);
                    if (!string.IsNullOrEmpty(error))
                        Log.Warn($"错误信息：{error}");
                    Log.Wait();
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"运行 WwiseConsole 时发生异常：{ex.Message}");
                return false;
            }
        }
    }
}

