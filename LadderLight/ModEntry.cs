using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;

namespace Tocseoj.Stardew.LadderLight {
	public sealed class ModConfig {
		public bool DebugMode { get; set; } = false;
	}

	/// <summary>The mod entry point.</summary>
	internal sealed class ModEntry : Mod
	{
		/*********
		** Fields
		*********/
		/// <summary>The mod configuration.</summary>
		private ModConfig Config = null!; // set in Entry

		/// <summary>The mine shaft that the player is currently in.</summary>
		private MineShaft? mineShaft = null;

		/// <summary>Queue of ladders to be processed.</summary>
		private List<KeyValuePair<Point, bool>> ladderQueue = new List<KeyValuePair<Point, bool>>();

		/// <summary>Whether the first tick has been skipped.</summary>
		private bool skippedFirstTick = false;

		/*********
		** Public methods
		*********/
		/// <summary>The mod entry point, called after the mod is first loaded.</summary>
		/// <param name="helper">Provides simplified APIs for writing mods.</param>
		public override void Entry(IModHelper helper)
		{
			Config = helper.ReadConfig<ModConfig>();

			helper.Events.World.LocationListChanged += OnLocationListChanged;
			helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
		}


		/*********
		** Private methods
		*********/
		/// <inheritdoc cref="IGameLoopEvents.UpdateTicked"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
		{
			if (ladderQueue.Count > 0) {
				if (!skippedFirstTick) {
					skippedFirstTick = true;
					return;
				}
				KeyValuePair<Point, bool> ladder = ladderQueue[0];
				Monitor.Log("Delayed Add.", LogLevel.Debug);
				OnLadderLocationAdded(ladder.Key, ladder.Value);
				ladderQueue.RemoveAt(0);
			}
		}

		/// <inheritdoc cref="IWorldEvents.LocationListChanged"/>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event data.</param>
		private void OnLocationListChanged(object? sender, LocationListChangedEventArgs e)
		{
			// go through each location in e.Added and see if its a mine
			foreach (MineShaft location in e.Added)
			{
				if (location is not null)
				{
					int laddersPresent = 0;
					mineShaft = location;
					skippedFirstTick = false;
					// get generated ladders (creation/objects/npcs)
					IReflectedField<NetPointDictionary<bool, NetBool>> generatedLadders = Helper.Reflection.GetField<NetPointDictionary<bool, NetBool>>(location, "createLadderDownEvent", false);
					if (generatedLadders != null) {
						generatedLadders.GetValue().OnValueAdded += OnLadderLocationAdded;
						laddersPresent = generatedLadders.GetValue().Count();
						foreach (KeyValuePair<Point, bool> ladder in generatedLadders.GetValue().Pairs) {
							ladderQueue.Add(ladder);
						}
					}
					// get placed ladders (from player)
					IReflectedField<NetVector2Dictionary<bool, NetBool>> placedLadders = Helper.Reflection.GetField<NetVector2Dictionary<bool, NetBool>>(location, "createLadderAtEvent", false);
					if (placedLadders != null) {
						placedLadders.GetValue().OnValueAdded += OnLadderLocationAdded;
					}
					// Debug
					string debugMessage = $"Level {location.mineLevel}: {laddersPresent} ladders present.";
					Monitor.Log(debugMessage, LogLevel.Debug);
					if (Config.DebugMode)
						Game1.addHUDMessage(new HUDMessage(debugMessage, HUDMessage.newQuest_type));
				}
			}
		}

		private void OnLadderLocationAdded(Point point, bool shaft) {
			OnLadderLocationAdded(new Vector2(point.X, point.Y), shaft);
		}
		private void OnLadderLocationAdded(Vector2 point, bool shaft) {
			// Debug
			string debugMessage = $"Ladder appeared at {point}!";
			Monitor.Log(debugMessage, LogLevel.Debug);
			if (Config.DebugMode)
				Game1.addHUDMessage(new HUDMessage(debugMessage, HUDMessage.newQuest_type));

			// Create light source at point
			mineShaft?.TemporarySprites.Add(
				new TemporaryAnimatedSprite(
					"LooseSprites\\Lighting\\lantern",
					new Rectangle(0, 0, 128 ,  128),
					9999f, // 75ms per frame
					1, // 12 frames
					9999, // 5 times
					point * 64f, // position
					flicker: false,
					flipped: false,
					-1f,
					0f, // fade
					Config.DebugMode ? Utility.getRandomRainbowColor() : Color.Transparent, // color
					0.5f, // scale
					0f, // scale change
					0f, // rotation
					0f // rotation speed
				) {
					drawAboveAlwaysFront = true,
					light = true,
					lightRadius = 0.33f,
					lightFade = 0
			});
			Monitor.Log("Added light.", LogLevel.Debug);

		}
	}

}
