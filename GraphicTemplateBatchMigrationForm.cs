namespace EBAssistant;

public sealed class GraphicTemplateBatchMigrationForm : Form
{
    private readonly GraphicTemplateTreeResult _cache;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = true,
        AllowUserToAddRows = false
    };
    private readonly Button _import = new() { Text = "导入表格", Width = 120, Height = 36 };
    private readonly Button _confirm = new() { Text = "确定", Width = 100, Height = 36, Enabled = false };
    private readonly Label _summary = new() { AutoSize = true, Text = "请导入 Excel 表格。" };
    private bool _hasPreview;

    public GraphicTemplateBatchMigrationForm(GraphicTemplateTreeResult cache)
    {
        _cache = cache;
        Text = "按表格迁移";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1180, 680);
        Font = new Font("Microsoft YaHei UI", 10F);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(_confirm);
        buttons.Controls.Add(_import);
        buttons.Controls.Add(_summary);
        Controls.Add(_grid);
        Controls.Add(buttons);

        _import.Click += (_, _) => Import();
        _confirm.Click += (_, _) => MessageBox.Show(
            this,
            "按表格迁移功能暂未开发。",
            "按表格迁移",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void Import()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls",
            Title = "导入模板图形迁移配置"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            SetBusy(true);
            var preview = GraphicTemplateBatchMigration.BuildPreview(
                GraphicTemplateBatchExcelImporter.Read(dialog.FileName),
                _cache);
            _grid.DataSource = preview;
            _summary.Text = $"共 {preview.Count} 行，有效 {preview.Count(row => row.IsValid)} 行，无效 {preview.Count(row => !row.IsValid)} 行。";
            _hasPreview = true;
        }
        catch (Exception ex)
        {
            _grid.DataSource = null;
            _summary.Text = "导入失败。";
            _hasPreview = false;
            MessageBox.Show(this, $"导入失败：{ex.Message}", "按表格迁移", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _import.Enabled = !busy;
        _confirm.Enabled = !busy && _hasPreview;
        UseWaitCursor = busy;
    }
}
