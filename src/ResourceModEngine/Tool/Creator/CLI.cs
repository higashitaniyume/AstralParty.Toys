using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
namespace ResourceModLoader.Tool.Creator
{
    /// <summary>
    /// 三区域CLI界面控制类
    /// </summary>
    public class CLI
    {
        private enum UIMode
        {
            Normal,
            WaitInput,       // 等待整数输入
            WaitInputText,   // 等待字符串输入
            WaitSelect       // 等待键盘选择
        }

        private string _status = "";
        private List<string> _infoLines = new List<string>();
        private bool _isActive;
        private UIMode _mode = UIMode.Normal;
        private string _waitPrompt;
        private List<string> _waitOptions;
        private string _waitDefault;        // 用于整数输入的默认值
        private string _waitTextDefault;    // 用于字符串输入的默认值
        private int _selectedIndex;

        /// <summary>
        /// 初始化CLI界面，清空屏幕并绘制默认布局
        /// </summary>
        public CLI()
        {
            Initialize();
        }

        /// <summary>
        /// 设置状态区文本（顶部一行）
        /// </summary>
        public void SetStatus(string status)
        {
            _status = status ?? "";
            if (_isActive) RefreshUI();
        }

        /// <summary>
        /// 设置信息显示区文本（支持多行，自动滚动）
        /// </summary>
        public void SetInfo(string info)
        {
            _infoLines = (info ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            if (_isActive) RefreshUI();
        }

        /// <summary>
        /// 等待用户输入整数，阻塞直到获得有效输入或使用默认值
        /// </summary>
        public int WaitInput(string prompt, string @default)
        {
            if (!_isActive) Initialize();
            _mode = UIMode.WaitInput;
            _waitPrompt = prompt;
            _waitDefault = @default;
            try
            {
                return ShowIntegerInput();
            }
            finally
            {
                _mode = UIMode.Normal;
                RefreshUI();
            }
        }

        /// <summary>
        /// 等待用户输入字符串，阻塞直到获得输入（直接回车返回默认值）
        /// </summary>
        public string WaitInputText(string prompt, string @default = "")
        {
            if (!_isActive) Initialize();
            _mode = UIMode.WaitInputText;
            _waitPrompt = prompt;
            _waitTextDefault = @default ?? "";
            try
            {
                return ShowTextInput();
            }
            finally
            {
                _mode = UIMode.Normal;
                RefreshUI();
            }
        }

        /// <summary>
        /// 等待用户从选项列表中选择一项（使用键盘上下键选择，回车确认）
        /// </summary>
        public int WaitSelect(string prompt, List<string> options)
        {
            if (!_isActive) Initialize();
            _mode = UIMode.WaitSelect;
            _waitPrompt = prompt;
            _waitOptions = options ?? new List<string>();
            _selectedIndex = 0;

            try
            {
                bool originalCursorVisible = Console.CursorVisible;
                Console.CursorVisible = false;

                while (true)
                {
                    RefreshUI();

                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            _selectedIndex = (_selectedIndex - 1 + _waitOptions.Count) % _waitOptions.Count;
                            break;
                        case ConsoleKey.DownArrow:
                            _selectedIndex = (_selectedIndex + 1) % _waitOptions.Count;
                            break;
                        case ConsoleKey.Enter:
                            return _selectedIndex;
                        default:
                            Console.Beep();
                            break;
                    }
                }
            }
            finally
            {
                _mode = UIMode.Normal;
                Console.CursorVisible = true;
                RefreshUI();
            }
        }

        /// <summary>
        /// 清空屏幕并退出CLI模式，此后屏幕可用于其他输出
        /// </summary>
        public void Clear()
        {
            Console.Clear();
            _isActive = false;
            _mode = UIMode.Normal;
        }

        // ---------- 私有实现 ----------

        private void Initialize()
        {
            Console.Clear();
            _isActive = true;
            _mode = UIMode.Normal;
            RefreshUI();
        }

        private void RefreshUI()
        {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight;

            // 计算输入区所需行数
            int inputLines;
            if (_mode == UIMode.WaitSelect)
                inputLines = (_waitOptions?.Count ?? 0) + 2;
            else if (_mode == UIMode.WaitInput || _mode == UIMode.WaitInputText)
                inputLines = 1;
            else
                inputLines = 1;

            if (1 + inputLines > height)
                throw new InvalidOperationException("输入区所需行数超过控制台高度，请调整窗口大小或减少选项数量。");

            int infoAvailableLines = height - 1 - inputLines;
            if (infoAvailableLines < 0) infoAvailableLines = 0;

            Console.Clear();

            // 状态区
            Console.SetCursorPosition(0, 0);
            WriteLineWithBackground(_status, width);

            // 信息区
            int infoStartRow = 1;
            var visibleInfo = _infoLines.TakeLast(infoAvailableLines).ToList();
            for (int i = 0; i < infoAvailableLines; i++)
            {
                Console.SetCursorPosition(0, infoStartRow + i);
                string line = i < visibleInfo.Count ? visibleInfo[i] : "";
                WriteTruncatedLine(line, width);
            }

            // 输入区
            int inputStartRow = infoStartRow + infoAvailableLines;
            if (_mode == UIMode.Normal)
            {
                Console.SetCursorPosition(0, inputStartRow);
                WriteTruncatedLine("Ready", width);
            }
            else if (_mode == UIMode.WaitInput)
            {
                Console.SetCursorPosition(0, inputStartRow);
                Console.Write(_waitPrompt);
            }
            else if (_mode == UIMode.WaitInputText)
            {
                Console.SetCursorPosition(0, inputStartRow);
                Console.Write(_waitPrompt);
            }
            else if (_mode == UIMode.WaitSelect)
            {
                int currentRow = inputStartRow;
                Console.SetCursorPosition(0, currentRow);
                WriteTruncatedLine(_waitPrompt, width);
                currentRow++;

                for (int i = 0; i < _waitOptions.Count; i++)
                {
                    Console.SetCursorPosition(0, currentRow);
                    if (i == _selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                        WriteTruncatedLine($"{i + 1}. {_waitOptions[i]}", width);
                        Console.ResetColor();
                    }
                    else
                    {
                        WriteTruncatedLine($"{i + 1}. {_waitOptions[i]}", width);
                    }
                    currentRow++;
                }

                Console.SetCursorPosition(0, currentRow);
                Console.Write("Use ↑/↓ to select, Enter to confirm");
            }
        }

        private int ShowIntegerInput()
        {
            while (true)
            {
                RefreshUI();
                string input = Console.ReadLine();

                if (int.TryParse(input, out int value))
                    return value;

                if (!string.IsNullOrEmpty(_waitDefault) && int.TryParse(_waitDefault, out int defaultVal))
                    return defaultVal;

                Console.WriteLine("Invalid input. Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        private string ShowTextInput()
        {
            while (true)
            {
                RefreshUI();
                string input = Console.ReadLine();

                if (!string.IsNullOrEmpty(input))
                    return input;
                return "";
            }
        }
        public bool ShowMessage(string message,bool canCancel = false)
        {
            return 0 == WaitSelect(message,
                canCancel ? ["确定", "取消"] : ["确定"]
               );
        }

        private void WriteLineWithBackground(string text, int width)
        {
            string padded = text.Length > width ? text.Substring(0, width) : text.PadRight(width);
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(padded);
            Console.ResetColor();
        }

        private void WriteTruncatedLine(string line, int width)
        {
            if (line.Length > width)
                line = line.Substring(0, width - 3) + "...";
            Console.Write(line.PadRight(width));
        }
    }
}
