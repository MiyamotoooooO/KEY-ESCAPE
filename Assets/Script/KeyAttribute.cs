/// <summary>
/// Spec Ver.0.2 §9: Chapter 1 uses only Movement and Attack. Defense/Dodge/etc.
/// are added later, once the corresponding action itself is unlocked
/// (Ver.0.2 §24 - carried over to Chapter 2+).
/// </summary>
public enum KeyAttribute
{
    None,
    Movement,
    Attack,
}
