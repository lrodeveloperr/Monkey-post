# Animation assets: what to download

The game's animation code is ready. It needs a free character model and free motion-capture
clips that can't be downloaded from the build environment, so they have to be fetched by hand.
Allow about 30 minutes in total. Until they're in, the game uses the placeholder footballers, so
nothing breaks in the meantime.

Everything goes into the repository on the branch `claude/blissful-gauss-zokc0u`. The easiest
way is GitHub's website: open the folder, then **Add file → Upload files**, drag the files in and
commit. The folders already exist.

> **Keep the repository private.** Mixamo's clips are free to use in a commercial game, but
> Adobe's terms don't allow redistributing the raw files. A public repository would do that.
> Kenney's model is CC0, so it's fine either way.

---

## 1. The character model: Kenney "Animated Characters 2" (CC0, free)

1. Go to <https://kenney.nl/assets/animated-characters-2> and download it. Donating is optional;
   choose the free option.
2. From the zip:
   - Upload `Model/characterMedium.fbx` to **`unity/Assets/Resources/Characters/`** and **rename
     it `Footballer.fbx`** (rename it before uploading).
   - Upload the **male** skin images from `Skins/` (the `.png` files) to
     **`unity/Assets/Resources/Characters/Skins/`**. The game gives each player a different one,
     for varied skin tones and hairstyles.

If the build log later says the model "is not a valid humanoid", there's a fallback: upload
`characterMedium.fbx` to Mixamo (**Upload Character**), let it auto-rig, then download it
(*FBX for Unity, With Skin*) and use that file as `Footballer.fbx` instead.

---

## 2. The clips: Mixamo (free, needs a free Adobe account)

Go to <https://www.mixamo.com>, sign in, and for each clip below:

1. Search for it. The search terms are suggestions: pick the closest-looking match.
2. Click **Download** with these settings:
   - Format: **FBX for Unity (.fbx)**
   - Skin: **Without Skin**
   - Frames per second: **30**
   - Keyframe reduction: **none**
   - If there's an **In Place** checkbox, **tick it**.
3. **Rename the file exactly as in the "File name" column** and upload it to
   **`unity/Assets/Resources/Animations/`**.

The file name is what matters: the game finds clips by name, and names starting with `Loop_`
loop automatically.

### Movement (the first three are required; the rest improve it)

| File name | Search Mixamo for | Notes |
|---|---|---|
| `Loop_Idle.fbx` | "Soccer Idle" (or any relaxed idle) | **required** |
| `Loop_Jog.fbx` | "Jogging" | **required**, In Place |
| `Loop_Sprint.fbx` | "Fast Run" or "Running" | **required**, In Place |
| `Loop_Backpedal.fbx` | "Running Backward" or "Walking Backwards" | In Place |
| `Loop_Shield.fbx` | a crouched, wide-stance idle, e.g. "Defensive Idle" | holding off a defender |
| `Loop_KeeperIdle.fbx` | "Goalkeeper Idle" | the keeper's ready stance |

### Reactions

| File name | Search Mixamo for | When it plays |
|---|---|---|
| `React_HeadInHands.fbx` | "Disappointed" or "Defeated" | after a miss, or the ball over knee height |
| `React_FistClench.fbx` | a short "Fist Pump" or "Cheering" | after winning a tackle |
| `React_Stumble.fbx` | "Stumble Backwards" or "Soccer Trip" | when tackled |
| `React_LookBack.fbx` | "Look Over Shoulder" or "Looking Around" | after being nutmegged |

### Goal celebrations (2 free + 6 for the shop)

| File name | Search Mixamo for | In the game |
|---|---|---|
| `Celebrate_FistPump.fbx` | "Fist Pump" | free |
| `Celebrate_RunOff.fbx` | "Victory" (arms raised) | free |
| `Celebrate_KneeSlide.fbx` | "Knee Slide" (or a kneeling victory) | shop |
| `Celebrate_Dance.fbx` | any short dance, e.g. "Hip Hop Dancing" | shop |
| `Celebrate_Salute.fbx` | "Salute" | shop |
| `Celebrate_Backflip.fbx` | "Backflip" | shop |
| `Celebrate_Shush.fbx` | "Shush" or "Quiet" | shop |
| `Celebrate_Bow.fbx` | "Bow" | shop |

You don't need kick or tackle clips: kicks, touches, tackles and the keeper's stretch are
animated by the physics, so the foot always meets the ball exactly where it really does.

---

## 3. Done

Once the files are pushed, the next APK build uses the real characters automatically. Tell
Claude, and it will check the build log for import problems and tune the clip speeds so the feet
don't slide.
