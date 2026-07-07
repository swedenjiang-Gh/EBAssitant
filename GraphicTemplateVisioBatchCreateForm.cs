namespace EBAssistant;

public sealed class GraphicTemplateVisioBatchCreateForm : Form
{
    private readonly EbAdapterClient _client;
    private readonly GraphicTemplateDirectoryNode _directory;
    private readonly TextBox _sourcePath = new() { Dock = DockStyle.Fill };
    private readonly TextBox _requestedCount = new() { Dock = DockStyle.Left, Width = 100 };
    private readonly Button _browseSource = new() { Text = "浏览", Dock = DockStyle.Right, Width = 80 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly Button _run = new() { Text = "开始", Dock = DockStyle.Right, Width = 100 };

    private bool _isRunning;

    public GraphicTemplateVisioBatchCreateForm(EbAdapterClient client, GraphicTemplateDirectoryNode directory)
    {
        _client = client;
        _directory = directory;

        Text = "Visio批量创建图形模板";
        Width = 760;
        Height = 260;
        StartPosition = FormStartPosition.CenterParent;

        _requestedCount.Text = GraphicTemplateVisioBatchPlanner.DefaultRequestedCount.ToString();
        _browseSource.Click += (_, _) => BrowseSourceFile();
        _run.Click += async (_, _) => await RunAsync();
        FormClosing += OnFormClosing;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var sourcePanel = new Panel { Dock = DockStyle.Fill };
        sourcePanel.Controls.Add(_sourcePath);
        sourcePanel.Controls.Add(_browseSource);

        layout.Controls.Add(new Label { Text = "目标目录", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(new Label { Text = _directory.FullPath, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
        layout.Controls.Add(new Label { Text = "源 Visio 文件", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        layout.Controls.Add(sourcePanel, 1, 1);
        layout.Controls.Add(new Label { Text = "创建数量", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        layout.Controls.Add(_requestedCount, 1, 2);
        layout.Controls.Add(_status, 1, 3);
        layout.Controls.Add(_run, 1, 4);
        Controls.Add(layout);
    }

    private void BrowseSourceFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Visio 文件 (*.vsd;*.vsdx;*.vsdm)|*.vsd;*.vsdx;*.vsdm",
            Title = "选择源 Visio 文件"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _sourcePath.Text = dialog.FileName;
        }
    }

    private async Task RunAsync()
    {
        _run.Enabled = false;
        _browseSource.Enabled = false;

        var countText = _requestedCount.Text.Trim();
        if (!GraphicTemplateVisioBatchPlanner.TryParseRequestedCount(countText, out var requestedCount, out var countValidation))
        {
            _run.Enabled = true;
            _browseSource.Enabled = true;
            MessageBox.Show(this, countValidation, "Visio批量创建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var sourcePath = _sourcePath.Text.Trim();
        var sourcePathValidation = GraphicTemplateVisioBatchPlanner.ValidateSourceVisioPath(sourcePath);
        if (!sourcePathValidation.IsValid)
        {
            _run.Enabled = true;
            _browseSource.Enabled = true;
            MessageBox.Show(this, sourcePathValidation.Message, "Visio批量创建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var batch = new GraphicTemplateVisioBatchResult
        {
            SourceVisioPath = sourcePath,
            TargetDirectoryPath = _directory.FullPath,
            RequestedCount = requestedCount
        };
        VisioAutomation? visio = null;
        object? sourceDocument = null;
        _isRunning = true;

        try
        {
            SetStatus("正在预检目标目录...");
            var validation = GraphicTemplateVisioBatchPlanner.ValidateTargetDirectory(_directory, requestedCount);
            if (!validation.IsValid) throw new InvalidOperationException(validation.Message);

            var baseTemplate = _directory.Templates[0];
            batch.BaseTemplateId = baseTemplate.Id;
            batch.BaseTemplateName = baseTemplate.Name;

            SetStatus("正在打开源 Visio 文件...");
            visio = VisioAutomation.OpenOrAttach();
            sourceDocument = visio.OpenSourceDocument(batch.SourceVisioPath);
            var sortedShapes = GraphicTemplateVisioBatchPlanner.SortGroupShapes(visio.ReadGroupShapes(sourceDocument), rowTolerance: 0.1);
            var sourceValidation = GraphicTemplateVisioBatchPlanner.ValidateSourceShapes(sortedShapes, requestedCount);
            if (!sourceValidation.IsValid) throw new InvalidOperationException(sourceValidation.Message);
            SetStatus("源 Visio 文件已就绪。");

            var names = GraphicTemplateVisioBatchPlanner.GenerateTemplateNames(requestedCount);
            var steps = GraphicTemplateVisioBatchPlanner.BuildSteps(names, sortedShapes.Take(requestedCount).ToList());

            foreach (var step in steps)
            {
                var record = new GraphicTemplateVisioBatchRecord
                {
                    TemplateName = step.TemplateName,
                    SourceShapeName = step.SourceShapeName,
                    SourceShapeIndex = step.SourceShapeIndex
                };
                batch.Records.Add(record);

                try
                {
                    var health = await _client.GetGraphicTemplateIdentityAsync();
                    if (!health.Success || health.Data is null)
                    {
                        throw new InvalidOperationException("EB 连接检查失败，已停止继续创建：" + health.Message);
                    }

                    SetStatus("正在创建模板 " + step.TemplateName + "...");
                    var creation = await _client.CreateNamedGraphicTemplatesAsync(new CreateNamedGraphicTemplatesRequest
                    {
                        DirectoryId = _directory.Id,
                        SourceTemplateId = baseTemplate.Id,
                        TargetNames = [step.TemplateName]
                    });
                    var created = creation.Data?.Records.FirstOrDefault(item => item.Status == "created");
                    if (creation.Data is null || created is null)
                    {
                        throw new InvalidOperationException(creation.Message);
                    }

                    record.TemplateId = created.ConfirmedTemplateId;
                    SetStatus("模板 " + step.TemplateName + " 已创建，正在确认 EB 响应...");
                    await WaitForEbResponsiveAsync("模板 " + step.TemplateName + " 创建后 EB 未恢复响应。");

                    SetStatus("正在打开模板 " + step.TemplateName + "...");
                    var beforeOpen = visio.CaptureDocumentSnapshot();
                    var open = await _client.OpenGraphicTemplateWithVisioAsync(new OpenGraphicTemplateWithVisioRequest { TemplateId = record.TemplateId });
                    if (open.Data is null || open.Data.Status != "opened")
                    {
                        throw new InvalidOperationException(open.Message);
                    }

                    var targetDocument = visio.WaitForOpenedTargetDocument(step.TemplateName, sourceDocument, beforeOpen);
                    SetStatus("模板 " + step.TemplateName + " 已打开。");
                    SetStatus("正在复制并粘贴源组合图形 " + step.SourceShapeIndex + "...");
                    VisioAutomation.EnsureSourceShapeReadyForCopy(sourceDocument, batch.SourceVisioPath, step.SourceShapeIndex);
                    VisioAutomation.EnsureTargetDocumentReadyForPaste(targetDocument, sourceDocument, step.TemplateName, beforeOpen);
                    visio.CopyGroupShapeFromSource(sourceDocument, step.SourceShapeIndex);
                    visio.PasteIntoTarget(targetDocument);
                    SetStatus("正在保存模板 " + step.TemplateName + "...");
                    VisioAutomation.EnsureTargetDocumentReadyForPaste(targetDocument, sourceDocument, step.TemplateName, beforeOpen);
                    visio.SaveAndCloseDocument(targetDocument);

                    record.Status = "saved";
                    record.Message = "已打开、粘贴，并通过关闭提示触发保存当前图形模板。";
                    SetStatus("模板 " + step.TemplateName + " 已关闭保存，正在确认 EB 响应...");
                    await WaitForEbResponsiveAsync("模板 " + step.TemplateName + " 关闭保存后 EB 未恢复响应。");
                }
                catch (Exception ex)
                {
                    record.Status = "failed";
                    record.Message = ex.Message;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            batch.Records.Add(new GraphicTemplateVisioBatchRecord
            {
                Status = "failed",
                Message = ex.Message
            });
        }
        finally
        {
            string logDirectory;
            try { logDirectory = GraphicTemplateVisioBatchLogWriter.Write(batch); }
            catch { logDirectory = ""; }

            _isRunning = false;
            _run.Enabled = true;
            _browseSource.Enabled = true;
            var completionText = BuildCompletionStatusText(batch, logDirectory);
            SetStatus(completionText);
            MessageBox.Show(this, completionText, "Visio批量创建", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public static string BuildCompletionStatusText(GraphicTemplateVisioBatchResult batch, string logDirectory)
    {
        var savedCount = batch.Records.Count(record => record.Status == "saved");
        var failedCount = batch.Records.Count(record => record.Status == "failed");
        var prefix = batch.Status switch
        {
            "completed" => "完成",
            "partial" => "已停止，部分完成",
            _ => "失败"
        };

        return prefix +
            "，状态：" + batch.Status +
            "，请求 " + batch.RequestedCount +
            "，已处理 " + batch.Records.Count +
            "，保存成功 " + savedCount +
            "，失败 " + failedCount +
            (string.IsNullOrEmpty(logDirectory) ? "" : "；日志：" + logDirectory);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isRunning)
        {
            return;
        }

        e.Cancel = true;
        SetStatus("正在运行 Visio 批量创建，请等待当前步骤完成。");
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        _status.Refresh();
    }

    private async Task WaitForEbResponsiveAsync(string failureMessage)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        string lastMessage = "";
        while (DateTime.UtcNow < end)
        {
            var response = await _client.GetGraphicTemplateIdentityAsync();
            if (response.Success && response.Data is not null)
            {
                return;
            }

            lastMessage = response.Message;
            await Task.Delay(500);
        }

        throw new TimeoutException(string.IsNullOrWhiteSpace(lastMessage) ? failureMessage : failureMessage + " " + lastMessage);
    }
}
