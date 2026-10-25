namespace Brooklyneir.Core
{
    /// <summary>
    /// Layer indices used by code. Layer 30 needs no name in the Tag Manager: Unity allows any
    /// index 0..31 to be used numerically. The main camera excludes it, the preview camera
    /// renders only it.
    /// </summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Preview = 30;
    }
}
