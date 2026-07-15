/// <summary>
/// Type-safe enum replacing magic strings in PlayerInputReader.IsFeatureEnabled().
/// The compiler will catch typos at build time instead of silently failing at runtime.
/// </summary>
public enum PlayerInputFeature
{
    Movement,
    Jump,
    Attack,
    PowerShot,
    Punch,
    Kick,
    Inventory,
    Pause,
    Settings
}
