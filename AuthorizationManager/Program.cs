namespace EBAssistant;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var prompt = new PasswordPromptForm();
        if (prompt.ShowDialog() != DialogResult.OK) return;
        if (!AuthorizationCrypto.VerifyManagerPassword(prompt.Password))
        {
            MessageBox.Show("密码错误。", "授权管理器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Application.Run(new AuthorizationManagerForm());
    }
}
