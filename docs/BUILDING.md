# Building and installing Monkey Post

Every push builds an Android APK on GitHub Actions using the free **Unity Personal** licence.
You don't need to install the Unity editor. There's a one-off licence setup first.

## One-off: connect your Unity licence (about 10 minutes)

1. Create a free Unity account at <https://id.unity.com> if you don't have one. Use the company
   account; Unity Personal is free while revenue and funding stay under Unity's threshold.
2. Install **Unity Hub** (just the Hub, not the editor): <https://unity.com/download>.
3. Sign in to Unity Hub, then go to **Preferences → Licenses → Add → Get a free personal licence**.
   Click **Add** even if a licence already shows; that step creates the licence file.
4. Find the licence file:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`

   These folders may be hidden; turn on "show hidden files".
5. On GitHub, open the repository → **Settings → Secrets and variables → Actions → New repository
   secret**, and add three secrets:

   | Name | Value |
   |---|---|
   | `UNITY_LICENSE` | The entire contents of `Unity_lic.ulf` (open it in Notepad / TextEdit, copy all) |
   | `UNITY_EMAIL` | Your Unity account email |
   | `UNITY_PASSWORD` | Your Unity account password |

Until these exist, the "Android build" workflow skips the build with a warning instead of failing.

## Getting the APK

1. On GitHub, open the **Actions** tab → **Android build** → the latest run.
2. When it's green (the first build takes 30–60 minutes; later ones are faster thanks to
   caching), download the **MonkeyPost-…** artifact at the bottom of the page. It's a zip
   containing `MonkeyPost.apk`.
3. Copy the APK to the phone (or download it there), open it, and allow "install unknown apps"
   when Android asks. Installs from Play will replace this later.

To build without waiting for a push: **Actions → Android build → Run workflow**.

## Playing the prototype

- **Left thumb:** drag anywhere on the left half to move.
- **SPRINT:** hold.
- **PASS / SWITCH:** with the ball, passes in the joystick direction; without it, switches player.
- **SHOOT / TACKLE:** with the ball, hold to charge and let go to shoot. Keep the bar in the
  **green**: red means it flies over knee height. Push the joystick up or down to aim at the
  far or near post. Without the ball, tap to tackle.
- **II:** pause.

In the Unity editor (optional): WASD/arrows to move, Shift to sprint, Space for pass/switch,
K for shoot/tackle.

## Running the rules tests

```sh
dotnet test dotnet/MonkeyPost.Core.Tests
```
