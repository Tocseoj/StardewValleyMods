
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

		/// <summary>The player's money at the previous tick.</summary>
		private int prevMoney;

		/// <summary>Objects that need to be created using AssetRequested.</summary>
		private Dictionary<string, ObjectData> objectsNeedingCreated = new();

		/*********
		** Public methods
		*********/
		/// <inheritdoc/>
		public override void Entry(IModHelper helper) {
			Config = helper.ReadConfig<ModConfig>();

			helper.Events.GameLoop.DayEnding += OnDayEnding;
			if (Config.TestMode) {
				Monitor.Log("Test mode is enabled. Giant crops will always spawn.", LogLevel.Debug);
				helper.Events.Content.AssetRequested += OnAssetRequested;
				helper.Events.Input.ButtonPressed += OnButtonPressed;
				helper.Events.GameLoop.Saving += (sender, e) => Monitor.Log($"Player money Saving: {Game1.player.Money} : and shipping bin count: {Game1.getFarm().getShippingBin(Game1.player).Count}");
				Helper.Events.World.ChestInventoryChanged += (sender, e) => Monitor.Log($"Player money ChestInventoryChanged({e.Chest}): {Game1.player.Money} : and shipping bin count: {Game1.getFarm().getShippingBin(Game1.player).Count}");
				Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
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
			Monitor.Log($"Player money DayEnding: {Game1.player.Money} : and shipping bin count: {Game1.getFarm().getShippingBin(Game1.player).Count}");

			Dictionary<string, int> cropList = HowManyGiantCrops();

			Dictionary<string, StardewValley.Object> cachedObjects = new();
			Dictionary<string, float> totalValue = new();

			Inventory primaryBin = (Inventory)Game1.getFarm().getShippingBin(Game1.player);
			List<Inventory> shippingBins = new() { primaryBin };
			foreach (Inventory shippingBin in shippingBins) {
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
								cachedObjects[cachedIdentifier] = validItem;
							}
							float modifier = (Config.PercentIncrease * cropList[matchedBigCropId]) + 1;
							totalValue[cachedIdentifier] += validItem.Price * modifier;
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
				primaryBin.Add(bonus);
			}
			Inventory newOrder = new();
			foreach (Item i in primaryBin.OrderBy(item => item.Name)) {
				newOrder.Add(i);
			}
			primaryBin.OverwriteWith(newOrder);
			Monitor.Log($"Total value: {string.Join(", ", totalValue.Select(pair => $"{pair.Key}: {pair.Value}"))}");
		}

		/// <inheritdoc cref="IInputEvents.ButtonPressed"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnButtonPressed(object? sender, ButtonPressedEventArgs e) {
			if (Config.TestMode && Context.IsWorldReady && e.Button.IsUseToolButton()) {

				Monitor.Log($"Player money OnButtonPressed: {Game1.player.Money} : and shipping bin count: {Game1.getFarm().getShippingBin(Game1.player).Count}");

				Dictionary<string, int> cropList = HowManyGiantCrops();

				foreach ((string key, int count) in cropList) {
					// TODO: Support multiplayer (when useSeparateWallets is true)
					Inventory shippingBin = (Inventory)Game1.getFarm().getShippingBin(Game1.player);

					foreach(Item item in shippingBin) {
						string? preserveId = Helper.Reflection.GetField<NetString>(item, "preservedParentSheetIndex").GetValue()?.Value;
						if (item.QualifiedItemId == key || $"(O){preserveId}" == key) {
							Monitor.Log($"Found {item.Name} in shipping bin which matches {key}.");
						}
					}
				}
			}
		}

		/// <inheritdoc cref="IGameLoopEvents.UpdateTicked"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e) {
			if (!Context.IsWorldReady)
				return;

			if (Game1.player.Money != prevMoney) {
				Monitor.Log($"Player money OnButtonPressed: {Game1.player.Money} : and shipping bin count: {Game1.getFarm().getShippingBin(Game1.player).Count}");
				prevMoney = Game1.player.Money;
			}
		}

		/// <summary>Get all giant crops in the game.</summary>
		private Dictionary<string, int> HowManyGiantCrops() {
			Dictionary<string, int> cropTypeCounts = new();

			// Looping through all locations and not just farm types to support any mods that allow crops to grow elsewhere
			// Plus this is only going to be running on day end so it should be fine
			Utility.ForEachLocation(location => {
				foreach (GiantCrop giantCrop in location.resourceClumps.OfType<GiantCrop>()) {
					string? item_key = giantCrop.GetData()?.FromItemId;
					if (item_key != null) {
							// Note the key is the source items id (for a Giant melon is '(O)254')
						if (cropTypeCounts.ContainsKey(item_key)) {
							cropTypeCounts[item_key]++;
						} else {
							cropTypeCounts[item_key] = 1;
						}
					}
				}
				// Need to cache Farm shippingBins and Object.Chests where SpecialChestType is Chest.SpecialChestTypes.MiniShippingBin
				StardewValley.Objects.Chest bin = new();
				return true;
			});
			Monitor.Log($"Giant crops: {string.Join(", ", cropTypeCounts.Select(pair => $"{pair.Key}: {pair.Value}"))}");
			return cropTypeCounts;
		}
	}

}
