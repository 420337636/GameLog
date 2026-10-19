# Code signing policy

This document describes how GameLog release binaries are built and signed. It applies to every artifact published on the project's GitHub Releases page.

## Project

- Name: **GameLog** (游戏时光)
- Repository: `https://github.com/<your-github-handle>/GameLog`
- License: MIT
- Maintainer: `<your name>` (`@<your-github-handle>`)

## Artifacts covered

Only the following files are signed:

- `GameLog.exe` — the WPF application, built from this repository
- future release binaries produced by the same workflow

No third-party binaries, installers obtained from other sources, or locally modified builds are signed.

## How builds are produced

1. The release is built by the public GitHub Actions workflow [`.github/workflows/build.yml`](.github/workflows/build.yml) on a GitHub-hosted `windows-latest` runner.
2. The build uses only source code from this repository and the C# compiler that ships with Windows (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`); there are no downloaded dependencies at build time.
3. The workflow uploads `dist/GameLog.exe` as a build artifact, and that exact artifact is submitted for signing.
4. Locally built binaries are **never** published as official releases.

## Who can sign

- Only the maintainer (`@<your-github-handle>`) can submit signing requests.
- Each signing request requires manual approval in SignPath; requests are not approved automatically.
- Multi-factor authentication is enabled on the GitHub account and is required for the SignPath account.
- Branch protection on `main` prevents force pushes and requires pull requests for changes to the build or signing configuration.

## Release process

1. Merge the release commit to `main`.
2. Tag the commit (`vX.Y.Z`).
3. GitHub Actions builds the binary from source.
4. The maintainer reviews the artifact hash and submits it to SignPath for signing.
5. The signed binary is attached to the GitHub Release with SHA-256 checksums.

## Key handling

- The code signing private key is generated and stored by SignPath Foundation in an HSM; the maintainer never has access to it.
- No signing keys, certificates or passwords are stored in this repository.

## Reporting a problem

If a published binary does not match this policy (unexpected signature, unknown artifact, tampered release), please open a security advisory as described in [SECURITY.md](SECURITY.md).
