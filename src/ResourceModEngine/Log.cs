using System;

namespace ResourceModLoader
{
    class Log
    {
        /// <summary>Toys 注入的日志出口；为空时退回控制台。</summary>
        public static Action<string>? Sink;
        /// <summary>库模式：不写控制台、不等待按键。</summary>
        public static bool Headless;
        private static bool disablePrefix = false;
        private static bool inProgress = false;
        private static bool padNext = false;
        private static int progressCount = 0;
        private static int progressTotal = 0;

        private static void WriteLine(string message)
        {
            if (Headless)
            {
                Sink?.Invoke(message);
                return;
            }
            if (inProgress || padNext)
            {
                message = "\r\x1b[K" + message;
            }
            Console.WriteLine(message);
            padNext = false;
        }

        public static void SetupProgress(int total)
        {
            inProgress = true;
            progressTotal = total;
            progressCount = 0;
        }

        public static void StepProgress(string desc, int step = 1)
        {
            progressCount += step;
            if (Headless)
            {
                if (progressCount == 1 || progressCount % 50 == 0 || (progressTotal > 0 && progressCount >= progressTotal))
                    Sink?.Invoke($"[{progressCount}/{progressTotal}] {desc}");
                return;
            }

            string message = $"[{progressCount.ToString().PadLeft(3, ' ')}/{progressTotal.ToString().PadLeft(3, ' ')}]";
            if (progressTotal <= 0)
                message = "";
            message += desc;
            int width;
            try { width = Console.WindowWidth; } catch { width = 120; }
            if (width <= 0) width = 120;
            if (message.Length > width - 25)
            {
                int tail = Math.Max(1, width - 65);
                message = message.Substring(0, 30) + "..." + message.Substring(Math.Max(30, message.Length - tail), tail);
            }
            Console.Write("\r\x1b[K" + message);
        }

        public static void FinalizeProgress(string? desc = null)
        {
            if (desc != null)
                WriteLine(desc);
            else
                padNext = true;
            inProgress = false;
            progressCount = 0;
            progressTotal = 0;
        }

        public static void Wait()
        {
            if (Headless) return;
            WriteLine("按任意键继续");
            Console.ReadKey();
        }

        public static void SetPrefixEnable(bool enable)
        {
            disablePrefix = !enable;
        }

        public static void PrefixWriteLine(string prefix, string content)
        {
            if (disablePrefix)
                WriteLine(content);
            else
                WriteLine($"[{prefix}] {content}");
        }

        public static void Info(string t) => PrefixWriteLine("I", t);

        public static void Error(string t) => PrefixWriteLine("E", t);

        public static void Warn(string t) => PrefixWriteLine("W", t);

        public static void Debug(string t) => PrefixWriteLine("D", t);

        public static void SuccessAll(string t) => PrefixWriteLine("SA", t);

        public static void SuccessPartial(string t) => PrefixWriteLine("SP", t);
    }
}
