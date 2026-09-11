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

    public static string Describe(DeleteOutcome outcome, bool toRecycleBin) => outcome switch
    {
        DeleteOutcome.Recycled => toRecycleBin ? "（已放入回收站）" : "",
        DeleteOutcome.PermanentlyDeleted => toRecycleBin ? "（回收站不可用，已永久删除）" : "",
        _ => ""
    };
}
