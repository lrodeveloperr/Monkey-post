# Building and installing Monkey Post

The game is made with **Godot 4.7** (C#), which is free and open source (MIT licence): no
account, licence, fees or revenue thresholds. Android APKs are built on GitHub Actions, so you
don't need to install anything. There's no setup and no secrets to add.

**Cost: £0.** GitHub gives private repositories 2,000 free Actions minutes a month. Builds are
**on demand only** (each takes about 5–10 minutes), so that allowance won't run out. If it ever
did, GitHub simply pauses builds until next month; with no payment method on the account,
nothing can be charged.

The APK is signed with a throwaway debug key, which is fine for testing on your own phone. Play
Store uploads (an `.aab` signed with the company's upload key) are added at the store milestone.

## Getting the APK

1. On GitHub, open the **Actions** tab → **Android build** → the latest run.
2. When it's green (about 5–10 minutes), download the **MonkeyPost-…** artifact at the bottom
   of the page. It's a zip containing `MonkeyPost.apk`.
3. Copy the APK to the phone (or download it there), open it, and allow "install unknown apps"
   when Android asks. Installs from Play will replace this later.

To build: **Actions → Android build → Run workflow** (or ask Claude to start one). Downloads are
kept for 7 days.

## Playing the prototype

- **Left thumb:** drag anywhere on the left half to move.
- **SPRINT:** hold.
- **PASS / SWITCH:** with the ball, passes in the joystick direction (double-tap for a through
  ball into space); without it, switches player.
- **Skill moves:** while dribbling, swipe on the right half (away from the buttons): forward =
  nutmeg, back = drag-back, sideways = step-over.
- **SHOOT / TACKLE:** with the ball, hold to charge and let go to shoot. Keep the bar in the
  **green**: red means it flies over knee height. Push the joystick up or down to aim at the
  far or near post. Without the ball, tap to tackle.
- **II:** pause.

On a computer (optional): WASD/arrows to move, Shift to sprint, Space for pass/switch
(double-tap for a through ball), K for shoot/tackle, 1/2/3/4 for nutmeg / drag-back / step-over
left / right.

## Running it on a computer (optional)

Install the **.NET 8 SDK** and **Godot 4.7.2 .NET** (<https://godotengine.org/download>; the
".NET" build), open `godot/project.godot` and press Play. The mouse works as a finger.
For a computer-v-computer match from the command line:

```sh
godot --path godot -- --demo=Hard
```

## Running the rules tests

```sh
dotnet test dotnet/MonkeyPost.Core.Tests
```
