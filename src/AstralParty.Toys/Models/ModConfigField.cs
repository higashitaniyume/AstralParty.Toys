namespace AstralParty.Toys.Services;

/// <summary>mod 配置表单字段（configs\{mod}.json 键值编辑）。</summary>
public sealed class ModConfigField
{
    public string Name { get; set; } = "";
    /// <summary>bool / number / string / key(键位绑定) / other（other 原样保留原始 JSON 文本）。</summary>
    public string Kind { get; set; } = "string";
    /// <summary>当前界面语言下 config.json 的 <c>_comments</c> 说明；没有说明时为空串。</summary>
    public string Comment { get; set; } = "";
    public bool BoolValue { get; set; }
    public double NumberValue { get; set; }
    public string StringValue { get; set; } = "";
}
