using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Minigames;

namespace Tocseoj.Stardew.StardewShuffle;
internal class ShuffleMinigame(IMonitor Monitor, IManifest ModManifest, IModHelper Helper, ModConfig Config)
	: ModComponent(Monitor, ModManifest, Helper, Config), IMinigame
{
	private bool quitMinigame = false;
	public void Start()
	{
		if (!Context.IsWorldReady) {
			Monitor.Log("You need to load a save first!", LogLevel.Info);
			return;
		}
		quitMinigame = false;
		Game1.currentMinigame = this;
	}
	public void AddConsoleCommand()
	{
		Helper.ConsoleCommands.Add("sshuffle", "Start a game of Stardew Shuffle.", (string command, string[] args) => {
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
		// Monitor.Log($"Drawing...{b}"); // Runs every tick
	}
	public void changeScreenSize()
	{
		Monitor.Log("Changing screen size");
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