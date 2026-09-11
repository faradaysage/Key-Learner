# Windows installer

GitHub Actions: `.github/workflows/windows-installer.yml`. Runs on main, pull requests, version tags, and manual dispatch. Download the **KeyLearner-Windows-installer** artifact from a successful run, unzip, and copy the setup EXE to the laptop. The package targets Windows 10/11 x64 and bundles the .NET runtime, fonts, icons, and 70 offline speech clips. Other words use Windows speech unless a parent adds recordings or optional Piper.

Build locally:

    ./scripts/build-installer.ps1 -Version 2.0.1 -Compiler 'path/to/ISCC.exe'

The workflow pins Inno Setup 7.1.0 and verifies its SHA-256 before installing the compiler. Ordinary runs use 2.0.<run number>; tags must be vMAJOR.MINOR.PATCH. Keep versions increasing for releases. Setup is currently unsigned, so Windows may show an unknown-publisher/SmartScreen prompt.

The permanent AppId is `{D5C654D0-1B14-4479-B771-20BD264731A8}`. Never change it to create a new version. Install location defaults to `%LOCALAPPDATA%\Programs\KeyLearner`, with the same Start menu shortcuts, taskbar identity, and uninstall entry across upgrades. Setup reuses the previous directory and can ask to close a running game. It never auto-launches protected play after installation.

Parent data remains in `%LOCALAPPDATA%\KeyLearner`, outside the application directory. Neither updates nor uninstall delete it. The private source CSV is explicitly excluded from publish. Add family words through the parent dictionary on the target laptop.

Run `scripts/test-installer.ps1 -Installer <setup.exe> -UpgradeInstaller <newer-setup.exe>` to exercise a temporary installation and upgrade, assert one registration/same directory/unchanged parent settings, then uninstall it. The test refuses to run if this AppId is already installed.

This installs the current session keyboard guard; it does not configure a Windows OS kiosk. See protection.md for its existing limits.
