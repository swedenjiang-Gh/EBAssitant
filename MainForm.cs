namespace EBAssistant;

public sealed class MainForm : Form
{
    private readonly Label _statusLabel;
    private readonly List<Form> _openWindows = [];

    public MainForm()
    {
        Text = "EB Assist";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 480);
        Size = new Size(900, 560);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Microsoft YaHei UI", 10F);

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "EB Assist",
            Font = new Font("Microsoft YaHei UI", 24F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 43, 59),
            Margin = new Padding(0, 0, 0, 6)
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            Text = "请选择需要使用的配置功能",
            ForeColor = Color.FromArgb(100, 112, 128),
            Margin = new Padding(0, 0, 0, 24)
        };

        var buttonGrid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        string[] functions =
        [
            "属性",
            "类型定义",
            "工作表",
            "权限配置",
            "图形模板",
            "工具面板配置"
        ];

        foreach (var functionName in functions)
        {
            buttonGrid.Controls.Add(CreateFunctionButton(functionName));
        }

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "就绪",
            ForeColor = Color.FromArgb(100, 112, 128),
            Margin = new Padding(0, 24, 0, 0)
        };

        var content = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(54, 42, 54, 42)
        };
        content.Controls.Add(titleLabel);
        content.Controls.Add(descriptionLabel);
        content.Controls.Add(buttonGrid);
        content.Controls.Add(_statusLabel);

        Controls.Add(content);
    }

    private Button CreateFunctionButton(string functionName)
    {
        var button = new Button
        {
            Text = functionName,
            Size = new Size(330, 88),
            Margin = new Padding(0, 0, 18, 18),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(32, 43, 59),
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(218, 224, 232);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 242, 255);
        button.Click += (_, _) => OpenFunction(functionName);
        return button;
    }

    private void OpenFunction(string functionName)
    {
        _statusLabel.Text = $"已选择：{functionName}";
        if (functionName == "属性")
        {
            var form = new AttributeFoldersForm();
            _openWindows.Add(form);
            form.FormClosed += (_, _) => _openWindows.Remove(form);
            form.Show();
            return;
        }
        if (functionName == "类型定义")
        {
            var form = new TypeDefinitionsForm();
            _openWindows.Add(form);
            form.FormClosed += (_, _) => _openWindows.Remove(form);
            form.Show();
            return;
        }

        MessageBox.Show(
            $"{functionName}功能界面将在后续开发中实现。",
            functionName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
