namespace AstralParty.Toys.Services;

/// <summary>mod 权限位（与 SDK 的 ModPermission 枚举一致）。</summary>
[Flags]
public enum ModPermission
{
    None = 0,
    ReadGameState = 1 << 0,
    GameActions = 1 << 1,
    SpeedHack = 1 << 2,
    FileWrite = 1 << 3,
}
