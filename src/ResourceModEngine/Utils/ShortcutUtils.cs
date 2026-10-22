using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsShortcutFactory;

namespace ResourceModLoader.Utils
{
    class ShortcutUtils
    {
        public static void CreateShortcut(string directory, string shortcutName, string targetPath, string arguments,
            string description = null, string iconLocation = null)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string shortcutPath = Path.Combine(directory, string.Format("{0}.lnk", shortcutName));

            // 使用对象初始化器设置所有属性
            using var shortcut = new WindowsShortcut
            {
                Path = targetPath,                               // 指向的目标程序
                WorkingDirectory = Path.GetDirectoryName(targetPath), // 起始位置
                Arguments = arguments,                           // 启动参数
                Description = description,                       // 备注
                IconLocation = string.IsNullOrWhiteSpace(iconLocation) ? targetPath : iconLocation
            };

            shortcut.Save(shortcutPath); // 保存快捷方式
        }
    }
}
