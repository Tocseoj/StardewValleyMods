using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace Tocseoj.Stardew.StardewShuffle;
internal class ShuffleMinigame(IMonitor Monitor, IManifest ModManifest, IModHelper Helper, ModConfig Config)
	: ModComponent(Monitor, ModManifest, Helper, Config), IMinigame
{
	private bool quitMinigame = false;
	private Texture2D backgroundSprite = null!;
	private Texture2D springCrops = null!;
	public int gameWidth = 320; // Hardcoded size of assets/StardewShufflePlayingTable.png

	public int gameHeight = 160; // Hardcoded size of assets/StardewShufflePlayingTable.png
	private Vector2 upperLeft = Vector2.Zero;
	private List<int[]> playerCards = null!;
	public void Start()
	{
		if (!Context.IsWorldReady) {
			Monitor.Log("You need to load a save first!", LogLevel.Info);
			return;
		}
		quitMinigame = false;
		backgroundSprite = Helper.ModContent.Load<Texture2D>("assets/StardewShufflePlayingTable.png");
		springCrops = Game1.content.Load<Texture2D>("Maps\\springobjects");
		changeScreenSize();
		playerCards ??= [[1, 400], [2, 400], [3, 400], [4, 400], [5, 400]];
		Game1.currentMinigame = this;
	}
	public void AddConsoleCommand()
	{
		Helper.ConsoleCommands.Add("sshuffle", "Start a game of Stardew Shuffle.", (string command, string[] args) => {
			if (args.Length > 0 && args[0] == "--test-crane") {
				if (!Context.IsWorldReady)
					return;
				// Fixed resolution at 4x scale
				Game1.currentMinigame = new CraneGame();
				return;
			}
			if (args.Length > 0 && args[0] == "--test-boat") {
				if (!Context.IsWorldReady)
					return;
				// Will fill whole screen, but things will be cut off
				Game1.currentMinigame = new BoatJourney();
				return;
			}
			if (args.Length > 0 && args[0] == "--test-plane") {
				if (!Context.IsWorldReady)
					return;
				// unused/test animation?
				Game1.currentMinigame = new PlaneFlyBy();
				return;
			}
			if (args.Length > 0 && args[0] == "--test-game") {
				if (!Context.IsWorldReady)
					return;
				// card game
				Game1.currentMinigame = new CalicoJack();
				return;
			}
			if (args.Length > 0 && args[0] == "--shwip") {
				if (!Context.IsWorldReady)
					return;
				Game1.playSound("shwip");
				return;
			}
			if (args.Length > 0 && args[0] == "--version") {
				Monitor.Log($"Stardew Shuffle v{ModManifest.Version}", LogLevel.Info);
				return;
			}
			if (args.Length > 0 && args[0] == "--help") {
				Monitor.Log($"Usage: {command}", LogLevel.Info);
				Monitor.Log("Start a game of Stardew Shuffle.", LogLevel.Info);
				Monitor.Log("Options:", LogLevel.Info);
				Monitor.Log("--version: Display the version of Stardew Shuffle.", LogLevel.Info);
				Monitor.Log("--help: Display this help message.", LogLevel.Info);
				Monitor.Log("--test-crane: Run the crane game for comparison testing.", LogLevel.Info);
				Monitor.Log("--test-boat: Run the boat journey for comparison testing.", LogLevel.Info);
				Monitor.Log("--test-plane: Run the plane flyby for comparison testing.", LogLevel.Info);
				Monitor.Log("--test-game: Run CalicoJack card game for comparison testing.", LogLevel.Info);
				Monitor.Log("--shwip: Play the shwip sound.", LogLevel.Info);
				return;
			}
			if (args.Length > 0) {
				Monitor.Log("Invalid arguments. Use --help for more information.", LogLevel.Info);
				return;
			}
			Start();
		});
	}
	public bool tick(GameTime time)
	{
		if (time.TotalGameTime.TotalSeconds % 5 < 0.0001)
			Monitor.Log($"Ticking...{time.TotalGameTime.TotalSeconds}");
		if (quitMinigame)
			unload();

		for (int k = 0; k < playerCards.Count; k++)
		{
			if (playerCards[k][1] > 0)
			{
				playerCards[k][1] -= time.ElapsedGameTime.Milliseconds;
				if (playerCards[k][1] <= 0)
				{
					playerCards[k][1] = 0;
				}
			}
		}

		return quitMinigame;
	}
	public bool overrideFreeMouseMovement()
	{
		Monitor.Log("Overriding free mouse movement");
		return false;
	}
	public bool doMainGameUpdates()
	{
		// Monitor.Log("Doing main game updates"); // Runs every tick
		return false;
	}
	public void receiveLeftClick(int x, int y, bool playSound = true)
	{
		Monitor.Log($"Received left click at {x}, {y}. Playing sound: {playSound}");
	}
	public void leftClickHeld(int x, int y)
	{
		Monitor.Log($"Left click held at {x}, {y}");
	}
	public void releaseLeftClick(int x, int y)
	{
		Monitor.Log($"Released left click at {x}, {y}");
	}
	public void receiveRightClick(int x, int y, bool playSound = true)
	{
		Monitor.Log($"Received right click at {x}, {y}. Playing sound: {playSound}");
	}
	public void releaseRightClick(int x, int y)
	{
		Monitor.Log($"Released right click at {x}, {y}");
	}
	public void receiveKeyPress(Keys k)
	{
		Monitor.Log($"Received key press: {k}");
		if (k == Keys.Escape)
      quitMinigame = true;
	}
	public void receiveKeyRelease(Keys k)
	{
		Monitor.Log($"Received key release: {k}");
	}
	public void draw(SpriteBatch b)
	{
		b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
		b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.graphics.GraphicsDevice.Viewport.Width, Game1.graphics.GraphicsDevice.Viewport.Height), new Color(234, 145, 78));
		b.Draw(backgroundSprite, upperLeft, backgroundSprite.Bounds, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
		// Draw cards
		var xOffset = 16;
		var yOffset = 16;
		foreach (int[] i in playerCards) {
			IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), (int)upperLeft.X + xOffset, (int)upperLeft.Y + yOffset, 96, 144, Color.White, 4f);
			b.Draw(springCrops, upperLeft + new Vector2(xOffset, yOffset) + new Vector2(32f, 32f), new Rectangle(224, 160, 16, 16), Color.White, 0f, Vector2.Zero, 8f, SpriteEffects.None, 1f);
			xOffset += 96;
		}
		if (Game1.IsMultiplayer) {
			Utility.drawTextWithColoredShadow(b, Game1.getTimeOfDayString(Game1.timeOfDay), Game1.dialogueFont, new Vector2(Game1.graphics.GraphicsDevice.Viewport.Width - Game1.dialogueFont.MeasureString(Game1.getTimeOfDayString(Game1.timeOfDay)).X - 16f, Game1.graphics.GraphicsDevice.Viewport.Height - Game1.dialogueFont.MeasureString(Game1.getTimeOfDayString(Game1.timeOfDay)).Y - 10f), Color.White, Color.Black * 0.2f);
		}
		b.End();
	}
	public void changeScreenSize()
	{
		Monitor.Log("Changing screen size");
		float pixel_zoom_adjustment = 1f / Game1.options.zoomLevel;
		Rectangle localMultiplayerWindow = Game1.game1.localMultiplayerWindow;
		float w = localMultiplayerWindow.Width;
		float h = localMultiplayerWindow.Height;
		Vector2 tmp = new Vector2(w / 2f, h / 2f) * pixel_zoom_adjustment;
		tmp.X -= gameWidth / 2 * 4;
		tmp.Y -= gameHeight / 2 * 4;
		upperLeft = tmp;
	}
	public void unload()
	{
		Monitor.Log("Unloading");
	}
	public void receiveEventPoke(int data)
	{
		Monitor.Log($"Received event poke: {data}");
	}
	public string minigameId()
	{
		Monitor.Log("Getting minigame ID");
		return "Tocseoj.StardewShuffle";
	}
	public bool forceQuit()
	{
		Monitor.Log("Force quitting!");
		return quitMinigame;
	}
}