# Monkey Post — Design Document

Status: **v1 draft for owner review**
Platform: Android (Google Play). Available worldwide, focused on Africa.
Budget: **£0 running cost.** No servers, no databases, no paid services.

Monkey Post is Nigerian street football played to tiny goals, the "monkey posts". This game is
a fast 5-a-side version: 4 outfield players and a keeper who may only use their feet.

---

## 1. Decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Audience | **13+**, marked "not designed for children" in Play Console. This keeps the game outside Google Play's Families Policy. The art and tone should read as street football for teens and adults, not a toddler cartoon. |
| 2 | Money | Free. **No in-app purchases.** AdMob **non-personalised** interstitial and rewarded ads only. |
| 3 | Infrastructure | **Google Play tools only.** No backend of our own. |
| 4 | Match format | **5-a-side: 4 outfield + 1 feet-only keeper.** One 4-minute clock, no halves, no substitutes ("no bench"). Draws allowed. |
| 5 | Pitch | **Walled cage**: the ball never goes out of play. No throw-ins, corners or goal kicks. |
| 6 | Controls | A person controls **the whole team, with auto-switch** to the best-placed player (like FIFA / eFootball). Bots move their teammates. |
| 7 | Visual style | **2.5D, side-on broadcast camera** following the ball, played with the phone held sideways. **Stylised low-poly**, men's teams. Mobile URP. |
| 8 | Audio and languages | Generic crowd noise plus a referee's whistle. No music or chants. **English and French only.** Available worldwide so the diaspora can play, but focused on Africa (see §8). |
| 9 | Running cost | £0. The only unavoidable cost is Google Play's one-off developer registration fee, if not already paid. |
| 10 | Ownership | Company Play developer account (organisation account, which needs a free D-U-N-S number) and company AdMob account. |
| 11 | Build process | Claude builds, owner reviews every PR. |
| 12 | v1 scope | **Offline vs bots + local 1v1 via Nearby Connections** (one phone per team, same room, no internet). |
| 13 | Single-player | **Quick match + 8-team street cup.** The player picks the difficulty, chooses a country (which sets the opponent teams) and customises their own team. |

---

## 2. Game rules (v1)

**Status: confirmed by owner (Part 1).** Match length, draws, own-box ban, knee-height goals, no
fouls, computer-controlled keeper, 1.8 m goals and keeper staying in the box were all confirmed.

| Rule | Value | Why |
|---|---|---|
| Pitch | 36 m × 20 m, walled | Typical cage/futsal size. The ball always stays in play. |
| Goals | **1.8 m wide**, no crossbar, but the ball must be **below knee height (0.7 m)** | The monkey posts. Blasting it from range goes "over". |
| Teams | 5 a side: keeper, 2 defenders, 2 attackers | |
| Keeper | **Feet only**: can block and control with their feet, never catch. Must stay in their **box** (4 m deep, 7 m wide). | Your rule, plus a box so the keeper can't wander. |
| Own box | Outfield players **can't enter their own box** | Stops defenders parking in front of a tiny goal. Attackers may enter. |
| Fouls | None. Mistimed tackles leave the tackler briefly stunned. | Street rules, no referee stoppages. |
| Clock | **4 minutes**, one period, then the final whistle | Short mobile session. A draw is a draw, except in cup ties: **golden goal** (next goal wins). |
| Kick-off | At the start, and by the conceding team after each goal | |

**Controls and camera — status: confirmed by owner (Part 2).**

Layout: **virtual joystick** under the left thumb, **three buttons** under the right thumb.

| Button | With the ball | Without the ball |
|---|---|---|
| A | **Pass** to the teammate in the joystick direction | **Switch** to another teammate |
| B | **Shoot**: hold to charge (power bar), let go to shoot; joystick up or down aims across the goal | **Tackle** |
| Sprint | Hold to sprint (drains the stamina bar) | Same |

- Too much shot power from mid-range flies over knee height. The power bar marks the "safe" zone
  for the current distance.
- Control moves automatically to the ball carrier, or to whoever is closest to a loose or
  opponent's ball. Switch overrides it.
- The keeper is always computer-controlled and clears the ball quickly when they win it.
- **Camera:** side-on, **follows the ball** showing about half the pitch, with a slight zoom near
  goal.
- **Haptics:** light buzzes on goals, tackles and full-power shots, with an off switch in settings.

All numbers live in `MatchConfig` and will be tuned from playtests.

---

## 3. Single-player modes and bots

**Status: confirmed by owner (Part 3).**

### Modes
- **Quick match:** pick an opponent and a difficulty (Easy / Medium / Hard), then play one match.
- **Street cup:** 8-team knockout (quarter-final, semi-final, final). The player picks Easy, Medium
  or Hard for the whole cup, and opponents get slightly stronger each round within that level.
  Level cup ties go to **golden goal**. Winning a Hard cup is the headline achievement.
- **Pause:** available in single player, and automatic when the app is minimised. No pausing in
  local 1v1.

### Countries
The player chooses a **country**, which sets the **opponent teams**: fictional street teams named
after that country's cities and neighbourhoods. Menus stay in English or French.

| Country | Language suggested |
|---|---|
| Nigeria | English |
| Ghana | English |
| Kenya | English |
| Cameroon | English or French |
| Senegal | French |
| Côte d'Ivoire | French |

Players elsewhere (the diaspora, or anyone worldwide) pick any of these countries. More countries
can be added later as content-only updates (team names, colours, badges).

### Teams
- Opponents are **fictional street teams**, never real clubs (no licensing).
- The player's **own team**: name, kit colours and a badge from presets. Stored only on the phone.

### Bots

One `TeamBrain` per team plays every footballer that a person isn't controlling. Bots see the same
`MatchSnapshot` as the UI, with a slightly delayed picture of the ball, so easier teams react late.

| Behaviour | Easy | Medium | Hard |
|---|---|---|---|
| Reaction delay | 0.35 s | 0.2 s | 0.1 s |
| Decisions per second | ~2 | ~3 | ~7 |
| Tackling | hesitant | balanced | aggressive |
| Shooting range | 9 m | 11 m | 12 m |
| Aims away from the keeper | rarely | half the time | usually |
| Pass selection | often random | decent | spots the best ball |
| Keeper positioning | reactive | anticipates a little | anticipates |

The behaviour model is: the closest player presses the ball, the next closest covers between ball
and goal, and the rest hold a 1-2-2 shape that shifts with the ball. In possession, the ball carrier
dribbles round defenders, passes when pressed, and shoots when in range with an angle.

A person's AI teammates always play at Medium.

**Measured with bot-only matches** (30 matches each, run by the test suite):

| Match | Goals per 4-min match | Result |
|---|---|---|
| Easy v Easy | 2.9 | |
| Medium v Medium | 3.6 | |
| Hard v Hard | 5.2 | |
| Hard v Easy | 5.1 | Hard won 25, drew 3, lost 2 |

---

## 3a. Progression and unlockables

**Status: confirmed by owner (Part 5).**

- Matches earn **coins**, which players spend in a **shop**. Coins can never be bought: there are
  no purchases.
- **Unlockables:** kits and badges, balls, pitches, goal celebrations.
- **Pace: steady.** A common item every 2–3 matches, a pitch or celebration every 15–20.
- **Rewarded ad:** offered on the full-time screen to **double that match's coins**.
- **Daily first-win bonus:** the first win each day pays extra coins. No streaks to lose.
- **Cup-winner items:** exclusive items for winning each cup level (e.g. a gold ball for the Hard
  cup). They can't be bought with coins.
- **Local 1v1** pays a smaller amount, so two friends can't farm coins by letting each other win.
- Coins and unlocks are saved on the phone and in Play Games Saved Games.

**Starting economy (to be tuned from playtests):**

| Earning | Coins |
|---|---|
| Finishing a match | 20 |
| Each goal scored (max 5 counted) | 10 |
| Win / draw | 30 / 10 |
| Difficulty multiplier | Easy ×1, Medium ×1.5, Hard ×2 |
| Cup winner bonus | Easy 150, Medium 250, Hard 400 (plus that level's exclusive item) |
| Daily first win | +100 |
| Rewarded ad | doubles the match's coins (not the daily or cup bonus) |
| Local 1v1 match | flat 25, for up to 10 matches a day |

| Shop item | Price |
|---|---|
| Kit colours / pattern, badge, ball | 150–250 |
| Goal celebration | 1,200 |
| Pitch | 1,500 |

A Medium win with two goals pays (20 + 20 + 30) × 1.5 = 105 coins, so a common item takes about
2 matches and a pitch about 15. That matches the "steady" pace.

---

## 3b. Art and audio

**Status: confirmed by owner (Part 8).**

- **Style:** stylised low-poly. Chunky, colourful, slightly exaggerated footballers with flat
  shading. Readable side-on on a small screen and light on cheap phones.
- **Players:** men's teams.
- **Pitches:** one per country, all walled to match the cage rule. Any player can unlock any of
  them, whatever country they chose.

| Pitch | Country | How to get it |
|---|---|---|
| Lagos street cage | Nigeria | Free from the start |
| Accra beach | Ghana | Shop |
| Nairobi rooftop | Kenya | Shop |
| Dakar sand court | Senegal | Shop |
| Abidjan market court | Côte d'Ivoire | Shop |
| Douala school field | Cameroon | Shop |

- **Match audio:** generic crowd noise reacting to play (rising on attacks, roaring on goals), a
  referee's whistle (kick-off, goals, full time), ball kicks and wall thuds. No commentary, music
  or chants.
- **Menu audio:** ambient street sounds (distant traffic, a ball against a wall, faint crowd). No
  music.
- **Sourcing at £0:** free asset packs that need no credit (CC0, e.g. Kenney, Quaternius,
  freesound.org CC0 sounds), plus free Mixamo animations (royalty-free for games through a free
  Adobe account, though not CC0). I customise, recolour and assemble them, and build simple
  models (walls, goals, props) in code. Every asset's licence is recorded in `CREDITS.md`, so we
  can show none needs payment. Paid art can replace any of it later.

---

## 3c. Menus and first-time experience

**Status: confirmed by owner (Part 9).**

**First launch:**
1. **Country picker** (Nigeria, Ghana, Kenya, Cameroon, Senegal, Côte d'Ivoire). Shown in the
   phone's language (English or French).
2. **Guided first match** straight away: a short match against Easy bots with the default team,
   with on-screen hints that appear as each action becomes useful (move, sprint, pass, shoot and
   charge power, tackle, switch). It can be skipped.
3. **Full-time screen: "Name your team!"** Name, kit colours and badge.
4. Ad consent form (UMP), only for users in regions that require it, then the home screen.
   No ads are shown before this point anyway (no interstitials in the first 3 matches).

**Home screen:**
- One big **Play** button: a quick match at the last difficulty played.
- Tiles: **Street Cup**, **Local 1v1**, **Shop**, **My Team**.
- The player's team stands in its kit on the Lagos cage (or their chosen pitch) in the
  background. Coins are shown top right, settings top left.

---

## 3d. Settings and accessibility

**Status: confirmed by owner (Part 11).**

| Setting | Options |
|---|---|
| Language | English / French (defaults to the phone's language) |
| Country | Changes the opponent teams |
| Left-handed layout | Mirrors the controls: joystick right, buttons left |
| Control size and transparency | Sliders for the joystick and buttons |
| Colour-blind friendly kits | Forces strongly contrasting kits and shows a marker above your team's players |
| Battery saver | Lower graphics detail and a lower frame-rate cap |
| Sound | Separate sliders for crowd, sound effects (kicks, whistle) and menu ambience |
| Haptics | On / off |
| Play Games | Sign in to back up progress / sign out |
| Privacy | Ad consent options (UMP), privacy policy link |

---

## 4. Architecture

```
┌───────────────────────────── Android phone (Unity 6 app) ──────────────────────────────┐
│                                                                                         │
│  Presentation (Unity, 2.5D URP,    ◄── snapshots ──   MonkeyPost.Core (pure C#)        │
│  side-on camera)                                       • MatchSimulation (rules,        │
│  Touch controls / UI / Audio        ── inputs ──►        ball physics)                  │
│  Localization                                          • TeamBrain bots                 │
│                                                        • LocalMatch (auto-switch)       │
│                                                                                         │
│  Play services adapters:                                                                │
│    Play Games Services v2 ── sign-in, achievements, leaderboards, Saved Games           │
│    Nearby Connections ────── local 1v1 transport (no internet)                          │
│    AdMob + UMP ───────────── non-personalised ads + consent                             │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

### Core rules package (`core/`)
- Pure C#, **no Unity references**. It's a Unity package (UPM) and is also compiled and
  unit-tested with `dotnet test` in CI.
- Fixed 30 Hz tick, with ball physics substepped so hard shots can't pass through players.
  The ball has height for the 2.5D view and the knee-height goal rule.
- Seeded randomness, so a match replays identically from its seed (useful for bug reports).

### Local 1v1 (Nearby Connections)

**Status: confirmed by owner (Part 4).**

- **Two phones, one team each.** Each player brings **their own customised team** (name, kit,
  badge). If the kits clash, the away team switches to its second kit automatically.
- **Pairing:** one phone taps **Host**; the other taps **Join**, sees nearby hosts listed by team
  name, and taps one. Both phones then show the same 4-digit code to confirm they're connected to
  the right phone (Nearby Connections provides this code).
- **Host-authoritative:** only the host runs the simulation. The guest sends its controls (move,
  sprint, pass, shoot, tackle, switch) and receives snapshots about 15 times a second, rendering
  with interpolation. The guest's own movement is predicted locally to hide latency.
- **Fairness:** the host's own controls are delayed by the measured guest latency, so neither
  player has an advantage.
- **Draws:** a level 1v1 goes to **golden goal**.
- **Disconnects:** the match pauses for up to **10 seconds** while the phones try to reconnect.
  If they can't, a bot takes over the missing team and the remaining player finishes the match.
- **After the match:** a **Rematch** button. If both players tap it, a new match starts on the
  same connection; otherwise both go back to the menu.
- No pausing in 1v1.
- Works over Bluetooth / Wi-Fi with **no internet and no server**.
- Unity has no official Nearby plugin, so we ship a small Kotlin Android library (AAR) that
  wraps the Nearby Connections API and talks to Unity through `AndroidJavaProxy`.

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
| Leaderboards and achievements | Play Games Services (e.g. goals scored, wins vs Hard bots) | Own database |
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

## 5a. Google Play Games

**Status: confirmed by owner (Part 7).**

- **Sign-in:** automatic and fully optional. If the player already uses Play Games it signs in
  silently. Otherwise the game works fully without it, and settings has a "Sign in to back up
  progress" button.
- **Cloud save (Saved Games):** coins, unlocks, own team and stats. If the phone and cloud copies
  differ, the game **keeps the most progress** automatically: unlocked items are combined, and coins
  and stats come from whichever save has earned more in total. No prompts.
- **Leaderboards** (Play Games provides daily, weekly and all-time views automatically):
  total goals scored, cups won, Hard cups won, and longest win streak vs bots.
- **Achievements:** about 20 at launch (draft below, for review). Hidden ones are marked *(hidden)*.

| # | Achievement | How to get it |
|---|---|---|
| 1 | Full Time | Finish your first match |
| 2 | Off the Mark | Score your first goal |
| 3 | First Win | Win a match |
| 4 | Hat-trick | One player scores 3 goals in a match |
| 5 | Clean Sheet | Win without conceding |
| 6 | Thrashing | Win by 5 or more goals |
| 7 | Comeback | Win after being 2 goals down |
| 8 | Golden Goal | Win a match with a golden goal |
| 9 | Street Champions | Win the Easy cup |
| 10 | Local Legends | Win the Medium cup |
| 11 | Kings of the Street | Win the Hard cup |
| 12 | Giant Killer | Beat a Hard team in a quick match |
| 13 | Long Range | Score from your own half |
| 14 | Just Under | Score a shot that crosses the line just under knee height |
| 15 | Brick Wall | Win 5 tackles in one match |
| 16 | Neighbours | Play your first local 1v1 |
| 17 | Bragging Rights | Win a local 1v1 |
| 18 | One More! | Play 3 rematches in a row |
| 19 | Shopper | Buy your first shop item |
| 20 | Collector | Unlock every ball |
| 21 | Wrong End *(hidden)* | Score an own goal |
| 22 | Last Gasp *(hidden)* | Score in the final 5 seconds to win |

---

## 6. Ads

**Status: confirmed by owner (Part 6).**

- **Non-personalised on every request.** Max ad content rating: **PG**.
- **Interstitial:** on the full-time screen **after every 2nd match**, at most one every
  3 minutes. Never at launch or mid-match (Play policy).
- **New players:** no interstitials during their **first 3 matches**. Rewarded ads are still
  offered.
- **Rewarded:** optional, on the full-time screen: "Watch an ad to double your coins."
  Everything can be earned by playing, so ads are never required.
- **No banner ads** anywhere.
- **Local 1v1:** no interstitials between rematches. One may show when a player leaves 1v1 and
  goes back to the menu (subject to the rules above).
- **Offline:** the game is fully playable without internet. If no ad can load, play simply
  continues and the rewarded button is hidden.
- **UMP consent form** for users in the EEA, UK and Switzerland, plus US state privacy messages.
  This is still required with non-personalised ads, because ads use device storage for frequency
  capping and fraud prevention. If consent is refused, Google serves limited ads.
- **app-ads.txt** must be hosted on the developer website listed on the store page. A free
  GitHub Pages site works and can also host the privacy policy.

---

## 7. Privacy and compliance

Principle: **collect nothing ourselves.** The only data flows are Google's own (Play Games,
AdMob).

- No email, phone number, location data, contacts, free-text chat, analytics SDK or own server.
- Communication in local 1v1: preset emotes and quick phrases only (localised, no free text).
- Team names typed by the player stay on their phone (and in their own Saved Games) and are never
  shared with anyone else, except the opponent's phone in local 1v1.
- Nearby Connections needs Bluetooth / Wi-Fi / nearby-device permissions (and location
  permission on older Android versions, required for Bluetooth discovery). We **never read or
  store location**. Permissions are only requested when the player opens Local 1v1.
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

**Status: confirmed by owner (Parts 3 and 10).** **English and French only.** Together they cover Nigeria, Ghana, Kenya,
Cameroon, Senegal and Côte d'Ivoire, plus the diaspora.

- Plain English and French throughout, with no Pidgin or other local-language flavour.
- The language defaults to the phone's setting (French if the phone is in French, otherwise
  English) and can be changed in settings.
- UI is **icon-first** (football is universal) so there are few strings to translate.
- Unity Localization package. Both languages use Latin script, so no extra font packs are needed.
- Claude drafts the French. **A native French speaker the owner knows reviews the full string
  list before launch.**
- Store listing in English and French.

---

## 9. Performance targets

- Runs on 2 GB RAM Android Go phones. Android 7.0+ (subject to Unity 6 and Nearby minimums).
- 30 fps cap, URP mobile, baked lighting, under 5k triangles per footballer (10 on screen).
- Base download under 60 MB.
- Simulation tick 30 Hz. Local 1v1 snapshots 15 Hz, each a few hundred bytes (10 players + ball).

---

## 10. Repository layout

```
docs/DESIGN.md                 this document
core/                          MonkeyPost.Core Unity package (pure C#, no engine refs)
  Runtime/                     rules, ball physics, bots, snapshots, local match
dotnet/                        .NET projects that compile core/ for tests and CI
  MonkeyPost.Core.csproj
  MonkeyPost.Core.Tests/       xunit tests, including bot-vs-bot balance runs
unity/                         Unity 6 project (next milestone)
android/nearby-bridge/         Kotlin AAR wrapping Nearby Connections (milestone 3)
```

Run the tests with `dotnet test dotnet/MonkeyPost.Core.Tests`.

---

## 11. Milestones

1. **Rules engine + bots** (pure C#, unit-tested). ← this PR
2. Unity project: 2.5D cage pitch, side-on camera, footballers, touch controls, offline match vs
   bots, full-time screen.
3. Nearby Connections bridge + local 1v1 lobby (host / join).
4. Play Games Services: sign-in, achievements, leaderboards, Saved Games.
5. Coins and shop (kits, badges, balls, pitches, celebrations), cup-winner items, AdMob + UMP,
   interstitial and rewarded placements.
6. Localization, audio (crowd, whistle), polish on low-end devices.
7. Privacy policy, app-ads.txt, Data Safety form, store listing, closed test → production.

Note: new personal developer accounts must run a closed test with testers before production.
Organisation accounts are currently exempt, but check this in Play Console at that milestone.

---

## 12. Open questions for the owner

- Name and branding of the company account (for the store listing and privacy policy).
- Art direction for the footballers and pitch: e.g. a Lagos street cage, a dusty school field,
  a beach pitch. These could double as unlockable pitches.
- Whether to add an optional golden-goal decider for draws later.
