using StardewModdingAPI;

namespace Tocseoj.Stardew.StardewShuffle;
internal abstract class ModComponent(IMonitor monitor, IManifest modManifest, IModHelper helper, ModConfig config)
{
    internal IMonitor Monitor { get; } = monitor;
    internal IManifest ModManifest { get; } = modManifest;
    internal IModHelper Helper { get; } = helper;
    internal ModConfig Config { get; set; } = config;
}