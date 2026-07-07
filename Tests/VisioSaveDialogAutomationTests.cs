namespace EBAssistant.Tests;

internal static class VisioSaveDialogAutomationTests
{
    public static void Run()
    {
        RecognizesYesButtonText();
        RecognizesNoAndCancelButtonText();
        RecognizesSaveQuestionText();
    }

    private static void RecognizesYesButtonText()
    {
        Assert.True(VisioSaveDialogAutomation.IsYesButtonText("是"));
        Assert.True(VisioSaveDialogAutomation.IsYesButtonText("是(Y)"));
        Assert.True(VisioSaveDialogAutomation.IsYesButtonText("&Yes"));
        Assert.True(VisioSaveDialogAutomation.IsYesButtonText("Yes"));
        Assert.True(!VisioSaveDialogAutomation.IsYesButtonText("否"));
        Assert.True(!VisioSaveDialogAutomation.IsYesButtonText("No"));
        Assert.True(!VisioSaveDialogAutomation.IsYesButtonText("取消"));
    }

    private static void RecognizesNoAndCancelButtonText()
    {
        Assert.True(VisioSaveDialogAutomation.IsNoButtonText("否(&N)"));
        Assert.True(VisioSaveDialogAutomation.IsNoButtonText("No"));
        Assert.True(VisioSaveDialogAutomation.IsCancelButtonText("取消"));
        Assert.True(VisioSaveDialogAutomation.IsCancelButtonText("Cancel"));
    }

    private static void RecognizesSaveQuestionText()
    {
        Assert.True(VisioSaveDialogAutomation.IsSaveQuestionText("要保存修改吗?"));
        Assert.True(VisioSaveDialogAutomation.IsSaveQuestionText("要保存修改吗？"));
    }
}
