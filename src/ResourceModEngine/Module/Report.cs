
namespace ResourceModLoader.Module
{
    enum FileStatus
    {
        OK,
        NO_EFFECT,
        OK_PATCH,
        WARNING,
        ERROR
    }
    class Report
    {
        static List<string> files = new List<string>();
        static List<List<string>> warnings = new List<List<string>>();

        static List<FileStatus> statuses = new List<FileStatus>();
        static List<List<string>> effectTo = new List<List<string>>();
        static List<string> belongModPack = new List<string>();

        static Dictionary<string, List<string>> taintBy = new Dictionary<string, List<string>>();

        static List<Tuple<string,string>> modPacks = new List<Tuple<string, string>> { new Tuple<string, string>("","未分类的")};
        static string fallbackModPath = string.Empty;

        static public void SetCurrentModPath(string modPath)
        {
            fallbackModPath = modPath;
            if (modPath != "" && modPacks.FindIndex(t=>t.Item1 == modPath) == -1)
                modPacks.Add(new Tuple<string, string>(fallbackModPath, Path.GetFileName(fallbackModPath)));
        }
        static public string GetCurrentModPath()
        {
            return fallbackModPath;
        }
        static public void AddModPack(string modPackPath,string name)
        {
            modPacks.Add(new Tuple<string, string>(modPackPath, name));
        }
        static public void AddToSameModPack(string newFile,string originalFile)
        {
            SetModPack(newFile, belongModPack[GetModFileId(originalFile)]);
        }
        static public void AddModFile(string name)
        {
            if (files.Contains(name)) return;
            files.Add(name);
            warnings.Add(new List<string>());
            statuses.Add(FileStatus.NO_EFFECT);
            effectTo.Add(new List<string>());
            belongModPack.Add(fallbackModPath);
        }

        static public int GetModFileId(string name)
        {
            if (!files.Contains(name))
                AddModFile(name);
            return files.IndexOf(name);
        }
        static public void AddTaintFile(string modFile, string targetFile)
        {
            var modFileId = GetModFileId(modFile);
            if (!taintBy.ContainsKey(targetFile))
                taintBy[targetFile] = new List<string>();

            taintBy[targetFile].Add(modFile);
            effectTo[modFileId].Add(targetFile);
            if (statuses[modFileId] == FileStatus.NO_EFFECT)
            {
                if (targetFile.EndsWith(".bundle"))
                    statuses[modFileId] = FileStatus.OK_PATCH;
                else
                    statuses[modFileId] = FileStatus.OK;
            }
        }

        static public void Warning(string modFile, string message)
        {
            warnings[GetModFileId(modFile)].Add(message);
            statuses[GetModFileId(modFile)] = FileStatus.WARNING;
        }
        static public void Error(string modFile, string message)
        {
            statuses[GetModFileId(modFile)] = FileStatus.ERROR;
            warnings[GetModFileId(modFile)].Add(message);
        }
        static public void SetModPack(string modFile,string modPackPath)
        {
            belongModPack[GetModFileId(modFile)] = modPackPath;
        }
        static public void Print(string modBase)
        {
            if (files.Count == 0)
            {
                Log.Info("没有mod文件被加载");
                Log.Info(" * 如果你已经删除了mods文件夹内的内容，那么现在所有mod的影响都已经被还原");
                Log.Info(" * 如果你是第一次运行这个程序，接下来你应该向mods文件夹内放入你需要安装的mod并再次运行本程序");
                return;
            }
            Log.SetPrefixEnable(false);
            Log.Info("|======================================================");
            Log.Info("|  污染状况");
            Log.Info("|======================================================");
            foreach (var tainted in taintBy)
            {
                for (int i = 0; i < tainted.Value.Count; i++)
                {
                    var name = tainted.Value[i].Replace(modBase, "").Replace("\\", " / ");
                    var k = i == 0 ? tainted.Key : "".PadRight(tainted.Key.Length, ' ');
                    string str = $"{k} <-- {name}";

                    if (tainted.Key.EndsWith(".bundle"))
                    {
                        Log.SuccessPartial("| " + str);
                    }
                    else
                    {
                        Log.SuccessAll("| " + str);
                    }
                }
            }
            Log.Info("\n\n|======================================================");
            Log.Info("|  加载文件");
            Log.Info("|======================================================");
            bool hasWarning = false;
            for (int i = 0; i < files.Count; i++)
            {
                if (statuses[i] == FileStatus.NO_EFFECT) continue;
                var name = files[i].Replace(modBase, "").Replace("\\", " >");
                var effect = effectTo[i].Count;
                if (statuses[i] == FileStatus.OK)
                    Log.SuccessAll($"| {name}({effect})");
                else if (statuses[i] == FileStatus.OK_PATCH)
                    Log.SuccessPartial($"| {name}({effect})");
                if (statuses[i] == FileStatus.WARNING)
                {
                    hasWarning = true;
                    Log.Warn($"| {name}({effect})");
                }
                if (statuses[i] == FileStatus.ERROR)
                {
                    hasWarning = true;
                    Log.Error($"| {name}({effect})");
                }
            }

            if (hasWarning)
            {
                Log.Info("\n\n|======================================================");
                Log.Info("| 警告和错误");
                Log.Info("|======================================================");
                for (int i = 0; i < files.Count; i++)
                {
                    if (statuses[i] == FileStatus.NO_EFFECT) continue;
                    var name = files[i].Replace(modBase, "").Replace("\\", " >");
                    if (statuses[i] == FileStatus.WARNING)
                    {
                        Log.Warn($"| {name}");
                        int c = 0;
                        foreach (var w in warnings[i])
                        {
                            if (c++ > 6)
                            {
                                Log.Warn($"| ...等总计{warnings[i].Count}个警告");
                                break;
                            }
                            Log.Warn("| \t" + w);
                        }
                    }
                    if (statuses[i] == FileStatus.ERROR)
                    {
                        Log.Error($"| {name}");
                        foreach (var w in warnings[i])
                        {
                            Log.Error("| \t" + w);
                        }
                    }
                }
            }
            else
            {
                Log.Info("\n\n|======================================================");
                Log.SuccessAll("|  加载过程中没有错误和警告");
                Log.Info("|======================================================");
            }

            Log.Info("\n\n|======================================================");
            Log.Info("| Mod列表");
            Log.Info("|======================================================");
            HashSet<string> presentModpackName = new HashSet<string>();
            foreach(var (_mpp,mpn) in modPacks)
            {
                string mpp = _mpp;
                int successCount = 0;
                int errorCount = 0;
                int warningCount = 0;
                for(int i = 0; i< files.Count; i++)
                {
                    if (belongModPack[i] != _mpp) continue;
                    if (statuses[i] == FileStatus.ERROR) errorCount++;
                    else if (statuses[i] == FileStatus.WARNING) warningCount++;
                    else if (statuses[i] == FileStatus.OK || statuses[i] == FileStatus.OK_PATCH) { successCount++; }
                }
                if (successCount == 0 && errorCount == 0 && warningCount == 0)
                    continue;
                if (mpp == "")
                    mpp = "mod根目录";
                if (errorCount > 0)
                    Log.Error("| [错误]Mod " + mpn + "(在" + mpp + ")");
                else if (errorCount > 0)
                    Log.Warn("| [警告]Mod " + mpn + "(在" + mpp + ")");
                else
                    Log.SuccessAll("| [成功]Mod " + mpn + "(在" + mpp + ")");

                if (errorCount > 0)
                    Log.Error($"| \t {errorCount}个文件出现错误");
                if (warningCount > 0)
                    Log.Warn($"| \t {warningCount}个警告");
                if (successCount > 0)
                    Log.SuccessAll($"| \t {successCount}个文件成功应用");
            }
            Log.SetPrefixEnable(true);
        }

        public static (int Errors, int Warnings, string[] Messages) GetSummary()
        {
            var errors = 0;
            var warningCount = 0;
            var messages = new List<string>();
            for (var i = 0; i < files.Count; i++)
            {
                if (i >= statuses.Count) break;
                if (statuses[i] == FileStatus.ERROR)
                {
                    errors++;
                    messages.Add(Path.GetFileName(files[i]) + "：" + string.Join("；", warnings[i]));
                }
                else if (statuses[i] == FileStatus.WARNING)
                {
                    warningCount++;
                }
            }
            return (errors, warningCount, messages.Take(8).ToArray());
        }

        internal static void Reset()
        {
            files.Clear();
            warnings.Clear();
            statuses.Clear();
            effectTo.Clear();
            taintBy.Clear();
            modPacks.Clear();
            belongModPack.Clear();
            modPacks.Add(new Tuple<string, string>("", "未分类的"));
        }
    }
}
