# KeyLearner on LeapPad Academy

These instructions target **LeapPad Academy Model 6022**, using the intended
Android 10 / API 29 ARMv7 configuration. Confirm the model on the tablet and the
Android version in its parent settings. Older Epic/Academy revisions may differ.

**Physical Model 6022 verification is pending. The PR remains a draft and must
not be merged or marked ready until the physical test result is reported.**
Use only the CI-produced APK identified by the handoff's workflow run, filename,
and SHA-256. Local development APKs are not the physical-device candidate.

KeyLearner installs as an APK, includes Blake narration, and runs offline after
installation. It does not require Google Play, an Amazon account, or a LeapFrog
Academy subscription.

## Download the exact CI candidate

1. Open the draft PR's **Checks** tab and the successful **Windows installer**
   workflow run identified in the handoff. Alternatively, open the repository's
   **Actions** tab and select that exact run/commit.
2. In the run's **Artifacts** section, download
   **KeyLearner-LeapPad-<version>**. GitHub may require you to sign in.
3. Extract the ZIP. Use **KeyLearner-LeapPad-<version>.apk** inside it. The same
   artifact includes this document, `build-report.json`, and the APK's
   `.inspection.json` with its SHA-256 and measured size.
4. Check the extracted APK against the handoff checksum. On Windows:

   ```powershell
   Get-FileHash -Algorithm SHA256 .\KeyLearner-LeapPad-<version>.apk
   ```

The APK checksum is different from GitHub's checksum for the artifact ZIP.
Use the APK checksum. The existing Windows installer is the separate
**INSTALL-KeyLearner-Unity-Windows-<version>** artifact from the same run.

## Install the candidate

1. Charge the tablet and enter its parent area using the parent-and-child icon
   and your Parent Lock PIN. LeapFrog documents this in its
   [Academy instruction manual](https://t7.leapfrog.com/images/8/80-602210_LeapPad_Academy_Instruction-Manual.pdf)
   and [App Manager guidance](https://leapfrog.happyfox.com/kb/article/11-3125/).
2. Transfer the supplied `KeyLearner-LeapPad-<version>.apk` to the tablet's
   Downloads folder using a USB data cable and the tablet's file-transfer mode.
   Alternatively, download the exact supplied APK link in the parent browser.
   Keep the filename and SHA-256 supplied with the candidate for verification.
3. Open Downloads or the parent file browser and tap that APK. If the installer
   asks to permit installation from this source, enable permission for that
   browser/file app in the parent Android settings, then return to the APK.
   Android 10 commonly calls this **Install unknown apps**; older LeapFrog
   instructions call it **Unknown sources**. Firmware labels must be confirmed
   during the physical test.
4. Tap **Install**, then **Open**. If these options are unavailable or the device
   blocks installation, report the exact message and firmware version. Do not
   reset the tablet or attempt to bypass the parent controls.
5. In LeapFrog Parent Settings → App Manager, find KeyLearner and use **Who Can
   Play** / child-profile permissions if offered. Return to the child's launcher
   and confirm KeyLearner is visible and launches there.
6. Turn off the temporary unknown-app installation permission after installation.

LeapFrog's official [Academy sideloading support page](https://www.leapfrog.com/en-us/support/products/leappad-academy/sideloading)
links an older Epic guide illustrating its parent-controlled APK installation
route. That guide installs Amazon Appstore; **KeyLearner uses its own APK and does
not need Amazon Appstore**. The older guide does not establish that every current
Model 6022 firmware exposes identical controls. Physical verification is required.

## Touch navigation to verify

Use the touchscreen throughout:

1. Launch KeyLearner from the child launcher and choose a game.
2. Play a round and listen for Blake. In Smash, tap the large learning target to
   complete a word one letter at a time.
3. Open **Menu**, then **Resume**. Confirm progress continues cleanly.
4. Open Menu → **Restart Current Game** and confirm the current activity restarts.
5. Choose **Another Game**, select a different game, and play without relaunching.
6. Open **Parent Options** and deliberately hold the parent-gate button for three
   seconds. Change the volume, save and return, then confirm it persists after a
   relaunch.
7. Use the parent area's **Exit KeyLearner** control, then launch again.
8. Check that the tablet stays responsive after repeated game switches and audio.
9. If Android Home/Back is exposed, reveal its bar with an edge swipe. Back should
   open the same game menu. Home should background the app; returning should
   present the paused activity. The child flow must still work without these keys.
10. Listen for intelligible, pleasant narration without missing letters, overlapping
    words, distortion, or long pauses. Report the tablet's app-storage size from
    its Android settings if available.

The physical test must confirm installation, launcher availability, readable UI,
touch gameplay, Blake audio, menu navigation, exit/relaunch and responsiveness.
Report the APK filename/checksum, tablet model, Android/firmware version, and any
failure messages. The PR must remain a draft, with merge blocked until this test result is reported.

## Updates and troubleshooting

Install a newer APK over the existing application to retain progress. Updates
must use the same package ID (`org.keylearner.app`) and signing certificate with
an appropriate version code. If Android reports a signature conflict, stop and
request a correctly signed update; uninstalling can erase local progress.

If narration is silent, check both the tablet media volume and KeyLearner's parent
sound/volume settings. If installation reports insufficient space, check the
reported final installed-size requirement and available tablet storage. If the
APK is incompatible, report the model, Android version and message rather than
installing an Intel emulator artifact.

Do not describe the build as **LeapPad device verified** until this exact candidate
passes on the physical Model 6022.
