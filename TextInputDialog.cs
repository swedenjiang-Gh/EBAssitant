namespace EBAssist;

public static class TextInputDialog
{
    public static string? Show(IWin32Window owner, string title, string prompt)
    {
        using var form = new Form { Text = title, Size = new Size(500, 190), StartPosition = FormStartPosition.CenterParent, Font = new Font("Microsoft YaHei UI", 10F) };
        var label = new Label { Text = prompt, Dock = DockStyle.Top, Height = 46, Padding = new Padding(10, 14, 10, 0) };
        var text = new TextBox { Dock = DockStyle.Top, Margin = new Padding(10) };
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 38 };
        form.Controls.Add(ok);
        form.Controls.Add(text);
        form.Controls.Add(label);
        form.AcceptButton = ok;
        form.Shown += (_, _) => text.Focus();
        return form.ShowDialog(owner) == DialogResult.OK ? text.Text.Trim() : null;
    }
}
