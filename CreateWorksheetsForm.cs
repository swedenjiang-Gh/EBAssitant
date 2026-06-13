namespace EBAssistant;

public sealed class CreateWorksheetsForm : Form
{
    private readonly EbAdapterClient _client;
    private readonly ProjectTemplateNode _project;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = true,
        AllowUserToAddRows = false
    };
    private readonly Button _import = new() { Text = "导入表格", Width = 120, Height = 38 };
    private readonly Button _confirm = new() { Text = "确定", Width = 120, Height = 38, Enabled = false };
    private readonly Label _target = new() { Dock = DockStyle.Top, Height = 52, Padding = new Padding(10, 14, 10, 0) };
    private readonly Label _summary = new() { AutoSize = true, Text = "请导入 Excel 表格。" };
    private List<WorksheetImportSheet> _sheets = [];
    private string _targetFolderPath = "";

    public CreateWorksheetsForm(EbAdapterClient client, ProjectTemplateNode project)
    {
        _client = client;
        _project = project;
        Text = "新建工作表";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1120, 680);
        Font = new Font("Microsoft YaHei UI", 10F);
        _target.Text = $"目标项目：{project.FullPath}；保存位置：工作表 / 收藏夹";

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(_confirm);
        buttons.Controls.Add(_import);
        buttons.Controls.Add(_summary);
        Controls.Add(_grid);
        Controls.Add(_target);
        Controls.Add(buttons);

        _import.Click += async (_, _) => await ImportAsync();
        _confirm.Click += async (_, _) => await CreateAsync();
    }

    private async Task ImportAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls",
            Title = "导入工作表配置"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            SetBusy(true);
            _sheets = WorksheetImportBuilder.Build(WorksheetExcelImporter.Read(dialog.FileName));
            var attributeIds = _sheets.SelectMany(x => x.Columns)
                .Where(x => x.AttributeId > 0)
                .Select(x => x.AttributeId)
                .Distinct()
                .ToList();
            var contextTask = _client.GetWorksheetCreationContextAsync(_project.Id);
            var validationTask = _client.ValidateWorksheetAttributeIdsAsync(attributeIds);
            await Task.WhenAll(contextTask, validationTask);

            var context = await contextTask;
            var validation = await validationTask;
            if (!context.Success || context.Data is null)
                throw new InvalidOperationException(context.Message);
            if (!validation.Success || validation.Data is null)
                throw new InvalidOperationException(validation.Message);

            _targetFolderPath = context.Data.TargetFolderPath;
            _target.Text = $"目标项目：{context.Data.TemplateProjectPath}；保存位置：{_targetFolderPath}";
            WorksheetPreparation.Apply(_sheets, context.Data.ExistingWorksheetNames, validation.Data.MissingIds);
            DisplayPreview();
        }
        catch (Exception ex)
        {
            _sheets = [];
            _grid.DataSource = null;
            _summary.Text = "导入失败。";
            MessageBox.Show(this, $"导入或校验失败：{ex.Message}", "新建工作表", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void DisplayPreview()
    {
        _grid.DataSource = _sheets
            .SelectMany(sheet => sheet.Columns.Select(column => new WorksheetPreviewRow
            {
                Excel页签 = sheet.SheetIndex,
                原页签名称 = sheet.OriginalName,
                最终工作表名称 = sheet.FinalName,
                Excel列 = column.ExcelColumnNumber,
                列标签预览 = column.Label,
                属性ID = column.AttributeId,
                列宽 = column.Width,
                列校验 = column.Validation,
                工作表校验 = sheet.Validation
            }))
            .ToList();
        var valid = _sheets.Count(x => x.IsValid);
        _summary.Text = $"共 {_sheets.Count} 个页签，可创建 {valid} 个，跳过 {_sheets.Count - valid} 个；列标签不写入 EB。";
    }

    private async Task CreateAsync()
    {
        var validSheets = _sheets.Where(x => x.IsValid).OrderBy(x => x.SheetIndex).ToList();
        if (validSheets.Count == 0) return;

        SetBusy(true);
        var request = new CreateWorksheetsRequest
        {
            TemplateProjectId = _project.Id,
            Worksheets = validSheets.Select(sheet => new CreateWorksheetItem
            {
                SheetIndex = sheet.SheetIndex,
                OriginalName = sheet.OriginalName,
                RequestedName = sheet.OriginalName,
                Columns = sheet.Columns.Select(column => new CreateWorksheetColumnItem
                {
                    Position = column.Position,
                    Label = column.Label,
                    AttributeId = column.AttributeId,
                    Width = column.Width
                }).ToList()
            }).ToList()
        };

        var response = await _client.CreateWorksheetsAsync(request);
        var result = response.Data ?? CreateUnprocessedResult(validSheets, response.Message);
        foreach (var record in result.Records.Where(x => string.IsNullOrWhiteSpace(x.SavePath)))
        {
            record.SavePath = _targetFolderPath;
        }
        result.Records.AddRange(_sheets.Where(x => !x.IsValid).Select(CreateSkippedRecord));
        result.Records = result.Records.OrderBy(x => x.SheetIndex).ToList();
        result.Status = ResolveStatus(result.Records);

        string logDirectory;
        try
        {
            logDirectory = WorksheetCreationLogWriter.Write(result);
        }
        catch (Exception ex)
        {
            logDirectory = "";
            MessageBox.Show(this, $"工作表操作已完成，但保存日志失败：{ex.Message}", "工作表创建结果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        new WorksheetCreationResultForm(result, logDirectory).Show();
        SetBusy(false);
        if (response.Success) Close();
    }

    private WorksheetOperationRecord CreateSkippedRecord(WorksheetImportSheet sheet) => new()
    {
        SheetIndex = sheet.SheetIndex,
        OriginalName = sheet.OriginalName,
        FinalName = sheet.FinalName,
        SavePath = _targetFolderPath,
        ObjectType = "器件",
        ColumnCount = sheet.Columns.Count,
        ColumnSummary = string.Join("；", sheet.Columns.Select(x => $"{x.Label} (AID={x.AttributeId})")),
        LabelStatus = "未写入 EB（EB 显示默认属性名称）",
        AutoWidthStatus = "未处理",
        Status = "validation_skipped",
        Message = sheet.Validation
    };

    private static CreateWorksheetsResult CreateUnprocessedResult(IEnumerable<WorksheetImportSheet> sheets, string message) => new()
    {
        Status = "failed",
        Records = sheets.Select(sheet => new WorksheetOperationRecord
        {
            SheetIndex = sheet.SheetIndex,
            OriginalName = sheet.OriginalName,
            FinalName = sheet.FinalName,
            ObjectType = "器件",
            ColumnCount = sheet.Columns.Count,
            ColumnSummary = string.Join("；", sheet.Columns.Select(x => $"{x.Label} (AID={x.AttributeId})")),
            LabelStatus = "未写入 EB（EB 显示默认属性名称）",
            AutoWidthStatus = "未处理",
            Status = "unprocessed",
            Message = message
        }).ToList()
    };

    private static string ResolveStatus(IEnumerable<WorksheetOperationRecord> records)
    {
        var list = records.ToList();
        if (list.Count > 0 && list.All(x => x.Status == "created")) return "completed";
        if (list.Any(x => x.Status == "created")) return "partial";
        return "failed";
    }

    private void SetBusy(bool busy)
    {
        _import.Enabled = !busy;
        _confirm.Enabled = !busy && _sheets.Any(x => x.IsValid);
        UseWaitCursor = busy;
    }

    private sealed class WorksheetPreviewRow
    {
        public int Excel页签 { get; set; }
        public string 原页签名称 { get; set; } = "";
        public string 最终工作表名称 { get; set; } = "";
        public int Excel列 { get; set; }
        public string 列标签预览 { get; set; } = "";
        public int 属性ID { get; set; }
        public int 列宽 { get; set; }
        public string 列校验 { get; set; } = "";
        public string 工作表校验 { get; set; } = "";
    }
}
