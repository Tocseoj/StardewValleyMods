using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.GiantCrops;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.TerrainFeatures;

namespace Tocseoj.Stardew.BigCropBonus {
	public sealed class ModConfig {
		public bool TestMode { get; set; } = true; // makes giant crops always spawn
		public float PercentIncrease { get; set; } = 0.1f; // 10% increase
	}

	/// <summary>The mod entry point.</summary>
	internal sealed class ModEntry : Mod {
		/*********
		** Fields
		*********/
		/// <summary>The mod configuration.</summary>
		private ModConfig Config = null!; // set in Entry
		private readonly Dictionary<string, float> modifiers = new();


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
			}
		}

		/// <inheritdoc cref="IGameLoopEvents.DayEnding"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnDayEnding(object? sender, DayEndingEventArgs e) {
			modifiers.Clear();
			GetGiantCrops();
			// get shipping bin items
			foreach (Item item in Game1.getFarm().getShippingBin(Game1.player).Where(item => item is not null)) {
				foreach (var modifier in modifiers) {
					string modified_key = Regex.Replace(modifier.Key, @"^\(.*\)", "");
					// TODO : Check if other mods that add big crops also use this tag
					if (item.HasContextTag($"preserve_sheet_index_{modified_key}")) {
						Monitor.Log($"Preserved {item.QualifiedItemId} with {modifier.Key} is worth {item.salePrice()}", LogLevel.Debug);
						break;
					}
				}
			}
		}

		/// <inheritdoc cref="IInputEvents.ButtonPressed"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnButtonPressed(object? sender, ButtonPressedEventArgs e) {
			if (e.Button.IsUseToolButton()) {
				GetGiantCrops();
			}
		}

		/// <summary>Get all giant crops in the game.</summary>
		private void GetGiantCrops() {
			if (!Context.IsWorldReady)
				return;

			Utility.ForEachLocation(location => {
				foreach (GiantCrop giantCrop in location.resourceClumps.OfType<GiantCrop>()) {
					Monitor.Log($"Found big version of {giantCrop.GetData()?.FromItemId}", LogLevel.Trace);
					string? item_key = giantCrop.GetData()?.FromItemId;
					if (item_key != null) {
						if (!modifiers.ContainsKey(item_key)) {
							modifiers[item_key] = 0;
						}
						modifiers[item_key] += Config.PercentIncrease;
					}
				}
				return true;
			});
			Monitor.Log($"Giant crops: {string.Join(", ", modifiers.Select(pair => $"{pair.Key}: {pair.Value * 100}%"))}", LogLevel.Debug);
		}
	}

}
