namespace EBAssistant.Tests;

internal static class VisioAutomationTests
{
    public static void Run()
    {
        OpensSourceDocumentReadOnly();
        ReusesAlreadyOpenSourceDocument();
        RejectsCopyWhenSourcePathDoesNotMatch();
        CloseTemplateDocumentPostsCloseToDocumentWindow();
        CloseTemplateDocumentFallsBackToKeyboardCloseWhenDocumentHandleIsMissing();
        AllowsTargetDocumentWithoutWindowBeforePaste();
        CloseTemplateDocumentUsesKeyboardSavePrompt();
        CloseTemplateDocumentDoesNotSendKeysWhenTargetCannotBeFocused();
    }

    private static void OpensSourceDocumentReadOnly()
    {
        var documents = new FakeVisioDocuments();

        VisioAutomation.OpenSourceDocumentFromDocuments(documents, @"D:\work\source.vsdx");

        Assert.Equal(@"D:\work\source.vsdx", documents.OpenedPath);
        Assert.Equal(138, documents.OpenedFlags);
    }

    private static void ReusesAlreadyOpenSourceDocument()
    {
        var existing = new FakeVisioDocument { FullName = @"D:\work\source.vsdx" };
        var documents = new FakeVisioDocuments { ExistingDocuments = [existing] };

        var actual = VisioAutomation.OpenSourceDocumentFromDocuments(documents, @"D:\work\source.vsdx");

        Assert.True(ReferenceEquals(existing, actual));
        Assert.Equal("", documents.OpenedPath);
        Assert.Equal(0, documents.OpenedFlags);
    }

    private static void RejectsCopyWhenSourcePathDoesNotMatch()
    {
        var source = new FakeVisioDocument
        {
            Name = "other.vsdx",
            FullName = @"D:\work\other.vsdx",
            PageCount = 1,
            ShapeType = 2
        };

        try
        {
            VisioAutomation.EnsureSourceShapeReadyForCopy(source, @"D:\work\source.vsdx", 1);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Expected source shape validation to fail.");
    }

    private static void CloseTemplateDocumentUsesKeyboardSavePrompt()
    {
        var app = new FakeVisioApplication { AlertResponse = 0 };
        var document = new FakeVisioDocument { Application = app };
        var keys = new List<string>();
        var waits = new List<int>();

        VisioAutomation.TriggerKeyboardCloseWithSavePrompt(
            app,
            document,
            keys.Add,
            waits.Add,
            _ => true,
            (_, _, _) => true);

        Assert.Equal(0, app.AlertResponse);
        Assert.True(document.Activated);
        Assert.Equal(0, document.SaveCount);
        Assert.Equal("%{F4}", keys[0]);
        Assert.True(waits.Count >= 1);
    }

    private static void CloseTemplateDocumentPostsCloseToDocumentWindow()
    {
        var app = new FakeVisioApplication { AlertResponse = 0 };
        var document = new FakeVisioDocument { Application = app, WindowHandle32 = 1234 };
        var promptClicked = false;
        var postedHandle = IntPtr.Zero;

        VisioAutomation.CloseDocumentWindowWithSavePrompt(
            document,
            _ =>
            {
                promptClicked = true;
                return true;
            },
            handle =>
            {
                postedHandle = handle;
                return true;
            });

        Assert.Equal(new IntPtr(1234), postedHandle);
        Assert.True(!document.Closed);
        Assert.Equal(0, document.SaveCount);
        Assert.Equal(true, promptClicked);
    }

    private static void CloseTemplateDocumentFallsBackToKeyboardCloseWhenDocumentHandleIsMissing()
    {
        var app = new FakeVisioApplication { AlertResponse = 0 };
        var document = new FakeVisioDocument
        {
            Application = app,
            Name = "01",
            FullName = "",
            Index = 2,
            WindowHandle32 = 0,
            WindowCount = 0
        };
        app.ActiveDocument = document;
        var keys = new List<string>();
        var waits = new List<int>();
        var promptClicked = false;

        VisioAutomation.CloseDocumentWindowWithSavePrompt(
            document,
            _ =>
            {
                promptClicked = true;
                return true;
            },
            _ => throw new InvalidOperationException("Window handle close should not be used."),
            sendKeys: keys.Add,
            wait: waits.Add,
            ensureTargetForeground: (_, _, _) => true);

        Assert.Equal("%{F4}", keys[0]);
        Assert.True(waits.Count >= 1);
        Assert.Equal(true, promptClicked);
    }

    private static void AllowsTargetDocumentWithoutWindowBeforePaste()
    {
        var source = new FakeVisioDocument
        {
            Name = "source.vsdx",
            FullName = @"D:\work\source.vsdx",
            PageCount = 1,
            WindowHandle32 = 100
        };
        var target = new FakeVisioDocument
        {
            Name = "01",
            FullName = "",
            PageCount = 1,
            Index = 2,
            WindowHandle32 = 0
        };
        var beforeOpen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"D:\work\source.vsdx"
        };

        VisioAutomation.EnsureTargetDocumentReadyForPaste(target, source, "01", beforeOpen);
    }

    private static void CloseTemplateDocumentDoesNotSendKeysWhenTargetCannotBeFocused()
    {
        var app = new FakeVisioApplication { AlertResponse = 0 };
        var document = new FakeVisioDocument { Application = app };
        var keys = new List<string>();
        var promptClicked = false;

        try
        {
            VisioAutomation.TriggerKeyboardCloseWithSavePrompt(
                app,
                document,
                keys.Add,
                _ => { },
                _ =>
                {
                    promptClicked = true;
                    return true;
                },
                (_, _, _) => false);
        }
        catch (InvalidOperationException)
        {
        }

        Assert.True(document.Activated);
        Assert.Equal(0, keys.Count);
        Assert.Equal(false, promptClicked);
    }

    public sealed class FakeVisioApplication
    {
        public int AlertResponse { get; set; }
        public List<int> AlertResponsesDuringClose { get; } = [];
        public FakeVisioDocument? ActiveDocument { get; set; }
        public FakeVisioWindow? ActiveWindow { get; set; }
    }

    public sealed class FakeVisioDocuments
    {
        public List<FakeVisioDocument> ExistingDocuments { get; set; } = [];
        public string OpenedPath { get; private set; } = "";
        public int OpenedFlags { get; private set; }
        public int Count => ExistingDocuments.Count;

        public FakeVisioDocument Item(int index)
        {
            return ExistingDocuments[index - 1];
        }

        public FakeVisioDocument OpenEx(string path, int flags)
        {
            OpenedPath = path;
            OpenedFlags = flags;
            return new FakeVisioDocument();
        }
    }

    public sealed class FakeVisioDocument
    {
        public bool Closed { get; private set; }
        public bool Activated { get; private set; }
        public int SaveCount { get; private set; }
        public string Name { get; set; } = "Drawing";
        public string FullName { get; set; } = "";
        public int Index { get; set; } = 1;
        public int PageCount { get; set; } = 1;
        public int ShapeType { get; set; } = 2;
        public int WindowHandle32 { get; set; }
        public int WindowCount { get; set; } = 1;
        public FakeVisioApplication? Application { get; set; }
        public FakeVisioWindows Windows => new(WindowHandle32, WindowCount);
        public FakeVisioPages Pages => new(PageCount, ShapeType);

        public void Activate()
        {
            Activated = true;
        }

        public void Save()
        {
            SaveCount++;
        }

        public void Close()
        {
            if (Application is not null)
            {
                Application.AlertResponsesDuringClose.Add(Application.AlertResponse);
            }

            Closed = true;
        }
    }

    public sealed class FakeVisioWindows
    {
        private readonly int _windowHandle32;
        private readonly int _count;

        public FakeVisioWindows(int windowHandle32, int count)
        {
            _windowHandle32 = windowHandle32;
            _count = count;
        }

        public int Count => _count;

        public FakeVisioWindow Item(int index)
        {
            return new FakeVisioWindow(_windowHandle32);
        }
    }

    public sealed class FakeVisioWindow
    {
        public FakeVisioWindow(int windowHandle32)
        {
            WindowHandle32 = windowHandle32;
        }

        public int WindowHandle32 { get; }
        public int CloseCount { get; private set; }

        public void Close()
        {
            CloseCount++;
        }
    }

    public sealed class FakeVisioPages
    {
        private readonly int _shapeType;

        public FakeVisioPages(int count, int shapeType)
        {
            Count = count;
            _shapeType = shapeType;
        }

        public int Count { get; }

        public FakeVisioPage Item(int index)
        {
            return new FakeVisioPage(_shapeType);
        }
    }

    public sealed class FakeVisioPage
    {
        private readonly int _shapeType;

        public FakeVisioPage(int shapeType)
        {
            _shapeType = shapeType;
        }

        public FakeVisioShapes Shapes => new(_shapeType);
    }

    public sealed class FakeVisioShapes
    {
        private readonly int _shapeType;

        public FakeVisioShapes(int shapeType)
        {
            _shapeType = shapeType;
        }

        public FakeVisioShape Item(int index)
        {
            return new FakeVisioShape(_shapeType);
        }
    }

    public sealed class FakeVisioShape
    {
        public FakeVisioShape(int type)
        {
            Type = type;
        }

        public int Type { get; }
    }
}
