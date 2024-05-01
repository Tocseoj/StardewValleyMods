using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.Inventories;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace Tocseoj.Stardew.BigCropBonus;

internal class GiftingBonus(IMonitor Monitor, IManifest ModManifest, IModHelper Helper, ModConfig Config)
	: ModComponent(Monitor, ModManifest, Helper, Config)
{

}