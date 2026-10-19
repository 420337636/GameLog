# Privacy policy

GameLog is a local Windows desktop application. It does not collect, transmit or sell personal data.

## What is stored on your computer

All data created by GameLog stays on your machine, in `%APPDATA%\GameLog` (or `data\` in portable mode):

- the list of games you added (executable paths, display names, cover image files)
- play time statistics (total, per day, last played)
- application settings (sorting, auto-start, monitoring preferences)
- a local log file used for troubleshooting

Deleting that folder removes all GameLog data. Uninstalling the application does not require any online step.

## Network requests

GameLog only accesses the network when you add a game or ask it to re-identify one:

- it sends the game name / executable name to public search and game-information pages
  (Baidu, Baidu Baike, Gamersky) to find the game title and cover image;
- it downloads the cover image from those public pages and stores it locally.

These requests contain no account identifiers, no telemetry identifiers and no information about your play time. The cookies used during those requests are kept in memory by the HTTP client for the duration of the process; GameLog does not persist them.

The privacy policies of those third-party sites apply to the requests they receive.

## What GameLog does not do

- No analytics, telemetry, crash reporting or advertising SDKs.
- No user accounts, no cloud sync, no remote storage.
- No collection of file listings, process lists or hardware information beyond what is needed locally to detect whether a game you added is running.

## Contact

Questions about this policy: open an issue at
<https://github.com/420337636/GameLog/issues> or email `420337636@qq.com`.

Last updated: 2026-10-19
