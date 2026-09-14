# Unity build environment diagnostic

On 2026-09-12 at approximately 23:45–23:49 America/New_York, an optional new `scripts/build-unity.ps1` wrapper was rejected locally by antivirus/AMSI before execution. The first attempted write was a long PowerShell here-string containing discovery, prerequisite builds, and live/batch Editor orchestration. The wrapper file was not created by that rejected command.

A subsequent scoped, reviewed wrapper used a static local C# build driver instead of dynamically assembled C#. Invoking `./scripts/build-unity.ps1 -DiscoverOnly` was also rejected at line 1 (`param(`):

> This script contains malicious content and has been blocked by your antivirus software.

The invoking shell was `C:\Users\brian\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe`. The requested operation was local Unity Editor/CLI discovery, not a download, account action, or profile change. No product-specific detection event was obtained from the read-only Windows Defender event query for the matching 15-minute window; the exact antivirus detection name remains unidentified. Do not infer the product or detection cause from the generic PowerShell parser message.

The optional wrapper and driver were removed. Reproducible direct CLI/Editor commands and the existing independently exercised prerequisite scripts are documented in `UnityPort/README.md`. No antivirus exclusion, AMSI bypass, security-setting change, or retry with an alternate interpreter was used. This failure is distinct from the Codex filesystem sandbox helper's `SetNamedSecurityInfoW` access-denied failure documented in `PROJECT_HISTORY.md`.

A later read-only `Add-Type` desktop-input query was also rejected at PowerShell parsing with the same antivirus/AMSI message. It was not retried through an alternate execution mechanism. Visible player UX testing subsequently demonstrated an interactive desktop through actual Unity focus-loss/focus-gain events, so the diagnostic was unnecessary. Background helpers remain hidden; actual user-requested gameplay acceptance launches use a normal visible window. Starting the Unity player hidden produced black captures and could cause a Direct3D resolution-switch failure during restore.
