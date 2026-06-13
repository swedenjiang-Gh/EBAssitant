using System.Text;

namespace EBAssistant;

public sealed class AuthorizationManagerForm : Form
{
    private readonly TextBox _computerNameBox = new() { Width = 360 };
    private readonly TextBox _macAddressBox = new() { Width = 360 };
    private readonly DateTimePicker _expiresOnPicker = new() { Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd" };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(100, 112, 128) };

    public AuthorizationManagerForm()
    {
        Text = "授权管理器";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(560, 320);
        Font = new Font("Microsoft YaHei UI", 10F);

        _expiresOnPicker.Value = DateTime.Today.AddYears(1);

        var extract = new Button { Text = "提取机器码", Width = 120, Height = 32 };
        extract.Click += (_, _) => ExtractMachineCode();
        var export = new Button { Text = "导出授权文件", Width = 130, Height = 32 };
        export.Click += (_, _) => ExportLicenseFile();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "计算机名", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_computerNameBox, 1, 0);
        layout.Controls.Add(new Label { Text = "MAC 地址", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_macAddressBox, 1, 1);
        layout.Controls.Add(new Label { Text = "到期日期", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_expiresOnPicker, 1, 2);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill };
        buttons.Controls.Add(extract);
        buttons.Controls.Add(export);
        layout.Controls.Add(buttons, 1, 3);
        layout.Controls.Add(_status, 1, 4);

        Controls.Add(layout);
    }

    private void ExtractMachineCode()
    {
        _computerNameBox.Text = AuthorizationMachineInfo.ComputerName;
        _macAddressBox.Text = AuthorizationMachineInfo.GetPrimaryMacAddress();
        _status.Text = "已提取当前机器码。";
    }

    private void ExportLicenseFile()
    {
        if (string.IsNullOrWhiteSpace(_computerNameBox.Text) || string.IsNullOrWhiteSpace(_macAddressBox.Text))
        {
            MessageBox.Show(this, "请先提取或填写计算机名和 MAC 地址。", "授权管理器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出授权文件",
            Filter = "EBAssistant 授权文件 (*.ealic)|*.ealic",
            DefaultExt = "ealic",
            AddExtension = true,
            FileName = "EBAssistant.ealic"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var licenseText = AuthorizationCrypto.CreateLicenseFileText(
            _computerNameBox.Text,
            _macAddressBox.Text,
            _expiresOnPicker.Value);
        File.WriteAllText(dialog.FileName, licenseText, new UTF8Encoding(false));
        _status.Text = $"已导出授权文件：{dialog.FileName}";
        MessageBox.Show(this, "授权文件已导出。", "授权管理器", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
