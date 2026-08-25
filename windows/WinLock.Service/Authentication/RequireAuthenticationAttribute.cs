namespace WinLock.Service.Authentication;

/// <summary>Marks a controller or action as requiring a valid authentication.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAuthenticationAttribute : Attribute;