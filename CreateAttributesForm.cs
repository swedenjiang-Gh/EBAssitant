namespace EBAssistant;

public sealed class CreateAttributesForm : Form
{
    private readonly EbAdapterClient _client;
    private readonly AttributeFolderNode _folder;
    private readonly IEnumerable<string> _existingNames;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AllowUserToAddRows = false };
    private readonly Button _import = new() { Text = "导入表格", Width = 120, Height = 38 };
    private readonly Button _mapping = new() { Text = "类型映射配置", Width = 140, Height = 38 };
    private readonly Button _confirm = new() { Text = "确定", Width = 120, Height = 38, Enabled = false };
    private readonly Label _summary = new() { AutoSize = true, Text = "请导入 Excel 表格。" };
    private List<ImportedAttributeRow> _rows = [];
    private string? _importedPath;

    public bool CreatedSuccessfully { get; private set; }

    public CreateAttributesForm(EbAdapterClient client, AttributeFolderNode folder, IEnumerable<string> existingNames)
    {
        _client = client;
        _folder = folder;
        _existingNames = existingNames;
        Text = "创建属性";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 620);
        Font = new Font("Microsoft YaHei UI", 10F);

        var target = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 14, 10, 0), Text = $"目标目录：{folder.FullPath}" };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        buttons.Controls.Add(_confirm);
        buttons.Controls.Add(_mapping);
        buttons.Controls.Add(_import);
        buttons.Controls.Add(_summary);
        Controls.Add(_grid);
        Controls.Add(target);
        Controls.Add(buttons);
        _import.Click += (_, _) => ImportExcel();
        _mapping.Click += (_, _) => OpenMappingConfiguration();
        _confirm.Click += async (_, _) => await CreateAsync();
    }

    private void ImportExcel()
    {
        using var dialog = new OpenFileDialog { Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls", Title = "导入属性表格" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _importedPath = dialog.FileName;
            _rows = ExcelAttributeImporter.Read(dialog.FileName, _existingNames);
            _grid.DataSource = _rows;
            var errors = _rows.Count(x => !x.IsValid);
            _summary.Text = $"共 {_rows.Count} 行，有效 {_rows.Count - errors} 行，错误 {errors} 行";
            _confirm.Enabled = _rows.Count > 0 && errors == 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"读取 Excel 失败：{ex.Message}", "导入表格", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenMappingConfiguration()
    {
        using var dialog = new AttributeTypeMappingForm();
        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(_importedPath))
        {
            try
            {
                _rows = ExcelAttributeImporter.Read(_importedPath, _existingNames);
                _grid.DataSource = null;
                _grid.DataSource = _rows;
                var errors = _rows.Count(x => !x.IsValid);
                _summary.Text = $"共 {_rows.Count} 行，有效 {_rows.Count - errors} 行，错误 {errors} 行";
                _confirm.Enabled = _rows.Count > 0 && errors == 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"重新校验 Excel 失败：{ex.Message}", "类型映射配置", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task CreateAsync()
    {
        if (_rows.Count == 0 || _rows.Any(x => !x.IsValid)) return;
        _import.Enabled = _confirm.Enabled = false;
        UseWaitCursor = true;
        var request = new CreateAttributesRequest
        {
            TargetFolderId = _folder.Id,
            Attributes = _rows.Select(x => new CreateAttributeItem { RowNumber = x.RowNumber, Name = x.Name.Trim(), Type = x.EbType, Digits = 0 }).ToList()
        };
        var response = await _client.CreateAttributesAsync(request);
        UseWaitCursor = false;
        _import.Enabled = true;
        _confirm.Enabled = true;
        if (!response.Success || response.Data is null)
        {
            MessageBox.Show(this, response.Message, "创建属性失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        MessageBox.Show(this, $"已在“{_folder.FullPath}”中创建 {response.Data.CreatedCount} 个属性。", "创建属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
        CreatedSuccessfully = true;
        DialogResult = DialogResult.OK;
        Close();
    }
}
