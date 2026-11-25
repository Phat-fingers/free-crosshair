# Free Crosshair

A free crosshair overlay for PC games on Windows 10 and 11. It puts a custom crosshair in the middle of your screen, on top of any game.

## Install

1. Go to **[Releases](../../releases/latest)** and download **FreeCrosshair.exe**.
2. Double-click it. Nothing to install. It runs on any Windows 10/11 PC.
3. The first time, Windows may say **"Windows protected your PC"**. Click **More info → Run anyway**. (This shows up because the app isn't code-signed, not because something is wrong.)

Tip: put the exe somewhere permanent (like `Documents\FreeCrosshair`) before ticking **Start with Windows**.

## Using it

| Key | Does |
|-----|------|
| **F8** | Show / hide the crosshair |
| **F9** | Switch to the next profile |
| **F10** | Open settings |

You can change these keys in settings. Closing the settings window keeps the crosshair running: find it in the system tray (bottom-right, near the clock). Right-click the tray icon to quit.

**Game must be in Borderless or Windowed mode.** Overlays can't draw over *exclusive* fullscreen. Almost every game has a "Borderless" / "Windowed Fullscreen" option in its video settings.

## Features

- Styles: cross, T-shape, X, dot, circle, circle + cross, or your own PNG image
- Color, line length, thickness, center gap, center dot (square or round), outline color and thickness
- Opacity, nudge left/right/up/down, pick which monitor
- Any number of profiles
- **Share codes**: "Copy crosshair code" gives a line of text a friend can paste into their app
- Live zoomable preview

## Is it safe with anti-cheat?

The app never touches the game. No injection, no memory reading, no file changes. It's a separate see-through window on top of the screen, like a Discord overlay. Still, some competitive games or tournaments have their own rules about crosshair overlays, so check the rules of the game you play.

## Where settings are saved

`%APPDATA%\FreeCrosshair\config.ini`. Delete that folder to reset everything.

## Building from source

Needs the .NET SDK on Windows:

```
dotnet build FreeCrosshair.csproj -c Release -o out
```

Every push is built and tested by GitHub Actions. To ship an update, bump `<Version>` in `FreeCrosshair.csproj` and push to `main`: the build publishes a new release with the exe automatically.

## License

MIT
