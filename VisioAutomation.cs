using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EBAssistant;

public sealed class VisioAutomation
{
    private object _application;
    private readonly HashSet<int> _openedSourceDocuments = [];

    private VisioAutomation(object application)
    {
        _application = application;
    }

    public static VisioAutomation OpenOrAttach()
    {
        if (TryGetActiveObject("Visio.Application", out var active) &&
            active is not null)
        {
            return new VisioAutomation(active);
        }

        var type = Type.GetTypeFromProgID("Visio.Application")
            ?? throw new InvalidOperationException("未找到 Visio.Application COM 注册。");
        var app = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("无法启动 Visio。");
        Set(app, "Visible", true);
        return new VisioAutomation(app);
    }

    public object OpenSourceDocument(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("源 Visio 文件不存在。", path);
        }

        var document = TryGetOpenSourceDocument(path);
        if (document is null)
        {
            var documents = Get(_application, "Documents");
            document = OpenSourceDocumentFromDocuments(documents, path);
            _openedSourceDocuments.Add(RuntimeHelpers.GetHashCode(document));
        }
        WaitUntil(
            () => Convert.ToInt32(Get(Get(document, "Pages"), "Count")) > 0,
            TimeSpan.FromSeconds(30),
            "源 Visio 文件未完全打开。");
        WaitUntil(
            () => IsDocumentReady(document),
            TimeSpan.FromSeconds(30),
            "源 Visio 文件未进入可响应状态。");
        return document;
    }

    public static object OpenSourceDocumentFromDocuments(object documents, string path)
    {
        var existing = FindOpenDocumentByFullName(documents, path);
        if (existing is not null)
        {
            return existing;
        }

        const int visOpenRO = 2;
        const int visOpenDontList = 8;
        const int visOpenMacrosDisabled = 128;
        return ComInvocation.Call(documents, "OpenEx", path, visOpenRO | visOpenDontList | visOpenMacrosDisabled);
    }

    private object? TryGetOpenSourceDocument(string path)
    {
        try
        {
            var documents = Get(_application, "Documents");
            var document = FindOpenDocumentByFullName(documents, path);
            if (document is not null)
            {
                return document;
            }
        }
        catch
        {
        }

        try
        {
            return Marshal.BindToMoniker(path);
        }
        catch
        {
            return null;
        }
    }

    private static object? FindOpenDocumentByFullName(object documents, string path)
    {
        var count = Convert.ToInt32(ComInvocation.Get(documents, "Count"));
        for (var index = 1; index <= count; index++)
        {
            var document = ComInvocation.Call(documents, "Item", index);
            var fullName = SafeFullName(document);
            if (string.Equals(fullName, path, StringComparison.OrdinalIgnoreCase))
            {
                return document;
            }
        }

        return null;
    }

    public List<VisioGroupShapeInfo> ReadGroupShapes(object document)
    {
        var pages = Get(document, "Pages");
        var page = Call(pages, "Item", 1);
        var shapes = Get(page, "Shapes");
        var count = Convert.ToInt32(Get(shapes, "Count"));
        var result = new List<VisioGroupShapeInfo>();

        for (var index = 1; index <= count; index++)
        {
            var shape = Call(shapes, "Item", index);
            var type = Convert.ToInt32(Get(shape, "Type"));
            if (type != 2)
            {
                continue;
            }

            var name = Convert.ToString(Get(shape, "NameU")) ?? ("Shape" + index);
            result.Add(new VisioGroupShapeInfo(
                name,
                index,
                CellResultIU(shape, "PinX"),
                CellResultIU(shape, "PinY"),
                CellResultIU(shape, "Width"),
                CellResultIU(shape, "Height")));
        }

        return result;
    }

    public HashSet<string> CaptureDocumentSnapshot()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documents = Get(_application, "Documents");
        var count = Convert.ToInt32(Get(documents, "Count"));
        for (var index = 1; index <= count; index++)
        {
            var document = Call(documents, "Item", index);
            result.Add(DocumentKey(document));
        }

        return result;
    }

    public object WaitForOpenedTargetDocument(
        string expectedTemplateName,
        object sourceDocument,
        IReadOnlySet<string> documentsBeforeOpen)
    {
        var sourceFullName = SafeFullName(sourceDocument);
        object? target = null;
        WaitUntil(
            () =>
            {
                target = FindOpenedTargetDocument(expectedTemplateName, sourceFullName, documentsBeforeOpen);
                return target is not null;
            },
            TimeSpan.FromSeconds(60),
            "目标图形模板未完全打开。");

        return target ?? throw new InvalidOperationException("无法取得目标 Visio 文档。");
    }

    private object? FindOpenedTargetDocument(
        string expectedTemplateName,
        string sourceFullName,
        IReadOnlySet<string> documentsBeforeOpen)
    {
        var target = FindOpenedTargetDocumentInApplication(_application, expectedTemplateName, sourceFullName, documentsBeforeOpen);
        if (target is not null)
        {
            return target;
        }

        if (TryGetActiveObject("Visio.Application", out var activeApplication) &&
            activeApplication is not null)
        {
            target = FindOpenedTargetDocumentInApplication(activeApplication, expectedTemplateName, sourceFullName, documentsBeforeOpen);
            if (target is not null)
            {
                _application = activeApplication;
                return target;
            }
        }

        return null;
    }

    private static object? FindOpenedTargetDocumentInApplication(
        object application,
        string expectedTemplateName,
        string sourceFullName,
        IReadOnlySet<string> documentsBeforeOpen)
    {
        try
        {
            var active = ComInvocation.Get(application, "ActiveDocument");
            if (IsTargetDocument(active, expectedTemplateName, sourceFullName, documentsBeforeOpen))
            {
                return active;
            }
        }
        catch
        {
        }

        var documents = ComInvocation.Get(application, "Documents");
        var count = Convert.ToInt32(ComInvocation.Get(documents, "Count"));
        for (var index = 1; index <= count; index++)
        {
            var document = ComInvocation.Call(documents, "Item", index);
            if (IsTargetDocument(document, expectedTemplateName, sourceFullName, documentsBeforeOpen))
            {
                return document;
            }
        }

        return null;
    }

    private static bool IsTargetDocument(
        object? document,
        string expectedTemplateName,
        string sourceFullName,
        IReadOnlySet<string> documentsBeforeOpen)
    {
        if (document is null || !HasEditablePage(document))
        {
            return false;
        }

        var key = DocumentKey(document);
        var name = Convert.ToString(Get(document, "Name")) ?? "";
        if (name.IndexOf(expectedTemplateName, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        var fullName = SafeFullName(document);
        return !documentsBeforeOpen.Contains(key) &&
            !string.IsNullOrEmpty(fullName) &&
            !string.Equals(fullName, sourceFullName, StringComparison.OrdinalIgnoreCase);
    }

    private static string DocumentKey(object document)
    {
        var fullName = SafeFullName(document);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        try
        {
            var name = Convert.ToString(Get(document, "Name")) ?? "";
            var index = Convert.ToString(Get(document, "Index")) ?? "";
            return name + "#" + index;
        }
        catch
        {
            return RuntimeHelpers.GetHashCode(document).ToString(CultureInfo.InvariantCulture);
        }
    }

    public object WaitForActiveTargetDocument(string expectedTemplateName, object sourceDocument)
    {
        var sourceFullName = SafeFullName(sourceDocument);
        WaitUntil(
            () =>
            {
                var document = TryGetActiveDocument();
                if (document is null || !HasEditablePage(document))
                {
                    return false;
                }

                var name = Convert.ToString(Get(document, "Name")) ?? "";
                if (name.IndexOf(expectedTemplateName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                var fullName = SafeFullName(document);
                return !string.IsNullOrEmpty(fullName) &&
                    !string.Equals(fullName, sourceFullName, StringComparison.OrdinalIgnoreCase);
            },
            TimeSpan.FromSeconds(30),
            "目标图形模板未完全打开。");

        return TryGetActiveDocument() ?? throw new InvalidOperationException("无法取得活动 Visio 文档。");
    }

    public void CopyGroupShapeFromSource(object sourceDocument, int sourceShapeIndex)
    {
        var pages = Get(sourceDocument, "Pages");
        var page = Call(pages, "Item", 1);
        var shapes = Get(page, "Shapes");
        var shape = Call(shapes, "Item", sourceShapeIndex);
        CallVoid(shape, "Copy");
    }

    public static void EnsureSourceShapeReadyForCopy(object sourceDocument, string expectedSourcePath, int sourceShapeIndex)
    {
        var actualFullName = SafeFullName(sourceDocument);
        if (!string.Equals(actualFullName, expectedSourcePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("复制前校验失败：当前源 Visio 文件不是用户选择的源文件。");
        }

        if (!HasEditablePage(sourceDocument))
        {
            throw new InvalidOperationException("复制前校验失败：源 Visio 文件没有可用页面。");
        }

        var pages = Get(sourceDocument, "Pages");
        var page = Call(pages, "Item", 1);
        var shapes = Get(page, "Shapes");
        var shape = Call(shapes, "Item", sourceShapeIndex);
        var type = Convert.ToInt32(Get(shape, "Type"));
        if (type != 2)
        {
            throw new InvalidOperationException("复制前校验失败：指定源图形不是组合图形。");
        }
    }

    public void PasteIntoTarget(object targetDocument)
    {
        var pages = Get(targetDocument, "Pages");
        var page = Call(pages, "Item", 1);
        var before = Convert.ToInt32(Get(Get(page, "Shapes"), "Count"));
        CallVoid(page, "Paste");
        WaitUntil(
            () => Convert.ToInt32(Get(Get(page, "Shapes"), "Count")) > before,
            TimeSpan.FromSeconds(10),
            "粘贴后未检测到新增图形。");
    }

    public static void EnsureTargetDocumentReadyForPaste(
        object targetDocument,
        object sourceDocument,
        string expectedTemplateName,
        IReadOnlySet<string> documentsBeforeOpen)
    {
        var sourceFullName = SafeFullName(sourceDocument);
        if (!IsTargetDocument(targetDocument, expectedTemplateName, sourceFullName, documentsBeforeOpen))
        {
            throw new InvalidOperationException("粘贴前校验失败：当前目标不是本次打开的 EB 图形模板。");
        }

    }

    public void SaveAndCloseDocument(object targetDocument)
    {
        var key = DocumentKey(targetDocument);
        CloseDocumentWindowWithSavePrompt(
            targetDocument,
            VisioSaveDialogAutomation.WaitAndClickYes,
            clickSaveYesExcluding: VisioSaveDialogAutomation.WaitAndClickYesExcluding);
        WaitUntil(
            () => !ContainsDocumentKey(key),
            TimeSpan.FromSeconds(30),
            "图形模板关闭保存后仍在 Visio 文档列表中。");
    }

    public void CloseSourceDocumentIfOpenedByAutomation(object sourceDocument)
    {
        if (!_openedSourceDocuments.Remove(RuntimeHelpers.GetHashCode(sourceDocument)))
        {
            return;
        }

        CloseDocumentWithoutSaveResponse(_application, sourceDocument);
    }

    public static void TriggerKeyboardCloseWithSavePrompt(
        object application,
        object targetDocument,
        Action<string> sendKeys,
        Action<int> wait,
        Func<TimeSpan, bool> clickSaveYes,
        Func<object, object, Action<int>, bool>? ensureTargetForeground = null)
    {
        try { ComInvocation.Set(application, "Visible", true); }
        catch { }

        try { ComInvocation.CallVoid(targetDocument, "Activate"); }
        catch { }

        wait(200);
        var isTargetForeground = ensureTargetForeground is null
            ? EnsureTargetVisioDocumentForeground(application, targetDocument, wait)
            : ensureTargetForeground(application, targetDocument, wait);
        if (!isTargetForeground)
        {
            throw new InvalidOperationException("无法确认当前前台窗口是目标 Visio 图形模板，已停止关闭动作，避免误关其他窗口。");
        }

        sendKeys("%{F4}");
        if (!clickSaveYes(TimeSpan.FromSeconds(30)))
        {
            throw new InvalidOperationException("未找到 EB 图形模板保存确认弹窗中的“是”按钮。");
        }
    }

    public static void CloseDocumentWithoutSaveResponse(object application, object targetDocument)
    {
        CloseDocumentWithAlertResponse(application, targetDocument, 7, "关闭源 Visio 文件失败：");
    }

    public static void CloseDocumentWindowWithSavePrompt(
        object targetDocument,
        Func<TimeSpan, bool> clickSaveYes,
        Func<IntPtr, bool>? postClose = null,
        Func<IReadOnlySet<IntPtr>, TimeSpan, bool>? clickSaveYesExcluding = null,
        Action<string>? sendKeys = null,
        Action<int>? wait = null,
        Func<object, object, Action<int>, bool>? ensureTargetForeground = null)
    {
        var existingSaveDialogs = VisioSaveDialogAutomation.CaptureSaveDialogSnapshot();
        var windowHandle = GetPrimaryDocumentWindowHandle(targetDocument);
        if (windowHandle == IntPtr.Zero)
        {
            var application = ComInvocation.Get(targetDocument, "Application");
            TriggerKeyboardCloseWithSavePrompt(
                application,
                targetDocument,
                sendKeys ?? SendKeysToForeground,
                wait ?? Thread.Sleep,
                timeout => clickSaveYesExcluding is null
                    ? clickSaveYes(timeout)
                    : clickSaveYesExcluding(existingSaveDialogs, timeout),
                ensureTargetForeground);
            return;
        }

        var clickTask = Task.Run(() => clickSaveYesExcluding is null
            ? clickSaveYes(TimeSpan.FromSeconds(30))
            : clickSaveYesExcluding(existingSaveDialogs, TimeSpan.FromSeconds(30)));
        var closeSent = postClose is null ? PostMessage(windowHandle, 0x0010, IntPtr.Zero, IntPtr.Zero) : postClose(windowHandle);
        if (!closeSent)
        {
            throw new InvalidOperationException("无法向目标 Visio 图形模板窗口发送关闭消息。");
        }

        if (!clickTask.Wait(TimeSpan.FromSeconds(35)) || !clickTask.Result)
        {
            throw new InvalidOperationException("未找到 EB 图形模板保存确认弹窗中的“是”按钮。");
        }
    }

    private static IntPtr GetPrimaryDocumentWindowHandle(object document)
    {
        try
        {
            var windows = ComInvocation.Get(document, "Windows");
            var count = Convert.ToInt32(ComInvocation.Get(windows, "Count"));
            for (var index = 1; index <= count; index++)
            {
                var window = ComInvocation.Call(windows, "Item", index);
                var handle = TryGetWindowHandle(window);
                if (handle != IntPtr.Zero)
                {
                    return handle;
                }
            }
        }
        catch
        {
        }

        return IntPtr.Zero;
    }

    private static void SendKeysToForeground(string keys)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("未找到 WScript.Shell，无法发送关闭快捷键。");
        var shell = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("无法创建 WScript.Shell，无法发送关闭快捷键。");
        try
        {
            type.InvokeMember("SendKeys", BindingFlags.InvokeMethod, null, shell, [keys]);
        }
        finally
        {
            if (Marshal.IsComObject(shell))
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }

    private static IntPtr TryGetWindowHandle(object window)
    {
        foreach (var property in new[] { "WindowHandle32", "WindowHandle" })
        {
            try
            {
                var value = ComInvocation.Get(window, property);
                var handle = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (handle != 0)
                {
                    return new IntPtr(handle);
                }
            }
            catch
            {
            }
        }

        return IntPtr.Zero;
    }

    private static void CloseDocumentWithAlertResponse(object application, object targetDocument, int alertResponse, string failurePrefix)
    {
        var originalAlertResponse = 0;
        var hasOriginalAlertResponse = false;
        try
        {
            originalAlertResponse = Convert.ToInt32(ComInvocation.Get(application, "AlertResponse"));
            hasOriginalAlertResponse = true;
        }
        catch
        {
            hasOriginalAlertResponse = false;
        }

        try
        {
            ComInvocation.Set(application, "AlertResponse", alertResponse);
            ComInvocation.CallVoid(targetDocument, "Close");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(failurePrefix + ex.Message, ex);
        }
        finally
        {
            if (hasOriginalAlertResponse)
            {
                try { ComInvocation.Set(application, "AlertResponse", originalAlertResponse); }
                catch { }
            }
        }
    }

    private bool ContainsDocumentKey(string key)
    {
        try
        {
            var documents = Get(_application, "Documents");
            var count = Convert.ToInt32(Get(documents, "Count"));
            for (var index = 1; index <= count; index++)
            {
                var document = Call(documents, "Item", index);
                if (string.Equals(DocumentKey(document), key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private object? TryGetActiveDocument()
    {
        try { return Get(_application, "ActiveDocument"); }
        catch { return null; }
    }

    private static bool HasEditablePage(object document)
    {
        try
        {
            var pages = Get(document, "Pages");
            return Convert.ToInt32(Get(pages, "Count")) > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDocumentReady(object document)
    {
        try
        {
            var pages = Get(document, "Pages");
            var page = Call(pages, "Item", 1);
            var shapes = Get(page, "Shapes");
            return Convert.ToInt32(Get(pages, "Count")) > 0 &&
                Convert.ToInt32(Get(shapes, "Count")) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool EnsureTargetVisioDocumentForeground(object application, object targetDocument, Action<int> wait)
    {
        var window = TryGetApplicationWindowHandle(application);
        if (window == IntPtr.Zero)
        {
            return false;
        }

        ShowWindow(window, 9);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try { ComInvocation.CallVoid(targetDocument, "Activate"); }
            catch { }

            SetForegroundWindow(window);
            wait(100);
            if (IsForegroundVisioWindow() && IsActiveDocument(application, targetDocument))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActiveDocument(object application, object targetDocument)
    {
        try
        {
            var activeDocument = ComInvocation.Get(application, "ActiveDocument");
            return string.Equals(DocumentKey(activeDocument), DocumentKey(targetDocument), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr TryGetApplicationWindowHandle(object application)
    {
        foreach (var property in new[] { "WindowHandle32", "WindowHandle" })
        {
            try
            {
                var value = ComInvocation.Get(application, property);
                var handle = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (handle != 0)
                {
                    return new IntPtr(handle);
                }
            }
            catch
            {
            }
        }

        try
        {
            var processes = Process.GetProcessesByName("VISIO")
                .Where(process => process.MainWindowHandle != IntPtr.Zero)
                .ToList();
            return processes.Count == 1 ? processes[0].MainWindowHandle : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool IsForegroundVisioWindow()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(foreground, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            return Process.GetProcessById((int)processId).ProcessName.Equals("VISIO", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string SafeFullName(object document)
    {
        try { return Convert.ToString(Get(document, "FullName")) ?? ""; }
        catch { return ""; }
    }

    private static double CellResultIU(object shape, string cellName)
    {
        var cell = Call(shape, "CellsU", cellName);
        return Convert.ToDouble(Get(cell, "ResultIU"));
    }

    private static object Get(object target, string name) => ComInvocation.Get(target, name);

    private static void Set(object target, string name, object value) => ComInvocation.Set(target, name, value);

    private static object Call(object target, string name, params object[] args) => ComInvocation.Call(target, name, args);

    private static void CallVoid(object target, string name, params object[] args) => ComInvocation.CallVoid(target, name, args);

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout, string failureMessage)
    {
        var end = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < end)
        {
            try
            {
                if (condition()) return;
            }
            catch (Exception ex)
            {
                last = ex;
            }
            Thread.Sleep(200);
        }

        throw new TimeoutException(last is null ? failureMessage : failureMessage + " " + last.Message);
    }

    private static bool TryGetActiveObject(string progId, out object? active)
    {
        active = null;
        var clsidResult = CLSIDFromProgID(progId, out var clsid);
        if (clsidResult != 0)
        {
            return false;
        }

        var result = GetActiveObject(ref clsid, IntPtr.Zero, out active);
        return result == 0 && active is not null;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = true)]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
