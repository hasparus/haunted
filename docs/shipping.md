# Shipping to the App Store, Google Play and Steam

What's left, as of v0.4.2 plus the touch-controls branch. Labels:
- **Blocker**: the build won't work, or the store will reject it.
- **Required**: must be done before launch (accounts, forms, rights).
- **Nice**: worth doing.

Store rules checked in Nov 2026:
- Google Play has required target API 36 since 31 Aug 2026.
- Apple has required Xcode 26 and the iOS 26 SDK since 28 Apr 2026.

## Done on this branch

- Touch controls (`Scripts/TouchControls.cs`):
  - A floating stick, plus Grab, Haunt and Dash buttons whose labels change with what a press would do.
  - The dash button shows its cooldown, and there's a pause button.
  - On a phone the overlay appears from the start; on other devices, from the first touch.
- The UI stays clear of notches and the home indicator (`UI.SafeInsets`).
- Landscape only: portrait rotation is off in Player Settings.
- On iOS and Android the menu has no Quit or Fullscreen row; Apple rejects apps that quit themselves.
- Native mobile builds run at 60 fps; Unity's default there is 30.
- The leaderboard signature has a Sign button, because phone keyboards don't always send Enter.
- The web build's canvas turns off browser touch gestures, so it can be played in a phone's browser.

## All platforms

1. **Blocker: Identity.**
   - `companyName` is still `DefaultCompany`.
   - Every `applicationIdentifier` is the URP template's (`com.UnityTechnologies.com.unity.template.urpblank`). See `ProjectSettings.asset:15,172-176`.
   - Pick one reverse-DNS ID now. It can't change after the first upload.
2. **Blocker: Icons.** All 32 icon slots are empty. Make a 1024² master, plus the Android adaptive foreground and background layers.
3. **Required: Privacy policy.** Host it, and link it from the store pages and the menu. It must cover:
   - leaderboard name, score and IP (`HighScores.cs`, `server/scores`);
   - the Unity Authentication anonymous ID, and Relay/Lobby IPs (`NetBootstrap.cs`);
   - Unity engine diagnostics.
4. **Required: Leaderboard names are user-generated content.**
   - Apple 1.2 and Google's UGC policy want a filter, a way to report, a way to hide, and contact info.
   - The worker only strips symbols, and anyone can post to it.
   - Pick one:
     - a word filter plus report and admin-delete;
     - Game Center / Play Games / Steam leaderboards per platform;
     - generated names only.
   - Add rate limiting either way. Concurrent posts can also drop scores from the single KV key (`server/scores/src/index.ts:42`).
5. **Required: Rights.**
   - A written commercial licence or assignment from the artist for the demon art.
   - Confirm the game jam's rules allow selling the entry.
   - The eye photos are AI-generated (`Resources/Eyes/CREDITS.txt`). Steam needs an AI disclosure, and you can't claim exclusive rights to them.
6. **Required: Music.** `mus_menu.ogg` and `mus_level.ogg` are registered in YouTube Content ID (`Audio/CREDITS.txt:175,182`), so trailers and streamers will get claimed.
   - Replace them, or license them directly.
   - Re-download the clips that are Freesound low-quality previews.
7. **Required: Age ratings.** Answer for mental-health themes and prescription-drug references (the meds that flip your controls) in Apple's questionnaire, IARC on Play and Steam's content survey. Consider a one-line content note on the store pages.
8. **Required: Unity Gaming Services.**
   - Check Relay/Lobby quotas and billing for launch.
   - Native builds should keep one Authentication profile rather than a new one each launch, which inflates the player count (`NetBootstrap.cs:157`).
9. **Nice: Credits screen.** Kenney, the Pixabay artists, the OFL fonts and the artist; Pixabay asks for credit.
10. **Nice: Saves.** Move saves from PlayerPrefs to a file in `persistentDataPath`. On Windows, PlayerPrefs lives in the registry, which Steam Cloud can't sync.
11. **Nice: Packages.** Drop the prerelease `com.unity.ai.assistant` and experimental `com.unity.pipeline`, and the template leftovers (`Assets/TutorialInfo`, `Readme.asset`).
12. **Nice: Unity splash.** It's still on (`m_ShowUnitySplashScreen: 1`). Unity 6 Personal lets you turn it off.

## iOS

1. **Blocker: No iOS build anywhere.**
   - Add a game-ci `targetPlatform: iOS` job; it exports an Xcode project.
   - Then a `macos` job archives it and uploads to TestFlight, with fastlane or `xcodebuild` + `altool`. It must use Xcode 26.
   - Secrets: an App Store Connect API key, the distribution certificate and the provisioning profile.
2. **Blocker: Signing.** `appleDeveloperTeamID` is empty.
3. **Required: App Privacy label.**
   - Identifiers: User ID.
   - User content: leaderboard name.
   - Gameplay: score.
   - Diagnostics.
   - None of it is used for tracking, so there's no ATT prompt.
   - Set `ITSAppUsesNonExemptEncryption = NO`.
4. **Required: Screenshots.** iPhone 6.9" (2868×1320) and iPad 13" (2752×2064). The project targets iPhone + iPad; ship iPhone-only if you don't want to test iPad.
5. **Required: Business.** $99/yr Developer Program, the Paid Apps agreement, tax and bank details, EU DSA trader status and a support URL.
6. **Nice: Backgrounding.** There's no `OnApplicationPause`. Pause a local run when the app goes to the background.
7. **Nice: Performance.** Profile on an older iPhone; the mobile quality tier still has shadows on. Bump `buildNumber` from CI.

## Android

1. **Blocker: Target API.** `AndroidTargetSdkVersion: 0` means "whatever's installed". Set 36 and install that SDK in CI.
2. **Blocker: Signing.** There's no keystore, so builds are debug-signed.
   - Create an upload key and enrol in Play App Signing.
   - Pass game-ci's `androidKeystore*` secrets with `androidExportType: androidAppBundle`. Play takes AABs only.
   - Upload with `r0adkll/upload-google-play`.
3. **Required: Closed test.** New personal developer accounts need 12+ testers opted in for 14 days before production access. Budget about three weeks.
4. **Required: Play Console.**
   - $25 fee and identity verification.
   - Data Safety form (same data as Apple's label).
   - IARC questionnaire, ads declaration (none) and DSA status.
5. **Required: Listing assets.** Feature graphic 1024×500, icon 512², 2–8 screenshots.
6. **Nice: Back button.** Check that the system Back gesture opens the pause menu (it listens for Escape); predictive back is on. Bump `AndroidBundleVersionCode` from CI.
7. Already fine: IL2CPP, ARM64, min SDK 26.

## Steam

1. **Required: Upload.** CI only pushes to itch. Add `game-ci/steam-deploy` with one depot each for Windows, macOS and Linux (the paths the itch job already builds), plus a Steam Guard config secret.
2. **Required: macOS signing.** Codesign with Developer ID and notarize on a macOS runner. The Mono runtime needs the JIT entitlements. Without this, macOS warns players the app is from an unidentified developer.
3. **Required: Store assets.**
   - Capsules: header 920×430, small 462×174, main 1232×706, vertical 748×896, library 600×900.
   - Library hero 3840×1240 and logo.
   - 5+ screenshots at 1920×1080.
   - A trailer.
   - Today there are only itch covers.
4. **Required: Business and timeline.**
   - $100 Steam Direct fee, tax interview and bank details.
   - Release is possible 30 days after paying.
   - The Coming Soon page must be public 2+ weeks before launch.
   - Store-page review and build review take a few business days each.
5. **Required: Content survey.** The AI-generated eye photos, and the mental-health and meds themes.
6. **Nice: Steamworks** (Steamworks.NET or Facepunch). It brings:
   - Steam leaderboards on PC, which solves item 4 above there;
   - achievements;
   - the Steam Deck on-screen keyboard for the room code and signature;
   - friend invites.
7. **Nice: Steam Deck.**
   - Turn on vsync or cap the frame rate (the PC frame rate is uncapped).
   - Check the UI at 1280×800.
   - If SteamOS reports the Deck's screen as a touchscreen, the overlay appears on touch and hides again once the gamepad is used. Check this on a real Deck.
8. **Nice: Steam features.** Turn on Remote Play Together and tag the game as local co-op.

## Suggested order

1. Identity, icons, privacy policy, leaderboard moderation decision, rights check. All three stores depend on these.
2. Steam:
   - Pay the fee and put up a Coming Soon page early; it has the longest lead time and needs no new code.
   - Then add the SteamPipe job and macOS notarization.
3. Android:
   - Open the developer account and start the 14-day closed test as soon as a signed AAB exists.
   - Use the test to tune the touch controls on real phones.
4. iOS last: it needs the most new CI (a macOS runner) and the strictest review.
