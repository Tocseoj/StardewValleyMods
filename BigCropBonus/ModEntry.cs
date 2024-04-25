using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData.GiantCrops;
using StardewValley.GameData.Objects;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace Tocseoj.Stardew.BigCropBonus
{
  public sealed class ModConfig {
		/// <summary>Whether to enable test mode, which makes giant crops always spawn (where valid).</summary>
		public bool TestMode { get; set; } = false;

		public KeybindList TraceLog { get; set; } = new KeybindList(SButton.T);

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

			helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			helper.Events.Content.AssetRequested += OnAssetRequested;
			helper.Events.GameLoop.DayEnding += OnDayEnding;
			helper.Events.GameLoop.DayStarted += OnDayStarted;
			helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
			helper.Events.Player.InventoryChanged += OnInventoryChanged;
			if (Config.TestMode) {
				Monitor.Log("Test mode is enabled. Giant crops will always spawn.", LogLevel.Debug);
				helper.Events.Input.ButtonPressed += OnButtonPressed;
			}
		}

		/*********
		** Private methods
		*********/
		/// <inheritdoc cref="IContentEvents.GameLaunched"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnGameLaunched(object? sender, GameLaunchedEventArgs e){
		 		// get Generic Mod Config Menu's API (if it's installed)
				var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
				if (configMenu is null)
						return;

				// register mod
				configMenu.Register(
						mod: this.ModManifest,
						reset: () => this.Config = new ModConfig(),
						save: () => this.Helper.WriteConfig(this.Config)
				);

				// add some config options
				configMenu.AddBoolOption(
						mod: this.ModManifest,
						name: () => "Test Mode",
						tooltip: () => "Whether to enable test mode, which makes giant crops always spawn (where valid).",
						getValue: () => this.Config.TestMode,
						setValue: value => this.Config.TestMode = value
				);
				configMenu.AddKeybindList(
						mod: this.ModManifest,
						name: () => "Trace to Log",
						tooltip: () => "Keybind for debugging.",
						getValue: () => this.Config.TraceLog,
						setValue: value => this.Config.TraceLog = value
				);
				configMenu.AddNumberOption(
						mod: this.ModManifest,
						name: () => "Percent (%) Increase",
						tooltip: () => "The percent (%) increase in value of giant crops.",
						getValue: () => (float)Math.Truncate(this.Config.PercentIncrease * 100),
						setValue: value => this.Config.PercentIncrease = (float)Math.Round(value / 100, 2)
				);
		}

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
			if (Config.TestMode && Context.IsWorldReady && Config.TraceLog.JustPressed()) {
				HowManyGiantCrops();
				Monitor.Log($"Number of shipping bins: {cachedShippingBins.Count}");
			}
		}

		/// <inheritdoc cref="IDisplayEvents.RenderedActiveMenu"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e) {
			if (Game1.activeClickableMenu != null) {
				// IClickableMenu.drawHoverText(e.SpriteBatch, "Test in moderation.", Game1.smallFont);
				// Monitor.Log($"Active menu: {Game1.activeClickableMenu}");
				if (Game1.player.stats.Get("Book_PriceCatalogue") != 0 && Game1.activeClickableMenu is ItemGrabMenu itemGrabMenu) {
					if (itemGrabMenu.hoveredItem != null && itemGrabMenu.hoveredItem is StardewValley.Object validItem) {
						Dictionary<string, int> cropList = GetGiantCrops();
						// The preserve index might break with custom crops...
						string matchedBigCropId = "";
						if (cropList.ContainsKey(validItem.QualifiedItemId)) {
							matchedBigCropId = validItem.QualifiedItemId;
						} else if (cropList.ContainsKey($"(O){validItem.preservedParentSheetIndex}")) {
							matchedBigCropId = $"(O){validItem.preservedParentSheetIndex}";
						}
						if (matchedBigCropId != "") {
							float modifier = Config.PercentIncrease * cropList[matchedBigCropId];
							int bonusMoney = (int)(validItem.Price * modifier);

							IClickableMenu.drawToolTip(
								e.SpriteBatch,
								$"{itemGrabMenu.hoveredItem.getDescription()}(+{Math.Truncate(modifier*100)}%)",
								itemGrabMenu.hoveredItem.DisplayName,
								itemGrabMenu.hoveredItem,
								Game1.player.CursorSlotItem != null,
								moneyAmountToShowAtBottom: (validItem.Price + bonusMoney) * validItem.Stack);
						}
					}
				}
				else if (Game1.player.stats.Get("Book_PriceCatalogue") != 0 && Game1.activeClickableMenu is GameMenu gameMenu) {
					IClickableMenu page = gameMenu.pages[gameMenu.currentTab];
					if (page is InventoryPage inventoryPage) {
						if (inventoryPage.hoveredItem != null && inventoryPage.hoveredItem is StardewValley.Object validItem) {
							Dictionary<string, int> cropList = GetGiantCrops();
							// The preserve index might break with custom crops...
							string matchedBigCropId = "";
							if (cropList.ContainsKey(validItem.QualifiedItemId)) {
								matchedBigCropId = validItem.QualifiedItemId;
							} else if (cropList.ContainsKey($"(O){validItem.preservedParentSheetIndex}")) {
								matchedBigCropId = $"(O){validItem.preservedParentSheetIndex}";
							}
							if (matchedBigCropId != "") {
								float modifier = Config.PercentIncrease * cropList[matchedBigCropId];
								int bonusMoney = (int)(validItem.Price * modifier);

								IClickableMenu.drawToolTip(
									e.SpriteBatch,
									$"{inventoryPage.hoveredItem.getDescription()}(+{Math.Truncate(modifier*100)}%)",
									inventoryPage.hoveredItem.DisplayName,
									inventoryPage.hoveredItem,
									Game1.player.CursorSlotItem != null,
									moneyAmountToShowAtBottom: (validItem.Price + bonusMoney) * validItem.Stack);
							}
						}
					}
				}
				else if (Game1.activeClickableMenu is ShopMenu shopMenu) {
					if (shopMenu.hoveredItem != null) {
						// buying an item
					}
					else if (!string.IsNullOrEmpty(shopMenu.hoverText)) {
						Vector2 mousePosition = Helper.Input.GetCursorPosition().GetScaledScreenPixels();
						foreach (ClickableComponent slot in shopMenu.inventory.inventory) {
							if (slot.bounds.Contains(mousePosition)) {
								Item hoveredItem = Game1.player.Items[slot.myID];
								if (hoveredItem != null && hoveredItem is StardewValley.Object validItem) {
									Dictionary<string, int> cropList = GetGiantCrops();
									// The preserve index might break with custom crops...
									string matchedBigCropId = "";
									if (cropList.ContainsKey(validItem.QualifiedItemId)) {
										matchedBigCropId = validItem.QualifiedItemId;
									} else if (cropList.ContainsKey($"(O){validItem.preservedParentSheetIndex}")) {
										matchedBigCropId = $"(O){validItem.preservedParentSheetIndex}";
									}
									if (matchedBigCropId != "") {
										float modifier = Config.PercentIncrease * cropList[matchedBigCropId];
										int bonusMoney = (int)(validItem.Price * modifier);

										IClickableMenu.drawHoverText(
											e.SpriteBatch,
											// Melon (+10%) x1
											$"{validItem.DisplayName} (+{Math.Truncate(modifier*100)}%) x{validItem.Stack}",
											Game1.smallFont,
											moneyAmountToDisplayAtBottom: shopMenu.hoverPrice + (bonusMoney * validItem.Stack));
									}
								}
							}
						}
					}
				}
			}
		}

		/// <inheritdoc cref="IPlayerEvents.InventoryChanged"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e) {
			if (!e.IsLocalPlayer) return;

			foreach (Item item in e.Removed) {
				Monitor.Log($"Removed item: {item.DisplayName}");
				SoldItem(e.Player, item, item.Stack);
			}
			foreach (ItemStackSizeChange stackChange in e.QuantityChanged) {
				Monitor.Log($"Quantity changed: {stackChange.Item.DisplayName} from {stackChange.OldSize} to {stackChange.NewSize}");
				int soldCount = stackChange.OldSize - stackChange.NewSize;
				SoldItem(e.Player, stackChange.Item, soldCount);
			}
		}

		/// <summary>Handle selling an item.</summary>
		/// <param name="item">The item being sold.</param>
		/// <param name="count">The number of items being sold.</param>
		private void SoldItem(Farmer player, Item item, int count) {
			if (Game1.activeClickableMenu is ShopMenu shopMenu) {
				if (item is StardewValley.Object validItem) {
					Dictionary<string, int> cropList = GetGiantCrops();
					// The preserve index might break with custom crops...
					string matchedBigCropId = "";
					if (cropList.ContainsKey(validItem.QualifiedItemId)) {
						matchedBigCropId = validItem.QualifiedItemId;
					} else if (cropList.ContainsKey($"(O){validItem.preservedParentSheetIndex}")) {
						matchedBigCropId = $"(O){validItem.preservedParentSheetIndex}";
					}
					if (matchedBigCropId != "") {
						Monitor.Log($"Sold {count} {item.DisplayName}(s) for {item.sellToStorePrice()}g ea. which matches a giant crop {matchedBigCropId}.");

						float modifier = Config.PercentIncrease * cropList[matchedBigCropId];
						int bonusMoney = (int)(validItem.Price * modifier);
						ShopMenu.chargePlayer(player, shopMenu.currency, -bonusMoney * count);

						Monitor.Log($"Bonus money: {bonusMoney} * {count} = {bonusMoney * count}g");
					}
				}
			}
		}

		/// <summary>Get giant crops (only).</summary>
		private static Dictionary<string, int> GetGiantCrops() {
			Dictionary<string, int> cropTypeCounts = new();
			Utility.ForEachLocation(location => {
				foreach (GiantCrop giantCrop in location.resourceClumps.OfType<GiantCrop>()) {
					GiantCropData? giantCropItem = giantCrop.GetData();
					if (giantCropItem != null) {
						// Note the key is the source items id (for a Giant melon, it is '(O)254')
						if (!cropTypeCounts.ContainsKey(giantCropItem.FromItemId)) {
							cropTypeCounts[giantCropItem.FromItemId] = 0;
						}
						cropTypeCounts[giantCropItem.FromItemId]++;
					}
				}
				return true;
			});
			return cropTypeCounts;
		}
	}
}
