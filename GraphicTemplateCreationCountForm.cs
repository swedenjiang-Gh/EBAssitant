namespace EBAssistant;

public sealed class GraphicTemplateCreationCountForm : Form
{
    private readonly NumericUpDown _count = new()
    {
        Minimum = 2,
        Maximum = 200,
        Value = 2,
        Width = 160,
        TextAlign = HorizontalAlignment.Right
    };

    public GraphicTemplateCreationCountForm()
    {
        Text = "新建模板图形";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(430, 190);
        Font = new Font("Microsoft YaHei UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(12, 14, 12, 0),
            Text = "请输入最终模板图形总数量（包含当前已有的模板图形）："
        };
        var input = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(12, 4, 12, 4) };
        input.Controls.Add(_count);

        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 100 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 100 };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(input);
        Controls.Add(label);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public int TotalCount => decimal.ToInt32(_count.Value);
}
