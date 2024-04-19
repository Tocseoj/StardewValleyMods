using System.Collections.Immutable;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Tocseoj.Stardew.MossyTreeBubble;

internal sealed class ModEntry : Mod {
	private readonly Dictionary<GameLocation, Dictionary<Vector2, Tuple<Tree, Tuple<TemporaryAnimatedSprite, TemporaryAnimatedSprite>>>> TreesAtLocation = new();
	public override void Entry(IModHelper helper) {
		helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
		helper.Events.Player.Warped += OnWarped;
		helper.Events.GameLoop.DayStarted += OnDayStarted;
		helper.Events.GameLoop.DayEnding += OnDayEnding;
		helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
		helper.Events.World.LocationListChanged += OnLocationListChanged;
	}

	private void OnWarped(object? sender, WarpedEventArgs e) {
		if (!Context.IsWorldReady) return;
		if (!e.IsLocalPlayer) return;

		GameLocation location = e.NewLocation;
		if (!TreesAtLocation.ContainsKey(location)) return;

		foreach ((_, (_, (TemporaryAnimatedSprite bubble, TemporaryAnimatedSprite moss))) in TreesAtLocation[location]) {
			if (location.TemporarySprites.Contains(bubble))
				break;
			location.TemporarySprites.Add(bubble);
			location.TemporarySprites.Add(moss);
		}

		Monitor.Log($"Added {TreesAtLocation[location].Count} mossy tree sprites to {location.Name}.");
	}

	private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e) {
		if (!Context.IsWorldReady) return;

		GameLocation location = Game1.currentLocation;
		if (!TreesAtLocation.ContainsKey(location)) return;

		foreach ((Vector2 tile, (Tree tree, (TemporaryAnimatedSprite bubble, TemporaryAnimatedSprite moss))) in TreesAtLocation[location]) {
			if (tree.hasMoss.Value == false) {
				Monitor.Log($"Moss taken from tree at {tile}. Removing sprites with id {bubble.id} and {moss.id}.");
				location.TemporarySprites.Remove(bubble);
				location.TemporarySprites.Remove(moss);
				TreesAtLocation[location].Remove(tile);
			}
		}
	}

	private void FindMossyTrees(GameLocation location) {
		FindMossyTrees(location, location.terrainFeatures.Pairs.ToImmutableDictionary());
	}

	private void FindMossyTrees(GameLocation location, ImmutableDictionary<Vector2, TerrainFeature> features) {
		foreach ((Vector2 tile, TerrainFeature feature) in features) {
			if (feature is Tree tree) {
				if (tree.hasMoss.Value == true && tree.growthStage.Value >= 5) {
					if (!TreesAtLocation.ContainsKey(location))
						TreesAtLocation[location] = new();

					Tuple<TemporaryAnimatedSprite, TemporaryAnimatedSprite> sprites = BubbleSprites(tile);
					TreesAtLocation[location][tile] = new(tree, sprites);
				}
			}
		}
	}

	private static Tuple<TemporaryAnimatedSprite, TemporaryAnimatedSprite> BubbleSprites(Vector2 tile) {
		Vector2 offset = new(2, -45);
		TemporaryAnimatedSprite bubble = new(
			textureName: "LooseSprites\\Cursors",
			sourceRect: new Rectangle(141, 465, 20, 24),
			animationInterval:	float.MaxValue, // ms per frame
			animationLength: 1, // frames in animation
			numberOfLoops: 0,
			position: tile * Game1.tileSize + offset,
			flicker: false,
			flipped: false
		) {
			alpha = 0.7f,
			drawAboveAlwaysFront = true,
			scale = 3f,
			yPeriodic = true,
			yPeriodicRange = 4f,
			yPeriodicLoopTime = 1500f,
		};
		Vector2 offset2 = new(5, 8);
		TemporaryAnimatedSprite moss = new(
			textureName: "TileSheets\\Objects_2",
			sourceRect: new Rectangle(96, 64, 16, 16),
			animationInterval: float.MaxValue, // ms per frame
			animationLength: 1, // frames in animation
			numberOfLoops: 0,
			position: tile * Game1.tileSize + offset + offset2,
			flicker: false,
			flipped: false
		) {
			alpha = 0.6f,
			drawAboveAlwaysFront = true,
			scale = 3f,
			yPeriodic = true,
			yPeriodicRange = 4f,
			yPeriodicLoopTime = 1500f,
		};
		return new Tuple<TemporaryAnimatedSprite, TemporaryAnimatedSprite>(bubble, moss);
	}

	private void OnDayStarted(object? sender, DayStartedEventArgs e) {
		foreach (GameLocation location in Game1.locations) {
      FindMossyTrees(location);
		}
		Monitor.Log($"Found {TreesAtLocation.Sum(pair => pair.Value.Count)} mossy trees.");
	}

	private void OnDayEnding(object? sender, DayEndingEventArgs e) {
		TreesAtLocation.Clear();
	}

	private void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e) {
		Monitor.Log($"TerrainFeatureListChanged: {e.Location.Name}.");
		FindMossyTrees(e.Location, e.Added.ToImmutableDictionary());

		// Remove single tree(s) from dictionary
		foreach ((Vector2 tile, TerrainFeature feature) in e.Removed) {
			if (feature is Tree) {
				if (TreesAtLocation.ContainsKey(e.Location) && TreesAtLocation[e.Location].ContainsKey(tile)) {
					TreesAtLocation[e.Location].Remove(tile);
				}
			}
		}
	}

	private void OnLocationListChanged(object? sender, LocationListChangedEventArgs e) {
		foreach (GameLocation location in e.Added) {
			Monitor.Log($"Adding {location.Name}.");
      FindMossyTrees(location);
		}

		// Remove location(s) from dictionary
		foreach (GameLocation location in e.Removed) {
			Monitor.Log($"Removing {location.Name}.");
			if (TreesAtLocation.ContainsKey(location)) {
				TreesAtLocation.Remove(location);
			}
		}
	}
}
