using System.Runtime.InteropServices;
using System.Text;

namespace EBAssistant;

public static class VisioSaveDialogAutomation
{
    private const int BmClick = 0x00F5;

    public static bool WaitAndClickYes(TimeSpan timeout)
    {
        return WaitAndClickYesExcluding(new HashSet<IntPtr>(), timeout);
    }

    public static HashSet<IntPtr> CaptureSaveDialogSnapshot()
    {
        var result = new HashSet<IntPtr>();
        FindEngineeringBaseSaveYesButton(result, collectOnly: true);
        return result;
    }

    public static bool WaitAndClickYesExcluding(IReadOnlySet<IntPtr> excludedDialogs, TimeSpan timeout)
    {
        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            var button = FindEngineeringBaseSaveYesButton(excludedDialogs, collectOnly: false);
            if (button != IntPtr.Zero)
            {
                SendMessage(button, BmClick, IntPtr.Zero, IntPtr.Zero);
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }

    public static bool IsYesButtonText(string text)
    {
        var normalized = NormalizeButtonText(text);
        return normalized.Equals("是", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("是(Y)", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Yes", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNoButtonText(string text)
    {
        var normalized = NormalizeButtonText(text);
        return normalized.Equals("否", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("否(N)", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("No", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCancelButtonText(string text)
    {
        var normalized = NormalizeButtonText(text);
        return normalized.Equals("取消", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Cancel", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSaveQuestionText(string text)
    {
        return text.Contains("要保存修改吗？", StringComparison.Ordinal) ||
            text.Contains("要保存修改吗?", StringComparison.Ordinal);
    }

    private static string NormalizeButtonText(string text)
    {
        return text.Trim()
            .Replace("(&", "(", StringComparison.Ordinal)
            .Replace("&", "", StringComparison.Ordinal);
    }

    private static IntPtr FindEngineeringBaseSaveYesButton(IReadOnlySet<IntPtr> excludedDialogs, bool collectOnly)
    {
        var result = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window))
            {
                return true;
            }

            var title = GetWindowText(window);
            if (!title.Equals("Engineering Base", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (excludedDialogs.Contains(window))
            {
                return true;
            }

            var context = new DialogSearchContext();
            EnumChildWindows(window, (child, _) =>
            {
                var childText = GetWindowText(child);
                if (IsSaveQuestionText(childText))
                {
                    context.HasSaveQuestion = true;
                }

                if (IsYesButtonText(childText))
                {
                    context.YesButton = child;
                }

                if (IsNoButtonText(childText))
                {
                    context.HasNoButton = true;
                }

                if (IsCancelButtonText(childText))
                {
                    context.HasCancelButton = true;
                }

                return true;
            }, IntPtr.Zero);

            if ((context.HasSaveQuestion || context.HasYesNoCancelButtons) && context.YesButton != IntPtr.Zero)
            {
                if (collectOnly && excludedDialogs is HashSet<IntPtr> snapshot)
                {
                    snapshot.Add(window);
                    return true;
                }

                result = context.YesButton;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static string GetWindowText(IntPtr window)
    {
        var builder = new StringBuilder(512);
        GetWindowText(window, builder, builder.Capacity);
        return builder.ToString();
    }

    private sealed class DialogSearchContext
    {
        public bool HasSaveQuestion { get; set; }
        public bool HasNoButton { get; set; }
        public bool HasCancelButton { get; set; }
        public bool HasYesNoCancelButtons => YesButton != IntPtr.Zero && HasNoButton && HasCancelButton;
        public IntPtr YesButton { get; set; }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
