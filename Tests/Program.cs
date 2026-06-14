namespace EBAssistant.Tests;

internal static class Program
{
    private static int Main()
    {
        try
        {
            WorksheetNameResolverTests.Run();
            WorksheetColumnWidthCalculatorTests.Run();
            WorksheetImportBuilderTests.Run();
            ProjectTemplateCacheTests.Run();
            GraphicTemplateCacheTests.Run();
            GraphicTemplateSelectionTests.Run();
            GraphicTemplateMigrationResultTests.Run();
            WorksheetPreparationTests.Run();
            PermissionAssignmentSelectionTests.Run();
            PermissionAssignmentResultTests.Run();
            Console.WriteLine("PASS: 10 test groups");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

public static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', but got '{actual}'.");
        }
    }

    public static void True(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Expected condition to be true.");
        }
    }

    public static void Null(object? value)
    {
        if (value is not null)
        {
            throw new InvalidOperationException($"Expected null, but got '{value}'.");
        }
    }

    public static void NotNull(object? value)
    {
        if (value is null)
        {
            throw new InvalidOperationException("Expected a non-null value.");
        }
    }
}
