using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Extensions;
using StardewValley.Menus;
using StardewValley.Minigames;
using SObject = StardewValley.Object;

namespace Tocseoj.Stardew.StardewShuffle;
internal class ShuffleMinigame(IMonitor Monitor, IManifest ModManifest, IModHelper Helper, ModConfig Config)
	: ModComponent(Monitor, ModManifest, Helper, Config), IMinigame
{
	private bool quitMinigame = false;
	private Texture2D backgroundSprite = null!;
	private Texture2D springCrops = null!;
	const int GAME_WIDTH = 320; // Hardcoded size of assets/StardewShufflePlayingTable.png

	const int GAME_HEIGHT = 160; // Hardcoded size of assets/StardewShufflePlayingTable.png

	const float SCALE = 4f;
	// 60x76 56x76 52x76 56x76
	private readonly List<Rectangle> cardSlots = [
		// Top row
		new(45, 0, 52, 76),
		new(108, 0, 52, 76),
		new(166, 0, 52, 76),
		new(224, 0, 52, 76),
		// Bottom row
		new(45, 84, 52, 76),
		new(108, 84, 52, 76),
		new(166, 84, 52, 76),
		new(224, 84, 52, 76),
	];
	private List<Rectangle> scaledCardSlots = null!;
	private Rectangle scaledGameRect = Rectangle.Empty;
	private Vector2 upperLeft = Vector2.Zero;
	private List<int[]> playerCards = null!;

	private Dictionary<string, Texture2D> cardTextures = [];
	private class Card
	{
		public Character? owner;
		public Rectangle? sourceRect;
		public string? texturePath;
		public string name = "";
		public int goldValue = 0;
	}
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
	private Texture2D LoadTextureFromPath(string path)
	{
		if (!cardTextures.ContainsKey(path))
			cardTextures[path] = Helper.ModContent.Load<Texture2D>(path);
		return cardTextures[path];
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
		b.Draw(backgroundSprite, scaledGameRect.Location.ToVector2(), backgroundSprite.Bounds, Color.White, 0f, Vector2.Zero, SCALE, SpriteEffects.None, 1f);

		Rectangle frontOfCardSourceRect = new(384, 396, 15, 15);
		Rectangle backOfCardSourceRect = new(399, 396, 15, 15);
		Rectangle melonCardSourceRect = new(224, 160, 16, 16);
		Rectangle cauliflowerCardSourceRect = new(352, 112, 16, 16);
		int xMargin = 16;
		int yMargin = 12;
		// Layer 1
		foreach (Rectangle r in scaledCardSlots) {
			IClickableMenu.drawTextureBox(b, Game1.mouseCursors, frontOfCardSourceRect, r.Left + xMargin, r.Top + yMargin, r.Width - xMargin * 2, r.Height - yMargin * 2, Color.White, SCALE);
			float CropScale = SCALE * 2;
			b.Draw(springCrops, r.Center.ToVector2() - new Vector2(8*CropScale, 8*CropScale), cauliflowerCardSourceRect, Color.White, 0f, Vector2.Zero, CropScale, SpriteEffects.None, 1f);

			// Title & Cost
			string cardName = "Cauliflower";
			if (cardName.Length > 6) {
				SpriteText.drawStringHorizontallyCenteredAt(b, cardName[..6] + ".", r.Center.X + 6, r.Top + 32);
			}
			else {
				SpriteText.drawStringHorizontallyCenteredAt(b, cardName, r.Center.X, r.Top + 32);
			}
			SpriteText.drawStringHorizontallyCenteredAt(b, "250g", r.Center.X + 10, r.Bottom - 74);
		}

		// Layer 2
		foreach (Rectangle r in scaledCardSlots) {
			if (r.Contains(Game1.getOldMouseX(), Game1.getOldMouseY())) {
				SObject cauliflower = new("190", 1, false, -1, SObject.lowQuality);
				IClickableMenu.drawHoverText(b, $"Can be sold to the shop", Game1.smallFont, moneyAmountToDisplayAtBottom: cauliflower.Price, boldTitleText: cauliflower.DisplayName);
			}
		}

		// From CalicoJack.cs
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
		tmp.X -= GAME_WIDTH / 2 * SCALE;
		tmp.Y -= GAME_HEIGHT / 2 * SCALE;
		upperLeft = tmp; // no longer needed
		scaledGameRect = new((int)tmp.X, (int)tmp.Y, (int)(GAME_WIDTH * SCALE), (int)(GAME_HEIGHT * SCALE));
		scaledCardSlots = [];
		foreach (Rectangle r in cardSlots) {
			scaledCardSlots.Add(new((int)(r.X * SCALE + tmp.X), (int)(r.Y * SCALE + tmp.Y), (int)(r.Width * SCALE), (int)(r.Height * SCALE)));
		}
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