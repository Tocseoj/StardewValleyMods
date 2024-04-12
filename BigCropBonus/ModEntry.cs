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
		public bool TestMode { get; set; } = false;

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

			helper.Events.Content.AssetRequested += OnAssetRequested;
			helper.Events.GameLoop.DayEnding += OnDayEnding;
			helper.Events.GameLoop.DayStarted += OnDayStarted;
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
					foreach ((string key, ObjectData data) in objectsNeedingCreated) {
						objects[key] = data;
					}
					objectsNeedingCreated.Clear();
				});
			}
		}

		/// <inheritdoc cref="IGameLoopEvents.DayStarted"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		[EventPriority(EventPriority.High - 1)]
		private void OnDayStarted(object? sender, DayStartedEventArgs e) {
			objectsNeedingCreated.Clear();
			Helper.GameContent.InvalidateCache("Data/Objects");
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

							// uh... how do we handle unique stacks?
							// just going to include quality as the second part of the identifier
							// but this will be very fragile code
							string comboIdentifier = $"Q{validItem.Quality}_{validItem.QualifiedItemId}";
							if (!totalValue.ContainsKey(comboIdentifier)) {
								totalQuantity[comboIdentifier] = 0;
								float modifier = Config.PercentIncrease * cropList[matchedBigCropId];
								totalValue[comboIdentifier] = modifier;
								cachedObjects[comboIdentifier] = validItem;
							}
							// quantity to re-add to primaryBin later
							totalQuantity[comboIdentifier] += shippingBin.ReduceId(validItem.QualifiedItemId, validItem.Stack);
						}
					}
				}
			}

			foreach ((string refItemId, float refItemValue) in totalValue) {
				string generatedItemId = $"Tocseoj.BigCropBonus_{refItemId}";
				ObjectData generatedObjectData = new() {
					Name = $"{cachedObjects[refItemId].Name} Bonus",
					DisplayName = $"{Math.Round(refItemValue * 100)}% big crop bonus",
					Description = "Your bonus for having a Giant Crop.",
					Type = cachedObjects[refItemId].Type,
					Category = cachedObjects[refItemId].Category,
					Price = (int)Math.Ceiling(cachedObjects[refItemId].Price * refItemValue),
					Texture = null,
					SpriteIndex = 26,
				};
				objectsNeedingCreated[generatedItemId] = generatedObjectData;
			}
			Helper.GameContent.InvalidateCache("Data/Objects");
			foreach (var refItem in totalValue) {
				string generatedItemId = $"Tocseoj.BigCropBonus_{refItem.Key}";
				StardewValley.Object refItemObject = cachedObjects[refItem.Key];
				refItemObject.Stack = totalQuantity[refItem.Key];
				primaryBin.Add(refItemObject);
				StardewValley.Object bonus = new(generatedItemId, totalQuantity[refItem.Key], false, -1, cachedObjects[refItem.Key].Quality);
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
						// Note the key is the source items id (for a Giant melon, it is '(O)254')
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
				HowManyGiantCrops();
				Monitor.Log($"Number of shipping bins: {cachedShippingBins.Count}");
			}
		}
	}

}
