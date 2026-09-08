namespace iDock;

// Display branding stays independent of the saved device data.
public static class ProductInfo
{
    public const string DisplayName = "iDock for Windows";
    public static string Version => typeof(ProductInfo).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    public static string VersionLabel => $"{DisplayName} · v{Version}";
}
