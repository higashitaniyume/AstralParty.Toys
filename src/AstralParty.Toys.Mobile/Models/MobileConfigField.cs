namespace AstralParty.Toys.Mobile.Models;

/// <summary>设备上模组配置的一个可编辑字段（只覆盖标量；对象/数组归为 <c>other</c>，只读）。</summary>
public sealed class MobileConfigField
{
    public string Name { get; set; } = "";

    /// <summary>bool / number / string / other。</summary>
    public string Kind { get; set; } = "string";

    /// <summary>按界面语言取的 <c>_comments</c> 说明，没有就是空串。</summary>
    public string Comment { get; set; } = "";

    public bool BoolValue { get; set; }

    public double NumberValue { get; set; }

    public string StringValue { get; set; } = "";
}
