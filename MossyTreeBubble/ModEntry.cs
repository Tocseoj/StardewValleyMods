﻿using System.Collections.Immutable;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace Tocseoj.Stardew.MossyTreeBubble;

internal sealed class ModEntry : Mod
{
	// Location -> Tile -> (Tree, Bubble, Moss))
	private Dictionary<GameLocation, Dictionary<Vector2, ValueTuple<Tree, TemporaryAnimatedSprite, TemporaryAnimatedSprite>>> TreesAtLocation = [];
	public override void Entry(IModHelper helper)
	{
		helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
		helper.Events.Player.Warped += OnWarped;
		helper.Events.GameLoop.DayStarted += OnDayStarted;
		helper.Events.GameLoop.DayEnding += (sender, e) => TreesAtLocation.Clear();
		helper.Events.GameLoop.ReturnedToTitle += (sender, e) => TreesAtLocation.Clear();
		helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
		helper.Events.World.LocationListChanged += OnLocationListChanged;
	}

	private void OnWarped(object? sender, WarpedEventArgs e)
	{
		if (!e.IsLocalPlayer) return;

		GameLocation location = e.NewLocation;
		if (!TreesAtLocation.ContainsKey(location))
			FindMossyTrees(location);

		foreach ((_, (_, TemporaryAnimatedSprite bubble, TemporaryAnimatedSprite moss)) in TreesAtLocation[location]) {
			// Prevent adding the same sprites multiple times
			// The farm doesn't seem to clear temporary sprites after warp
			// so we need to check if the sprites are already there
			if (location.TemporarySprites.Contains(bubble))
				break;

			location.TemporarySprites.Add(bubble);
			location.TemporarySprites.Add(moss);
		}

		Monitor.Log($"Added {TreesAtLocation[location].Count} mossy tree sprites to {location.Name}.");
	}

	private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
	{
		if (!Context.IsWorldReady) return;

		GameLocation location = Game1.player.currentLocation;
		if (location == null || !TreesAtLocation.ContainsKey(location)) return;

		foreach ((Vector2 tile, (Tree tree, TemporaryAnimatedSprite bubble, TemporaryAnimatedSprite moss)) in TreesAtLocation[location]) {
			if (tree.hasMoss.Value == false) {
				Monitor.Log($"Moss taken from tree at {tile}. Removing sprites with id {bubble.id} and {moss.id}.");
				location.TemporarySprites.Remove(bubble);
				location.TemporarySprites.Remove(moss);
				TreesAtLocation[location].Remove(tile);
			}
		}
	}

	private bool FindMossyTrees(GameLocation location)
	{
		return FindMossyTrees(location, location.terrainFeatures.Pairs.ToImmutableDictionary());
	}

	private bool FindMossyTrees(GameLocation location, ImmutableDictionary<Vector2, TerrainFeature> features)
	{
		if (!TreesAtLocation.ContainsKey(location))
			TreesAtLocation[location] = [];
		foreach ((Vector2 tile, TerrainFeature feature) in features) {
			if (feature is Tree tree) {
				if (tree.hasMoss.Value == true && tree.growthStage.Value >= 5) {
					(TemporaryAnimatedSprite bubble, TemporaryAnimatedSprite moss) = BubbleSprites(tile);
					TreesAtLocation[location][tile] = (tree, bubble, moss);
				}
			}
		}
		return true;
	}

	private static (TemporaryAnimatedSprite bubble, TemporaryAnimatedSprite moss) BubbleSprites(Vector2 tile)
	{
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
		return (bubble, moss);
	}

	private void OnDayStarted(object? sender, DayStartedEventArgs e)
	{
		TreesAtLocation.Clear();
		Utility.ForEachLocation(FindMossyTrees);
		Monitor.Log($"Found {TreesAtLocation.Sum(pair => pair.Value.Count)} mossy trees.");
	}

	private void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
	{
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

	private void OnLocationListChanged(object? sender, LocationListChangedEventArgs e)
	{
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
