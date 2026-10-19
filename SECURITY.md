# Security policy

## Scope

GameLog is a local Windows desktop utility. It has no server component, no telemetry and no account system.

- Game library and play time are stored locally in `%APPDATA%\GameLog` (or a portable `data\` folder).
- The only outbound requests happen when the user adds or re-identifies a game: public Baidu / Baidu Baike / Gamersky pages, used to look up the game title and cover image.
- No personal data is collected or uploaded.

## Supported versions

Only the latest release receives security fixes.

| Version | Supported |
| --- | --- |
| 1.0.x | yes |
| older | no |

## Reporting a vulnerability

Please **do not** open a public issue for security problems.

1. Open a private security advisory on GitHub:
   `https://github.com/<your-github-handle>/GameLog/security/advisories/new`
2. Or contact the maintainer by email: `<your email>`

Include: affected version, a description of the problem, reproduction steps and, if possible, a proof of concept.

You can expect an initial response within 7 days. Confirmed issues are fixed in a new release, and the reporter is credited unless they prefer otherwise.

## Verifying releases

- Builds are produced by the public GitHub Actions workflow from this repository.
- When SignPath signing is available, `GameLog.exe` carries a SignPath Foundation signature; verify it with:

  ```bat
  signtool verify /pa /v GameLog.exe
  ```

- Compare the SHA-256 of the downloaded file with the checksum in the release notes:

  ```powershell
  Get-FileHash .\GameLog.exe -Algorithm SHA256
  ```

## Out of scope

- Issues in the websites whose public pages are parsed
- Problems caused by modified copies of the binary
- Windows SmartScreen warnings for unsigned builds (see [README](README.md) and [docs/open-source.md](docs/open-source.md))
