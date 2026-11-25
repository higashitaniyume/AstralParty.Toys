using System.Text.Json;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private bool HandleDeviceFileMessage(string? type, JsonElement root)
    {
        switch (type)
        {
            case "deviceFilesList":
            {
                var target = ResolveDeviceTargetArg(root);
                if (target is null || DeviceBusyGate()) return true;
                var path = ReadDeviceString(root, "path") ?? "";
                var showHidden = ReadDeviceBool(root, "showHidden");
                _deviceBusy = true;
                _ = PushFilesListAsync(target, path, showHidden);
                return true;
            }

            case "deviceFilePreview":
            {
                var target = ResolveDeviceTargetArg(root);
                var path = ReadDeviceString(root, "path") ?? "";
                if (target is null || string.IsNullOrEmpty(path) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = PushFilePreviewAsync(target, path);
                return true;
            }

            case "deviceFileMkdir":
            {
                var target = ResolveDeviceTargetArg(root);
                var name = ReadDeviceString(root, "name") ?? "";
                if (target is null || string.IsNullOrEmpty(name) || DeviceBusyGate()) return true;
                var basePath = ReadDeviceString(root, "basePath") ?? "";
                var showHidden = ReadDeviceBool(root, "showHidden");
                _deviceBusy = true;
                _ = WriteFileOpAsync(() =>
                {
                    DeviceFileProgress($"正在新建文件夹：{name}…", 35, true);
                    return MobileFiles.CreateDirectoryAsync(target, basePath, name);
                }, target, basePath, showHidden);
                return true;
            }

            case "deviceFileRename":
            {
                var target = ResolveDeviceTargetArg(root);
                var path = ReadDeviceString(root, "path") ?? "";
                var newName = ReadDeviceString(root, "newName") ?? "";
                if (target is null || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(newName) || DeviceBusyGate())
                    return true;
                var basePath = ReadDeviceString(root, "basePath") ?? "";
                var showHidden = ReadDeviceBool(root, "showHidden");
                _deviceBusy = true;
                _ = WriteFileOpAsync(() =>
                {
                    DeviceFileProgress($"正在重命名为 {newName}…", 35, true);
                    return MobileFiles.RenameAsync(target, path, newName);
                }, target, basePath, showHidden);
                return true;
            }

            case "deviceFileDelete":
            {
                var target = ResolveDeviceTargetArg(root);
                var path = ReadDeviceString(root, "path") ?? "";
                if (target is null || string.IsNullOrEmpty(path) || DeviceBusyGate()) return true;
                var basePath = ReadDeviceString(root, "basePath") ?? "";
                var showHidden = ReadDeviceBool(root, "showHidden");
                _deviceBusy = true;
                _ = WriteFileOpAsync(() =>
                {
                    DeviceFileProgress($"正在删除 {Path.GetFileName(path)}…", 35, true);
                    return MobileFiles.DeleteAsync(target, path);
                }, target, basePath, showHidden);
                return true;
            }

            case "deviceFileUpload":
            {
                var target = ResolveDeviceTargetArg(root);
                if (target is null || DeviceBusyGate()) return true;
                var basePath = ReadDeviceString(root, "basePath") ?? "";
                var showHidden = ReadDeviceBool(root, "showHidden");
                RunWithDeviceDialog("打开上传文件对话框失败", () =>
                {
                    var picker = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "选择要上传到设备的文件",
                        Filter = "所有文件 (*.*)|*.*",
                        Multiselect = true,
                        CheckFileExists = true,
                        RestoreDirectory = true,
                    };
                    if (!ShowOwnedDialog(picker) || picker.FileNames.Length == 0)
                        return false;

                    _deviceBusy = true;
                    _ = WriteFileOpAsync(
                        () => MobileFiles.UploadFilesAsync(target, basePath, picker.FileNames, progress: DeviceFileProgress),
                        target, basePath, showHidden);
                    return true;
                });
                return true;
            }

            case "deviceFileDownload":
            {
                var target = ResolveDeviceTargetArg(root);
                var path = ReadDeviceString(root, "path") ?? "";
                if (target is null || string.IsNullOrEmpty(path) || DeviceBusyGate()) return true;
                var isDir = ReadDeviceBool(root, "isDir");
                RunWithDeviceDialog("打开保存对话框失败", () =>
                {
                    if (isDir)
                    {
                        var folder = PickFolderWithFallback("选择保存文件夹的位置", null);
                        if (string.IsNullOrEmpty(folder))
                            return false;

                        _deviceBusy = true;
                        _ = WriteFileOpAsync(
                            () => MobileFiles.DownloadDirectoryAsync(target, path, folder, progress: DeviceFileProgress),
                            target, refreshPath: null, showHidden: false);
                        return true;
                    }

                    var save = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "保存设备文件",
                        FileName = Path.GetFileName(path),
                        Filter = "所有文件 (*.*)|*.*",
                        OverwritePrompt = true,
                        RestoreDirectory = true,
                    };
                    if (!ShowOwnedDialog(save) || string.IsNullOrEmpty(save.FileName))
                        return false;

                    _deviceBusy = true;
                    _ = WriteFileOpAsync(
                        () => MobileFiles.DownloadToFileAsync(target, path, save.FileName, progress: DeviceFileProgress),
                        target, refreshPath: null, showHidden: false);
                    return true;
                });
                return true;
            }

            case "deviceFileOpen":
            {
                var target = ResolveDeviceTargetArg(root);
                var path = ReadDeviceString(root, "path") ?? "";
                if (target is null || string.IsNullOrEmpty(path) || DeviceBusyGate()) return true;
                _deviceBusy = true;
                _ = WriteFileOpAsync(
                    () => MobileFiles.OpenLocallyAsync(target, path, progress: DeviceFileProgress),
                    target, refreshPath: null, showHidden: false);
                return true;
            }

            default:
                return false;
        }
    }

    private async Task PushFilesListAsync(MobileTarget target, string path, bool showHidden)
    {
        try
        {
            var result = await Task.Run(() => MobileFiles.ListAsync(target, path, showHidden, progress: DeviceFileProgress));
            Post(new { type = "deviceFilesListed", payload = result });
        }
        catch (Exception ex)
        {
            Post(new
            {
                type = "deviceFilesListed",
                payload = new MobileFileListResult
                {
                    TargetId = target.Id,
                    Kind = target.Kind,
                    Path = path,
                    Error = ex.Message,
                },
            });
        }
        finally
        {
            FinishDeviceBusyOp();
        }
    }

    private Task PushFilePreviewAsync(MobileTarget target, string path) =>
        RunBusyDeviceOpAsync("读取文件预览失败", async () =>
        {
            var result = await Task.Run(() => MobileFiles.ReadPreviewAsync(target, path, progress: DeviceFileProgress));
            Post(new { type = "deviceFilePreview", payload = result });
        });

    private Task WriteFileOpAsync(
        Func<Task<MobileOperationResult>> operation,
        MobileTarget target,
        string? refreshPath,
        bool showHidden) =>
        RunBusyDeviceOpAsync("文件操作失败", async () =>
        {
            var result = await Task.Run(operation);
            Post(new { type = "deviceFilesOpResult", payload = result, targetId = target.Id });
            if (result.Ok && refreshPath is not null)
            {
                var listed = await Task.Run(() => MobileFiles.ListAsync(target, refreshPath, showHidden, progress: DeviceFileProgress));
                Post(new { type = "deviceFilesListed", payload = listed });
            }
        });
}
