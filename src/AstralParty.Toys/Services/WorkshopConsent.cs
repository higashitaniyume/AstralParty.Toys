namespace AstralParty.Toys.Services;

/// <summary>
/// 创意工坊的使用同意记录：没有同意之前宿主不会发起任何工坊网络请求。
/// 文案改动（免责说明实质变化）时提高 <see cref="CurrentVersion"/> 会重新询问一次。
/// </summary>
public static class WorkshopConsent
{
    /// <summary>当前免责说明的版本。</summary>
    public const int CurrentVersion = 1;

    public static bool IsGranted(ReplayLibrarySettings settings)
        => settings.WorkshopConsentVersion >= CurrentVersion;
}
