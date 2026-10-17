namespace AstralParty.Toys.Services;

internal enum DeleteOutcome
{
    Failed,
    Recycled,
    PermanentlyDeleted
}

/// <summary>删除目录时优先送进回收站，让玩家可以后悔；回收站不可用时才退回永久删除。</summary>
internal static class RecycleBinHelper
{
    public static DeleteOutcome Delete(string directoryPath, out string error)
    {
        error = "";
        if (!Directory.Exists(directoryPath)) return DeleteOutcome.PermanentlyDeleted;

        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                directoryPath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            if (!Directory.Exists(directoryPath)) return DeleteOutcome.Recycled;
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        try
        {
            Directory.Delete(directoryPath, recursive: true);
            error = "";
            return DeleteOutcome.PermanentlyDeleted;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return DeleteOutcome.Failed;
        }
    }

    /// <summary>
    /// 把单个文件送进 Windows 回收站（和资源管理器里按 Delete 一样，可以还原）。
    /// 回收站不可用时**不删除**，返回 <c>Recycled=false</c> 让调用方保留文件并如实提示 ——
    /// 这里刻意不退回永久删除：<c>FOF_ALLOWUNDO</c> 在没有回收站的卷上会被忽略而直接删掉文件，
    /// 所以先用 <see cref="VolumeHasRecycleBin"/> 挡住那种情况。
    /// </summary>
    public static (bool Recycled, string Error) TryRecycleFile(string path)
    {
        if (!File.Exists(path)) return (true, "");
        if (!VolumeHasRecycleBin(path)) return (false, "所在磁盘没有回收站");
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return File.Exists(path) ? (false, "文件仍被占用，没能移入回收站") : (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>该路径所在卷是否有回收站（<c>$Recycle.Bin</c>）。没有就别指望能「回收」，只能保留文件。</summary>
    internal static bool VolumeHasRecycleBin(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return !string.IsNullOrEmpty(root) && Directory.Exists(Path.Combine(root, "$Recycle.Bin"));
        }
        catch
        {
            return false;
        }
    }

    public static string Describe(DeleteOutcome outcome, bool toRecycleBin) => outcome switch
    {
        DeleteOutcome.Recycled => toRecycleBin ? "（已放入回收站）" : "",
        DeleteOutcome.PermanentlyDeleted => toRecycleBin ? "（回收站不可用，已永久删除）" : "",
        _ => ""
    };
}
