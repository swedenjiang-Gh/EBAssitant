namespace EBAssistant;

public sealed class AttributeTypeMappingForm : Form
{
    private readonly BindingSource _source = new();
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = true };

    public AttributeTypeMappingForm()
    {
        Text = "属性类型映射配置";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(700, 560);
        Font = new Font("Microsoft YaHei UI", 10F);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AttributeTypeMapping.UserType), HeaderText = "用户在表格中填写的类型", Width = 300 });
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(AttributeTypeMapping.EbType),
            HeaderText = "对应的正确属性类型",
            Width = 260,
            DataSource = new[] { "String", "Date", "Time", "DateTime", "Boolean", "Number", "Float", "Formula" }
        });
        _source.DataSource = AttributeTypeMappingStore.Load();
        _grid.DataSource = _source;

        var note = new Label { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8), Text = "只要 Excel 中填写的类型能在此表找到映射，就会按右侧正确类型创建属性。可新增、修改或删除映射。" };
        var save = new Button { Text = "保存", Dock = DockStyle.Bottom, Height = 42 };
        save.Click += (_, _) => SaveMappings();
        Controls.Add(_grid);
        Controls.Add(note);
        Controls.Add(save);
    }

    private void SaveMappings()
    {
        _grid.EndEdit();
        _source.EndEdit();
        var mappings = _source.List.Cast<AttributeTypeMapping>().ToList();
        var duplicate = mappings.Where(x => !string.IsNullOrWhiteSpace(x.UserType))
            .GroupBy(x => x.UserType.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
        {
            MessageBox.Show(this, $"用户类型“{duplicate.Key}”存在重复映射。", "类型映射配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AttributeTypeMappingStore.Save(mappings);
        DialogResult = DialogResult.OK;
        Close();
    }
}
