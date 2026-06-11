namespace EBAssistant;

public sealed class TypeDefinitionDialogForm : Form
{
    private readonly EbAdapterClient _client;
    private readonly List<TypeDefinitionNode> _targets;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AllowUserToAddRows = false };
    private readonly Button _import = new() { Text = "导入表格", Width = 120, Height = 38 };
    private readonly Button _confirm = new() { Text = "确定", Width = 120, Height = 38, Enabled = false };
    private readonly Label _summary = new() { AutoSize = true, Text = "请导入 Excel 表格。" };
    private List<DialogDefinitionRow> _rows = [];

    public TypeDefinitionDialogForm(EbAdapterClient client, List<TypeDefinitionNode> targets)
    {
        _client = client;
        _targets = targets;
        Text = "定义对话框";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(920, 650);
        Font = new Font("Microsoft YaHei UI", 10F);
        var target = new Label { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 14, 10, 0), Text = $"将操作 {_targets.Count} 个叶级类型对象。" };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_confirm);
        buttons.Controls.Add(_import);
        buttons.Controls.Add(_summary);
        Controls.Add(_grid);
        Controls.Add(target);
        Controls.Add(buttons);
        _import.Click += async (_, _) => await ImportAsync();
        _confirm.Click += async (_, _) => await ApplyAsync();
    }

    private async Task ImportAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls", Title = "导入定义对话框配置" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            SetBusy(true);
            _rows = DialogDefinitionExcelImporter.Read(dialog.FileName);
            var validIds = _rows.Where(x => x.AttributeId > 0).Select(x => x.AttributeId).Distinct().ToList();
            var response = await _client.ValidateAttributeIdsAsync(validIds);
            if (!response.Success || response.Data is null) throw new InvalidOperationException(response.Message);
            var missing = response.Data.MissingIds.ToHashSet();
            foreach (var row in _rows)
            {
                if (row.Validation == "待校验") row.Validation = missing.Contains(row.AttributeId) ? "EB 中不存在该属性 ID" : "有效";
                else if (row.Validation == "重复行，已合并" && missing.Contains(row.AttributeId)) row.Validation = "EB 中不存在该属性 ID";
            }
            _grid.DataSource = null;
            _grid.DataSource = _rows;
            var errors = _rows.Count(x => !x.IsValid);
            _summary.Text = $"共 {_rows.Count} 行，有效配置 {_rows.Count(x => x.Validation == "有效")}，合并重复 {_rows.Count(x => x.IsDuplicate)}，错误 {errors}";
            _confirm.Enabled = _rows.Count > 0 && errors == 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导入或校验失败：{ex.Message}", "定义对话框", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    private async Task ApplyAsync()
    {
        if (_rows.Count == 0 || _rows.Any(x => !x.IsValid)) return;
        SetBusy(true);
        var definitions = _rows.Where(x => !x.IsDuplicate)
            .GroupBy(x => $"{x.TabName.ToUpperInvariant()}\u001f{x.AttributeId}")
            .Select(x => x.First())
            .Select(x => new DialogDefinitionItem { TabName = x.TabName.Trim(), AttributeId = x.AttributeId }).ToList();
        var response = await _client.ApplyTypeDefinitionDialogsAsync(new ApplyTypeDefinitionDialogsRequest
        {
            TypeItemIds = _targets.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Definitions = definitions
        });
        SetBusy(false);
        var result = response.Data ?? new ApplyTypeDefinitionDialogsResult
        {
            Status = "failed",
            Records = [new TypeDefinitionOperationRecord { Status = "failed", Message = response.Message }]
        };
        var logDirectory = TypeDefinitionLogWriter.Write(result);
        var resultForm = new TypeDefinitionResultForm(result, logDirectory);
        resultForm.Show();
        if (response.Success) Close();
    }

    private void SetBusy(bool busy)
    {
        _import.Enabled = !busy;
        _confirm.Enabled = !busy && _rows.Count > 0 && _rows.All(x => x.IsValid);
        UseWaitCursor = busy;
    }
}
