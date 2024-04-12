
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.GiantCrops;
using StardewValley.GameData.Objects;
using StardewValley.Inventories;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace Tocseoj.Stardew.BigCropBonus
{
  public sealed class ModConfig {
		/// <summary>Whether to enable test mode, which makes giant crops always spawn (where valid).</summary>
		public bool TestMode { get; set; } = true;

		/// <summary>The percent increase in value of giant crops.</summary>
		public float PercentIncrease { get; set; } = 0.1f;
	}

	/// <summary>The mod entry point.</summary>
	internal sealed class ModEntry : Mod {
		/*********
		** Fields
		*********/
		/// <summary>The mod configuration.</summary>
		private ModConfig Config = null!; // set in Entry

		/// <summary>Objects that need to be created using AssetRequested.</summary>
		private readonly Dictionary<string, ObjectData> objectsNeedingCreated = new();

		/// <summary>Shipping bins in the world.</summary>
		private readonly List<Inventory> cachedShippingBins = new();

		/*********
		** Public methods
		*********/
		/// <inheritdoc/>
		public override void Entry(IModHelper helper) {
			Config = helper.ReadConfig<ModConfig>();

			helper.Events.GameLoop.DayEnding += OnDayEnding;
			helper.Events.Content.AssetRequested += OnAssetRequested;
			if (Config.TestMode) {
				Monitor.Log("Test mode is enabled. Giant crops will always spawn.", LogLevel.Debug);
				helper.Events.Input.ButtonPressed += OnButtonPressed;
			}
		}

		/*********
		** Private methods
		*********/
		/// <inheritdoc cref="IContentEvents.AssetRequested"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnAssetRequested(object? sender, AssetRequestedEventArgs e) {
			if (e.NameWithoutLocale.IsEquivalentTo("Data/GiantCrops")) {
				e.Edit(asset => {
					var giantCrops = asset.AsDictionary<string, GiantCropData>().Data;
					foreach ((string key, GiantCropData data) in giantCrops) {
						if (Config.TestMode) {
							data.Chance = 1;
						}
					}
				}, AssetEditPriority.Default + 1);
			} else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects")) {
				if (objectsNeedingCreated.Count == 0)
					return;

				e.Edit(asset => {
					var objects = asset.AsDictionary<string, ObjectData>().Data;
					// how to merge objects and objectsNeedingCreated?
					foreach ((string key, ObjectData data) in objectsNeedingCreated) {
						objects[key] = data;
					}
					objectsNeedingCreated.Clear();
				});
			}
		}

		/// <inheritdoc cref="IGameLoopEvents.DayEnding"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnDayEnding(object? sender, DayEndingEventArgs e) {
			Dictionary<string, int> cropList = HowManyGiantCrops();

			// These two dictionaries have matching keys (make this a tuple/class?)
			Dictionary<string, StardewValley.Object> cachedObjects = new();
			Dictionary<string, float> totalValue = new();
			Dictionary<string, int> totalQuantity = new();

			Inventory primaryBin = cachedShippingBins.First();
			// TODO: Support multiplayer (when useSeparateWallets is true)
			foreach (Inventory shippingBin in cachedShippingBins) {
				foreach (Item item in shippingBin) {
					if (item is StardewValley.Object validItem) {
						// The preserve index might break with custom crops...
						string matchedBigCropId = "";
						string cachedIdentifier = "";
						if (cropList.ContainsKey(validItem.QualifiedItemId)) {
							matchedBigCropId = validItem.QualifiedItemId;
							cachedIdentifier = validItem.QualifiedItemId;
						} else if (cropList.ContainsKey($"(O){validItem.preservedParentSheetIndex}")) {
							matchedBigCropId = $"(O){validItem.preservedParentSheetIndex}";
							cachedIdentifier = $"{validItem.QualifiedItemId}x{validItem.preservedParentSheetIndex}";
						}

						if (matchedBigCropId != "") {
							Monitor.Log($"Found {validItem.Name} in shipping bin which matches {matchedBigCropId}.");

							// hmm, this is all i needed to do huh...
							// validItem.Price += (int)(validItem.Price * Config.PercentIncrease);

							if (!totalValue.ContainsKey(cachedIdentifier)) {
								totalValue[cachedIdentifier] = 0;
								totalQuantity[cachedIdentifier] = 0;
								cachedObjects[cachedIdentifier] = validItem;
							}
							float modifier = (Config.PercentIncrease * cropList[matchedBigCropId]) + 1;
							totalValue[cachedIdentifier] += validItem.Price * modifier;
							// Moving to primaryBin later (todo: this destroys unqiue stacks like quality)
							totalQuantity[cachedIdentifier] += shippingBin.ReduceId(validItem.QualifiedItemId, validItem.Stack);
						}
					}
				}
			}

			foreach ((string refItemId, float refItemValue) in totalValue) {
				string generatedItemId = $"Tocseoj.BigCropBonus_{refItemId}";
				ObjectData generatedObjectData = new() {
					Name = $"{cachedObjects[refItemId].Name} Bonus",
					DisplayName = $"Bonus to {cachedObjects[refItemId].DisplayName}",
					Description = "Your bonus for having a Giant Crop.",
					Type = cachedObjects[refItemId].Type,
					Category = cachedObjects[refItemId].Category,
					Price = (int)Math.Ceiling(refItemValue),
					Texture = null,
					SpriteIndex = 26,
				};
				objectsNeedingCreated[generatedItemId] = generatedObjectData;
			}
			Helper.GameContent.InvalidateCache("Data/Objects");
			foreach (var refItem in totalValue) {
				string generatedItemId = $"Tocseoj.BigCropBonus_{refItem.Key}";
				StardewValley.Object bonus = new(generatedItemId, 1);
				StardewValley.Object refItemObject = cachedObjects[refItem.Key];
				refItemObject.Stack = totalQuantity[refItem.Key];
				primaryBin.Add(refItemObject);
				primaryBin.Add(bonus);
			}
			cachedShippingBins.Clear();

			Monitor.Log($"Total value: {string.Join(", ", totalValue.Select(pair => $"{pair.Key}: {pair.Value}"))}");
		}

		/// <summary>Get all giant crops in the game.</summary>
		private Dictionary<string, int> HowManyGiantCrops() {
			Dictionary<string, int> cropTypeCounts = new();
			Dictionary<string, string> cropTypeNames = new();

			// Looping through all locations and not just farm types to support any mods that allow crops to grow elsewhere
			// Plus this is only going to be running on day end so it should be fine
			cachedShippingBins.Clear();
			Utility.ForEachLocation(location => {
				foreach (GiantCrop giantCrop in location.resourceClumps.OfType<GiantCrop>()) {
					GiantCropData? giantCropItem = giantCrop.GetData();
					if (giantCropItem != null) {
						// Note the key is the source items id (for a Giant melon is '(O)254')
						if (!cropTypeCounts.ContainsKey(giantCropItem.FromItemId)) {
							cropTypeCounts[giantCropItem.FromItemId] = 0;
							cropTypeNames[giantCropItem.FromItemId] = giantCropItem.Condition;
						}
						cropTypeCounts[giantCropItem.FromItemId]++;
					}
				}
				// cache Farm location shippingBins and Object.Chests where SpecialChestType is Chest.SpecialChestTypes.MiniShippingBin
				if (location is Farm farm) {
					if (Game1.getFarm() == farm) {
						cachedShippingBins.Insert(0, (Inventory)farm.getShippingBin(Game1.player));
					} else {
						cachedShippingBins.Add((Inventory)farm.getShippingBin(Game1.player));
					}
				}
				// getting all mini shipping bins
				foreach (StardewValley.Object objects in location.objects.Values) {
					if (objects is Chest chest && chest.SpecialChestType == Chest.SpecialChestTypes.MiniShippingBin) {
						cachedShippingBins.Add(chest.Items);
					}
				}

				return true;
			});
			Monitor.Log($"Giant crops count: {string.Join(", ", cropTypeCounts.Select(pair => $"{pair.Key}: {pair.Value}"))}");
			Monitor.Log($"Giant crops conditions: {string.Join(", ", cropTypeNames.Select(pair => $"{pair.Key}: {pair.Value}"))}");
			return cropTypeCounts;
		}

		/// <inheritdoc cref="IInputEvents.ButtonPressed"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnButtonPressed(object? sender, ButtonPressedEventArgs e) {
			if (Config.TestMode && Context.IsWorldReady && e.Button.IsUseToolButton()) {
				// Monitor.Log($"OnButtonPressed: TODO if needed");
				HowManyGiantCrops();
				Monitor.Log($"Number of shipping bins: {cachedShippingBins.Count}");
			}
		}
	}

}
