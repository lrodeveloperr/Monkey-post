# Monkey Post — Design Document

Status: **v1 draft for owner review**
Platform: Android (Google Play). **Nigerian concept, English only.** Listed worldwide so Nigerians
abroad can play too.
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
| 7 | Visual style | **2.5D, side-on broadcast camera** following the ball, played with the phone held sideways. **Stylised low-poly**, men's teams. Godot Compatibility renderer (runs on older phones). |
| 8 | Audio and languages | Generic crowd noise plus a referee's whistle. No music or chants. **English only.** The concept is **Nigeria only for now**, but the app is listed worldwide so Nigerians abroad can play (see §8). |
| 9 | Running cost | £0. The only unavoidable cost is Google Play's one-off developer registration fee, if not already paid. |
| 10 | Ownership | **Worksbien Studios Inc.** Company Play developer account (organisation account, which needs a free D-U-N-S number) and company AdMob account. |
| 11 | Build process | Claude builds, owner reviews every PR. |
| 12 | v1 scope | **Offline vs bots + local 1v1 via Nearby Connections** (one phone per team, same room, no internet). |
| 13 | Single-player | **Quick match + 8-team street cup** against fictional Nigerian street teams. The player picks the difficulty and customises their own team. |

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

**Knee-height tuning:** shots get enough lift that the rule matters at every range. The safe maximum
is about 75–85% power from 4–16 m and rises at long range, where the ball has time to come down.
The power bar shows this safe zone in green, and bots judge it too (weaker bots blaze more over).

**Controls and camera — status: confirmed by owner (Part 2).**

Layout: **virtual joystick** under the left thumb, **three buttons** under the right thumb.

| Button | With the ball | Without the ball |
|---|---|---|
| A | **Pass** to the teammate in the joystick direction; **double-tap: through ball** into space ahead of a runner | **Switch** to another teammate |
| B | **Shoot**: hold to charge (power bar), let go to shoot; joystick up or down aims across the goal | **Tackle** |
| Sprint | Hold to sprint (drains the stamina bar) | Same |

- **Skill moves:** swipe anywhere else on the right half while dribbling (see §2c).
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

## 2b. Physics

**Status: confirmed by owner.** Goal: best-in-class football physics. Owner decisions: **free-ball
dribbling with a touch assist**, **automatic curl and spin from how you strike it**, **each pitch
surface plays differently**, and **realistic momentum tuned to stay responsive**.

**Calibrated against real-world measurements** (FIFA turf and ball test methods, wind-tunnel and
trajectory studies, sports-science sprint data). Every benchmark is enforced by a test. The full
reference, with sources and measured results, is in [PHYSICS.md](PHYSICS.md).

All of it lives in `core/` (pure C#, deterministic and unit-tested), not in the game engine's physics.
That keeps the host-runs-the-match model of local 1v1 exact on every phone. The match steps at
**60 Hz** and the ball at **240 Hz**, so even a 26 m/s shot can't pass through a leg or a post.

**The ball** (`BallPhysics`): a size 5 football, modelled as a hollow sphere.
- In the air: gravity, quadratic **air drag with a drag crisis** (hard-hit balls meet relatively
  less drag), **Magnus lift from spin** (curl, topspin dip, backspin float) and slow spin decay.
- On the ground: a struck ball **skids, then friction locks its spin to a true roll** (a hollow
  ball keeps 3/5 of its speed). Rolling resistance comes from the surface.
- **Bounces, walls, posts and bodies** use impulses with friction at the contact point, so spin
  and speed trade off: **backspin checks up, topspin kicks on**, and side-spin carries off the wall.
- **The monkey posts** are physical cylinders: shots can come back off the post.

**Surfaces** (`PitchSurface`):

| Pitch | Roll | Bounce | Character |
|---|---|---|---|
| Lagos street cage (turf) | fast, true | medium | the reference pitch |
| Ibadan school field (dusty earth) | slow | dead | bumpy |
| Abuja estate court (asphalt) | quick | lively | |
| Port Harcourt waterfront (concrete) | fastest | highest | |
| Kano sand court | dies quickly | barely bounces | a little bumpy |
| Enugu hillside (natural grass) | medium | medium | slightly uneven |

**Kicking:**
- Shots are planned with the real physics. **Aim towards a post and a softer shot curls in**, with
  the launch direction corrected so the curl brings it on target. **Power shots get topspin and
  dip.** Too much power still flies over knee height; the power bar's green zone comes from the
  same physics, so it's always right.
- **Accuracy:** harder is wilder, and a shot struck **off balance** (facing away) is weaker and wider.
- **Passes** are weighted to arrive already rolling at a controllable pace (about 5 m/s), leading
  a running teammate. Keepers clear firmly.
- **Kick windows:** press pass or shoot as the ball is arriving and the player steps up and
  strikes it first time (0.4 s window).

**Dribbling and control:**
- The ball **really leaves the foot on every touch**. Jogging keeps it close; **sprinting pushes it
  further ahead**, so it's quicker but easier to nick. The **touch assist** gently steers the
  ball towards the ideal spot ahead of the carrier (adjustable in config).
- **First touch:** balls up to about 6 m/s are killed cleanly; faster or bouncing balls can run
  away, and fast balls can only be killed close to the body. **Above 15 m/s the ball deflects off
  the player instead.**
- **Tackles:** timing matters. A ball between touches (away from the carrier's feet) is easier to
  win; a missed tackle leaves the tackler stunned.

**Players** (`PlayerMotion`):
- The **sports-science sprint model**: initial acceleration about 7 m/s², top speed 8 m/s (street
  level, below professionals' ~9.3), braking up to 6.5 m/s². A **friction-circle grip limit**
  makes sprinters curve on realistic 7–11 m radii, and a sharp change of direction at speed means
  **planting and braking first**. A stamina bar lasts about 6.5 s of flat-out sprinting.
- Players have **bodies**: they shoulder each other with real momentum, and the ball deflects off
  them.
- **The feet-only keeper** stretches a leg to block shots, comes off the line to narrow the angle,
  always takes the first touch away from goal, and clears firmly.
- **Goal credit:** a shot that deflects in off a defender or keeper is the shooter's goal.

**Verified by tests** (62 in total, 15 of them real-world benchmarks; see PHYSICS.md), including: drag shortens a lofted ball; sidespin curls the
correct way by 0.5–5 m; topspin lands short and backspin long; skid-to-roll keeps 3/5 speed;
backspin checks up and topspin kicks on the bounce; concrete rolls and bounces furthest and sand
least; planned curling shots arrive within 15 cm of aim; passes arrive at about 5 m/s; dribbled
balls leave the foot but stay under control; sprinting pushes the ball further; fast balls deflect;
slow balls are controlled; posts rebound; sprinting players plant before reversing.

---

## 2c. Skills, through balls, shielding and team strength

**Status: confirmed by owner.**

**Skill moves:** a small set, each a swipe on the right half while dribbling, relative to the
direction you're running:

| Swipe | Move | When it works |
|---|---|---|
| Forward | **Nutmeg**: knock it through the defender's legs and spin round him | Only if his **legs are open**: he's mid-stride (running at more than 2.5 m/s), lunging into a tackle, stumbling or stretching. Into a planted defender, it hits his legs. |
| Back | **Drag-back**: sole on the ball, roll it back and turn away | A much quicker turn than planting and turning, for escaping pressure. |
| Sideways | **Step-over**: fake that way, go the other | Wrong-foots a defender who is facing you within 2.5 m: he shifts his weight towards the fake and can't tackle for half a second. |

- **Risk is about timing:** a badly timed move loses the ball. There's a 0.8 s cooldown between
  moves.
- **Reward:** a move counts as a success when it beats its defender and the team keeps the ball.
  Success brings a "NUTMEG!" or "SKILL!" banner, a crowd roar and **bonus coins**. Getting
  nutmegged shows "NUTMEGGED!". Nutmegs have **no special scoring rule**.

**Through balls:** double-tap PASS. The ball is played into the space ahead of the teammate in the
joystick direction, **timed so ball and runner arrive together** (the rolling physics is solved
for the launch speed). A single tap waits 0.18 s for a possible second tap before passing.

**Shielding (automatic):** a slow carrier (under 3 m/s, not sprinting) with a defender within
1.6 m turns his back to the defender and keeps the ball on the far side. He gives far less ground
in body contact, and tackles from behind succeed less than half as often. Sprinting cancels it.

**Team strength:** a person's own team never has stats (skill decides, fair in 1v1, no
pay-to-win). Computer teams get small physical edges on top of their skill level:

| Level | Pace | Tackling | Touch |
|---|---|---|---|
| Easy | −3% | −10% | −10% |
| Medium | — | — | — |
| Hard | +3% | +10% | +10% |
| Each street-cup round | +0.5% | +2% | +2% |

**Computer teammates and opponents:** attackers **make runs in behind automatically** when the
ball carrier is facing forward. Carriers **play through balls** to runners with space, take on
close defenders with **skill moves** (Easy rarely, Hard often), and the intended receiver of a pass
runs onto it. Teams **push up when losing late** and **sit deeper when winning late**.

Measured over 30 matches each (Hard v Hard): about 20 skill moves a match, nearly half of them
beating the defender. Easy teams try about 1.5 a match.

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

### Setting: Nigeria only (for now)
Owner decision: the concept is **Naija only** for now. There is no country choice. All opponents
are fictional street teams from Nigerian cities and neighbourhoods (Lagos, Ibadan, Abuja, Port
Harcourt, Kano, Enugu and more), and all pitches are Nigerian. Other countries can be added later
as content-only updates (team names, colours, badges, pitches), since nothing in the code depends
on the country.

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
| Easy v Easy | 2.4 | |
| Medium v Medium | 3.1 | |
| Hard v Hard | 4.7 | |
| Hard v Easy | 4.0 | Hard won 25, drew 3, lost 2 |

---|---|---|
| Easy v Easy | 2.2 | |
| Medium v Medium | 2.5 | |
| Hard v Hard | 3.0 | |
| Hard v Easy | 3.6 | Hard won 20, drew 9, lost 1 |

---|---|---|
| Easy v Easy | 2.2 | |
| Medium v Medium | 3.3 | |
| Hard v Hard | 7.6 | |
| Hard v Easy | 3.7 | Hard won 24, drew 5, lost 1 |

---

## 2d. Animation

**Status: confirmed by owner.**

- **Hybrid:** recorded clips (Mixamo motion capture) for running, idling, reactions and
  celebrations, blended by speed; a **procedural layer driven by the physics** on top. Two-bone
  leg IK puts the foot exactly where the simulation met the ball (kicks, touches, tackles, the
  keeper's stretch), the body leans into acceleration, and the head follows the ball.
- **Kicks:** instant contact with a quick follow-through, no wind-up delay, so it stays
  responsive on a touchscreen.
- **Model:** a free CC0 low-poly character (Kenney Animated Characters 2), rigged with Mixamo's
  free auto-rigger so it shares the clips' skeleton. One body with varied skins (skin tones and
  hairstyles). **Teams wear coloured street-football bibs**, numbered on the back.
- **Reactions:** head in hands after a miss or a shot over the posts, fist clench after winning a
  tackle, stumble when tackled, a look back when nutmegged.
- **Celebrations:** 2 free (Fist Pump, Arms Out) and 6 in the shop (Knee Slide, Street Dance,
  Salute, Backflip, Shush the Crowd, Take a Bow).
- **Assets:** the owner downloads the model and about 18 clips (list and settings in
  [ANIMATION_ASSETS.md](ANIMATION_ASSETS.md)). Godot imports them automatically (FBX, in place,
  looping by name). Until they're in, the primitive placeholder footballers are used, with the
  same kicks, reactions and celebrations done procedurally.

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
| Skill move that beats a defender / nutmeg | +5 / +10 (max 50 a match) |
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
- **Pitches:** six Nigerian pitches, all walled to match the cage rule. (Updated for the
  Nigeria-only concept; the city choices are Claude's suggestion, open to change.)

| Pitch | City | How to get it |
|---|---|---|
| Street cage | Lagos | Free from the start |
| School field | Ibadan | Shop |
| Estate court | Abuja | Shop |
| Waterfront court | Port Harcourt | Shop |
| Sand court | Kano | Shop |
| Hillside court | Enugu | Shop |

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
1. **Guided first match** straight away: a short match against Easy bots with the default team,
   with on-screen hints that appear as each action becomes useful (move, sprint, pass, shoot and
   charge power, tackle, switch). It can be skipped.
2. **Full-time screen: "Name your team!"** Name, kit colours and badge.
3. Ad consent form (UMP), only for users in regions that require it, then the home screen.
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
| Left-handed layout | Mirrors the controls: joystick right, buttons left |
| Control size and transparency | Sliders for the joystick and buttons |
| Colour-blind friendly kits | Forces strongly contrasting kits and shows a marker above your team's players |
| Battery saver | Lower graphics detail and a lower frame-rate cap |
| Sound | Separate sliders for crowd, sound effects (kicks, whistle) and menu ambience |
| Haptics | On / off |
| Play Games | Sign in to back up progress / sign out |
| Privacy | Ad consent options (UMP), privacy policy link |

---

## 3e. Branding, store listing and legal

**Status: confirmed by owner (Part 12).**

| Item | Value |
|---|---|
| Publisher (Play developer name) | **Worksbien Studios Inc.** |
| App name on Play | **Monkey Post: Street Football** |
| Store listing language | English |
| Support email (public on the store) | **info@worksbienstudios.com** |
| Website | The company's existing website, `worksbienstudios.com` (confirmed) |
| Privacy policy | A page on the company website, e.g. `worksbienstudios.com/monkeypost/privacy` |
| AdMob | **Handled by Worksbien Studios**: AdMob account, app and ad unit IDs, payments, app-ads.txt on the company domain, and the consent (UMP) messages in AdMob's Privacy & messaging. The app only needs the IDs. |

Claude drafts the privacy policy (English). The company adds it to its website.

---

## 4. Architecture

```
┌───────────────────────────── Android phone (Godot 4 app) ──────────────────────────────┐
│                                                                                         │
│  Presentation (Godot 4 C#, 2.5D,   ◄── snapshots ──   MonkeyPost.Core (pure C#)        │
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
- Pure C#, **no engine references**. The Godot project compiles the same source files, and
  they're unit-tested with `dotnet test` in CI.
- Fixed 60 Hz tick with the ball substepped at 240 Hz (see §2b).
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
- Godot has no official Nearby plugin, so we ship a small Kotlin Godot Android plugin (AAR)
  that wraps the Nearby Connections API and talks to the game through signals.

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
- **Achievements:** about 24 at launch (draft below, for review). Hidden ones are marked *(hidden)*.

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
| 23 | Nutmeg King | Nutmeg 10 defenders |
| 24 | Showboat | Beat 3 defenders with skill moves in one match |
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
- **AdMob setup is handled by Worksbien Studios** (account, ad units, app-ads.txt, consent
  messages). The app integrates the Google Mobile Ads and UMP SDKs and reads the ad unit IDs
  from config.

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

**Documents to publish (company website):** privacy policy and support contact. app-ads.txt is
handled by the company with its AdMob setup.

**Regulations covered by this design:** Nigeria NDPA 2023, GDPR / UK GDPR (through UMP and
non-personalised ads), US state privacy laws (through UMP), Google Play User Data and Ads
policies.

---

## 8. Localization

**Status: confirmed by owner.** **English only** (Nigeria-only concept).

- Plain English throughout, with no Pidgin or other local-language flavour.
- All text still lives in one string table rather than being hard-coded, so adding a language
  later is a content job, not a code change.
- Store listing in English.

---

## 9. Performance targets

- Runs on 2 GB RAM Android Go phones. **Android 8.0+** (owner decision), 64-bit and 32-bit ARM.
- **Test device:** a cheap/older phone (e.g. a 2–3 GB Tecno, Infinix or Itel). If it's smooth there,
  it's smooth everywhere.
- 30 fps cap, Godot Compatibility renderer, simple lighting, under 5k triangles per footballer (10 on screen).
- Base download under 60 MB.
- Simulation tick 60 Hz (ball 240 Hz). Rendering capped at 30 fps. Local 1v1 snapshots 15 Hz,
  each a few hundred bytes (10 players + ball).

---

## 10. Repository layout

```
docs/DESIGN.md                 this document
core/                          MonkeyPost.Core (pure C#, no engine refs)
  Runtime/                     rules, ball physics, bots, snapshots, local match
dotnet/                        .NET projects that compile core/ for tests and CI
  MonkeyPost.Core.csproj
  MonkeyPost.Core.Tests/       xunit tests, including bot-vs-bot balance runs
godot/                         Godot 4.7 C# project; builds the scene from code
  assets/                      footballer model, skins and Mixamo clips (owner-supplied)
android/nearby-bridge/         Kotlin AAR wrapping Nearby Connections (milestone 3)
```

Run the tests with `dotnet test dotnet/MonkeyPost.Core.Tests`.

**Engine (owner decision):** Godot 4 with C#, free and open source, so there are no licence
fees or revenue thresholds. (Unity was used first and dropped to keep costs at £0.)

**Builds (owner decision):** APKs are built on GitHub Actions on demand (Godot's free export
templates, no secrets). The owner downloads it from the Actions tab and installs it on the test
phone. No local install is needed. Steps: [BUILDING.md](BUILDING.md).

---

## 11. Milestones

1. **Rules engine + bots** (pure C#, unit-tested). ✓
2. Godot project: 2.5D cage pitch, side-on camera, footballers, touch controls, offline match vs
   bots, full-time screen. ← **playable prototype with placeholder art built from primitives**;
   the CC0 low-poly art and remaining screens follow.
3. Nearby Connections bridge + local 1v1 lobby (host / join).
4. Play Games Services: sign-in, achievements, leaderboards, Saved Games.
5. Coins and shop (kits, badges, balls, pitches, celebrations), cup-winner items, AdMob + UMP,
   interstitial and rewarded placements.
6. Audio (crowd, whistle), polish on low-end devices.
7. Privacy policy, Data Safety form, store listing, closed test → production.

Note: new personal developer accounts must run a closed test with testers before production.
Organisation accounts are currently exempt, but check this in Play Console at that milestone.

---

## 12. Open items for the owner

All 12 decision areas are confirmed, and so are the website (`worksbienstudios.com`) and the
company's D-U-N-S number.

- AdMob (account, ad unit IDs, app-ads.txt, consent messages): handled by Worksbien Studios. Send
  the ad unit IDs when they exist; test IDs are used until then.
