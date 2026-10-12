# AiSeasonCreator REDUX

Create iRacing **AI seasons** and **AI rosters** that match the official multiplayer series — tracks, cars, race lengths, rules and real weather forecasts — and edit existing rosters with minimal effort.

> Keep in mind that some cars and tracks are not AI-enabled in iRacing; those are skipped automatically.

## What's new in REDUX

- **Always up to date, no app update per season.** The app downloads the current series, schedules, cars, tracks, logos and weather from iRacing using *your own* iRacing account. When a new season starts, it just shows up.
- **Sign in on iRacing's own website** inside the app (embedded Microsoft Edge / WebView2). AiSeasonCreator never sees your password; it only uses the resulting web session, exactly like the iRacing web UI does.
- **Works offline**: the last downloaded data is cached on your PC. A bundled snapshot is used until your first sign-in.
- **Real weather forecasts** per race week, downloaded on demand and cached.
- Series search box, week numbers and details on each track, clear error messages, status bar showing which season data is loaded.
- Bug fixes: lap-limited race length, duplicated weather keyframes, same track in several weeks, season/roster name clashes, invalid file names, crashes when logos or new cars were missing, update check errors at every start.

## Install

1. Download `AiSeasonCreator-vX.Y.Z-win-x64.zip` from [Releases](https://github.com/nishizumi-maho/AiSeasonCreatorREDUX/releases).
2. Extract it anywhere and run `AiSeasonCreator.exe` (no .NET install required).
3. On first start, choose **Sign in** and log in to iRacing. Season data downloads automatically.

Requires Windows 10/11 with the Microsoft Edge WebView2 Runtime (pre-installed on current Windows).

## How the live data works

The iRacing Data API no longer accepts new third-party OAuth clients. The iRacing web UI (`members-ng.iracing.com`) reads the same `/data` endpoints through its own proxy (`/bff/pub/proxy/data/...`) using the signed-in user's session. AiSeasonCreator signs in through that web UI in an embedded browser and issues the same requests:

| Data | Endpoint |
| --- | --- |
| Seasons & schedules | `series/seasons?include_series=true` |
| Series details | `series/get` |
| Cars / car classes | `car/get`, `carclass/get` |
| Tracks | `track/get` |
| Logos | `car/assets`, `series/assets`, `track/assets` + `images-static.iracing.com` |
| Weather | each schedule's `weather_url` (fresh links via `series/season_schedule`) |

Data is refreshed at most every 12 hours (or on demand with **Update now**) and stored in `%LocalAppData%\AiSeasonCreator`. Logs are written to `%LocalAppData%\AiSeasonCreator\AiSeasonCreator.log`.

## Build

```bash
dotnet publish AiSeasonCreator/AiSeasonCreator.csproj -c Release -r win-x64 --self-contained true -o publish
```
