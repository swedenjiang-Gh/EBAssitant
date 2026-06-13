namespace EBAssistant;

public sealed class PasswordPromptForm : Form
{
    private readonly TextBox _passwordBox = new() { UseSystemPasswordChar = true, Width = 260 };

    public PasswordPromptForm()
    {
        Text = "授权密码";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(360, 140);
        Font = new Font("Microsoft YaHei UI", 10F);

        var label = new Label
        {
            Text = "请输入授权管理器密码：",
            AutoSize = true,
            Location = new Point(18, 18)
        };
        _passwordBox.Location = new Point(20, 48);

        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(174, 92), Width = 75 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(260, 92), Width = 75 };

        Controls.Add(label);
        Controls.Add(_passwordBox);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string Password => _passwordBox.Text;
}
