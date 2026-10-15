# Monkey Post — Design Document

Status: **v1 draft for owner review**
Platform: Android (Google Play), worldwide
Budget: **£0 running cost.** No servers, no databases, no paid services.

---

## 1. Decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Audience | **13+**, marked "not designed for children" in Play Console. This keeps the game outside Google Play's Families Policy. The art and tone should read as a party game, not a toddler cartoon, so Google doesn't classify it as appealing to children. |
| 2 | Money | Free. **No in-app purchases.** AdMob **non-personalised** interstitial and rewarded ads only. |
| 3 | Infrastructure | **Google Play tools only.** No backend of our own. |
| 4 | Match format | 6 players in a ring (5 seated, 1 runner). Bots fill empty slots. 3-minute clock, points-based, **no bench/elimination**. |
| 5 | Visual style | 2.5D: 3D characters, fixed angled camera, mobile URP. |
| 6 | Audio and languages | No song. Generic crowd noise plus a referee whistle. Launches worldwide (see §8 for languages). |
| 7 | Running cost | £0. The only unavoidable cost is Google Play's one-off developer registration fee, if not already paid. |
| 8 | Ownership | Company Play developer account (organisation account, which needs a free D-U-N-S number) and company AdMob account. |
| 9 | Build process | Claude builds, owner reviews every PR. |
| 10 | v1 scope | Offline vs bots + **local multiplayer via Nearby Connections**. Anything else only if a native Play capability covers it for free. |

---

## 2. Game rules (v1)

Based on the Nigerian playground game. The runner ("the Monkey") circles the ring holding the
post (handkerchief) and secretly drops it behind a seated player.

### Setup
- 6 players: 5 sit on 5 evenly spaced seats, 1 is the **Runner**.
- The runner always runs anticlockwise around the outside of the ring.
- Nobody (except the runner) can see whether the runner still holds the post.

### A round
1. **Circling.** The runner jogs or sprints around the ring and may **drop** the post behind the
   seated player they have just passed. If they haven't dropped it after **15 s**, it drops
   automatically behind the player they last passed.
2. **Check.** Any seated player may tap **Check** to look behind them.
   - Post is there → they grab it and become the **Chaser**. The chase starts.
   - Post isn't there → **false alarm**: −1 point (never below 0) and a 3 s cooldown before
     they can check again.
3. **Chase.** The chaser leaves their seat and runs anticlockwise after the runner. The chaser
   gets an adrenaline boost (×1.35 speed). The runner tries to complete the lap and sit in the
   chaser's empty seat.
   - Runner sits first → **Safe**: runner +3. The chaser becomes the new runner.
   - Chaser tags the runner → **Tagged**: chaser +3, chaser sits back down, runner stays runner.
4. **Posted!** If the runner completes a full lap after dropping and the target never noticed,
   the target is **Posted**: runner +5, target −2 (never below 0). Runner and target swap.
5. A 1.5 s interlude (whistle), then the next round starts. The clock keeps running.

### Movement
- Jog: 0.12 laps/s. Sprint: 0.20 laps/s. Nobody can stand still.
- Sprinting drains stamina, which comes back while jogging or seated.
- Sprinting right after passing someone is a natural "tell", and sprinting without dropping is a
  bluff. That's the core mind game.

### End
When the **3-minute clock** hits zero the match ends immediately. Highest score wins. There is no
elimination ("no bench"), so everyone plays the whole match.

All numbers live in `MatchConfig` and will be tuned from playtests.

---

## 3. Bots

Bots receive the same **public snapshot** a human would see. The simulation never gives them the
hidden post location, so they cannot cheat.

| Behaviour | Easy | Medium | Hard |
|---|---|---|---|
| Reaction time | slow | medium | fast |
| Check after runner passes | often (wastes points) | sometimes | selective |
| Reads the "sprint tell" | barely | yes | strongly |
| As runner: sprint right after drop (gives a tell) | always | sometimes | waits, then sprints |
| As runner: bluff sprints | never | sometimes | often |
| As runner: picks target | random | random | the leader |

Bots fill any empty slot in offline and local matches. In local matches they run on the host.

---

## 4. Architecture

```
┌───────────────────────────── Android device (Unity 6 app) ─────────────────────────────┐
│                                                                                         │
│  Presentation (Unity, 2.5D URP)   ◄── snapshots ──   MonkeyPost.Core (pure C#)         │
│  Input / UI / Audio / Localization  ── inputs ──►    • MatchSimulation (rules)          │
│                                                       • Bots                            │
│                                                       • Snapshots (hide secret info)    │
│                                                                                         │
│  Play services adapters:                                                                │
│    Play Games Services v2 ── sign-in, achievements, leaderboards, Saved Games           │
│    Nearby Connections ────── local multiplayer transport (no internet)                  │
│    AdMob + UMP ───────────── non-personalised ads + consent                             │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### Core rules package (`core/`)
- Pure C#, **no Unity references**. It's a Unity package (UPM) and is also compiled and
  unit-tested with `dotnet test` in CI.
- One rules engine drives offline play, local multiplayer (on the host) and the bots.

### Local multiplayer (Nearby Connections)
- Topology: **star**. One phone hosts (advertises), up to 5 phones join (discover). Bots fill
  the rest.
- **Host-authoritative:** only the host runs `MatchSimulation`. Guests send inputs (sprint, drop,
  check) and receive per-player snapshots about 15 times a second, rendering with interpolation.
- Per-player snapshots mean a guest's phone never receives the hidden post location, so a
  modified app can't reveal it.
- Works over Bluetooth / Wi-Fi with **no internet and no server**.
- Unity has no official Nearby plugin, so we ship a small Kotlin Android library (AAR) that
  wraps the Nearby Connections API and talks to Unity through `AndroidJavaProxy`.
- If the host leaves, the match ends and everyone keeps their XP. (Host migration is out of
  scope for v1.)

### No backend
Nothing runs on any server we operate. Player progress and unlocks live on the device and in Play
Games **Saved Games** (stored by Google, free).

---

## 5. Google Play tools used

| Need | Play tool | Replaces |
|---|---|---|
| Distribution, signing, small downloads | Play Console, Play App Signing, Play Asset Delivery | Own CDN |
| Device testing | Pre-launch reports (real devices, free) | Paid device farms |
| Crash and freeze reports | **Android vitals** | Crashlytics / any crash SDK |
| Identity | Play Games Services v2 (automatic sign-in) | Own accounts, email, phone login |
| Leaderboards and achievements | Play Games Services | Own database |
| Cloud save of unlocks | Play Games **Saved Games** | Own database |
| Local multiplayer | **Nearby Connections** (Google Play services) | Game servers |
| Ads and consent | AdMob + User Messaging Platform (UMP) | Third-party ad networks / consent tools |
| Store translations | Play Console listing translations | Paid translation for the listing |
| Staged rollout and reviews | Play Console tracks, in-app review API | — |

The game works fully if the player declines Play Games sign-in: progress then stays on the
device.

Not available from Play (accepted): online play with distant friends or strangers. Google
retired Play Games real-time multiplayer in 2020, and any replacement needs a server.

---

## 6. Ads

- **Non-personalised on every request.** Max ad content rating: **PG**.
- **UMP consent form** for users in the EEA, UK and Switzerland, plus US state privacy messages.
  This is still required with non-personalised ads, because ads use device storage for frequency
  capping and fraud prevention. If consent is refused, Google serves limited ads.
- **Interstitial:** only on the results screen, after every 2nd match, at most one every
  3 minutes. Never at launch or mid-match (Play policy).
- **Rewarded:** optional, player-initiated. "Watch an ad to unlock this outfit / post colour /
  ring location." Everything is also unlockable through play (XP), so ads are never required.
- In local matches each phone shows its own ads on its own results screen.
- **app-ads.txt** must be hosted on the developer website listed on the store page. A free
  GitHub Pages site works and can also host the privacy policy.

---

## 7. Privacy and compliance

Principle: **collect nothing ourselves.** The only data flows are Google's own (Play Games,
AdMob).

- No email, phone number, location data, contacts, free-text chat, analytics SDK or own server.
- Communication: preset emotes and quick phrases only (localised, no free text).
- Nearby Connections needs Bluetooth / Wi-Fi / nearby-device permissions (and location
  permission on older Android versions, required for Bluetooth discovery). We **never read or
  store location**. Permissions are only requested when the player opens Local Play.
- Match traffic between phones is processed in memory only and never stored.

**Play Data Safety form (draft):**
- Collected by our code: none.
- Collected by AdMob SDK: device or other IDs (advertising ID), app interactions, diagnostics.
  Purpose: advertising, fraud prevention. Shared with Google.
- Play Games Services data is handled by Google under its own terms.
- Data encrypted in transit: yes. Deletion: no account data held by us. Saved Games data can be
  deleted by the player in Play Games.

**Documents to publish (free, GitHub Pages):** privacy policy, app-ads.txt, support contact.

**Regulations covered by this design:** Nigeria NDPA 2023, GDPR / UK GDPR (through UMP and
non-personalised ads), US state privacy laws (through UMP), Google Play User Data and Ads
policies.

---

## 8. Localization

Launch set (about 20), chosen by Play reach plus home market:

English, Nigerian Pidgin, Yoruba, Igbo, Hausa, Swahili, French, Spanish, Portuguese (BR), German,
Italian, Russian, Turkish, Arabic (RTL), Hindi, Indonesian, Vietnamese, Thai, Japanese, Korean,
Chinese (Simplified).

- UI is **icon-first** so there are few strings to translate.
- Unity Localization package. Fonts for non-Latin scripts load as language packs through Play
  Asset Delivery so the base download stays small.
- Claude drafts translations. Nigerian languages and Pidgin get a native-speaker review before
  launch.
- More languages are added after launch based on where installs come from (Play Console
  statistics).

---

## 9. Performance targets

- Runs on 2 GB RAM Android Go phones. Android 7.0+ (subject to Unity 6 and Nearby minimums).
- 30 fps cap, URP mobile, baked lighting, under 10k triangles per character.
- Base download under 60 MB.
- Simulation tick 30 Hz. Local snapshots 15 Hz, each a few hundred bytes.

---

## 10. Repository layout

```
docs/DESIGN.md                 this document
core/                          MonkeyPost.Core Unity package (pure C#, no engine refs)
  Runtime/                     rules engine, bots, snapshots
dotnet/                        .NET projects that compile core/ for tests and CI
  MonkeyPost.Core.csproj
  MonkeyPost.Core.Tests/       xunit tests
unity/                         Unity 6 project (next milestone)
android/nearby-bridge/         Kotlin AAR wrapping Nearby Connections (milestone 3)
```

Run the tests with `dotnet test dotnet/MonkeyPost.Core.Tests`.

---

## 11. Milestones

1. **Rules engine + bots** (pure C#, unit-tested). ← this PR
2. Unity project: 2.5D ring, characters, input, offline match vs bots, results screen.
3. Nearby Connections bridge + local multiplayer lobby (host / join).
4. Play Games Services: sign-in, achievements, leaderboards, Saved Games.
5. AdMob + UMP, unlockables, interstitial and rewarded placements.
6. Localization, audio (crowd, whistle), polish on low-end devices.
7. Privacy policy, app-ads.txt, Data Safety form, store listing, closed test → production.

Note: new personal developer accounts must run a closed test with testers before production.
Organisation accounts are currently exempt, but check this in Play Console at that milestone.

---

## 12. Open questions for the owner

- Name and branding of the company account (for the store listing and privacy policy).
- Character art direction: humans, animals (monkeys?), or both.
- Whether runners should ever be allowed to run clockwise (a variant mode for later).
