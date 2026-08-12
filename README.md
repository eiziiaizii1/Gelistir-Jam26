# ICE AS HELL

> **SLIDE. JUMP. SURVIVE THE INFERNO!**

You are a block of ice loose on a hellish downhill slope. Everything down there wants to melt
you — lava rivers, falling meteors, molten pillars — and the clock is simply how much of you is
left. Steer with slaps, jump the hazards, ride the boost pads, and reach the end before the last
of you runs down the mountain.

Made with Unity 6 (URP) for **Geliştir Jam '26**.

---

## Gameplay

The block never stops accelerating downhill — gravity and a constant slide acceleration handle
the descent, and you never brake. All you control is *where* it goes.

- **Melting is the timer.** The ice shrinks and gets lighter as it melts, and lighter ice reacts
  harder to every slap. At 0% you're done.
- **Hazards melt you faster.** Lava rivers, lava columns, meteors, pillars and tentacles each
  take a bite out of the ice bar.
- **Speed pads and ramps pay you back.** Boost pads add raw impulse; ramps are real physics
  wedges that launch you into a flight, with a pop at the lip.
- **The run ends at the FinishZone** — a huge transparent trigger volume at the end of the
  slope. Cross it and you escape to the outro.

### Controls

| Input | Action |
| --- | --- |
| **Hold Left Mouse + swipe left/right** | Slap the block sideways — this is your steering |
| **Hold Left Mouse + push mouse forward** | Forward dash |
| **Right Mouse (hold / release)** | Jump — releasing early cuts the jump short |

Steering is gesture-based, not held: one press produces one impulse, and releasing the button
re-arms the next slap. Slap strength scales with how fast you actually swipe. The two mouse axes
are strictly isolated, so a sideways correction can never accidentally fire the dash.

---

## Scene flow

```
Menu ──► intro anim ──► Game ──► Outro
 ▲                                 │
 └───────── Return to Menu ────────┘
```

| Scene | Role |
| --- | --- |
| `Menu` | Title screen — Start Run, Settings (audio/quality), Quit |
| `intro anim` | Intro video playback, auto-advances to gameplay |
| `Game` | The run itself |
| `Outro` | "Escaped from hell" success screen with Return to Menu |

All four are registered in Build Settings in that order.

---

## Systems

### Player

| Script | What it does |
| --- | --- |
| `IceSlideController` | Physics slide, slap steering, dash, jump, coyote time and jump buffering. Descent and cross-slope drift are capped independently, so steering never trades away speed. |
| `IceMelt` | The melt clock. Implements `IMeltSource`, shrinks the visual and lowers mass as the ice goes. |
| `SlapHand` | The giant hand that swats on a horizontal swipe. Purely cosmetic — it never touches the Rigidbody. |
| `IceSquashAndStretch`, `IceTrail` | Impact squash and the wet trail left on the slope. |

### World and hazards

| Script | What it does |
| --- | --- |
| `ProceduralTrackLoop` | Recycles 300-unit track segments end-to-end and repopulates them with obstacles, boost pads and lava rivers, so the slope never runs out. |
| `ObstacleHazard` / `HellObstacle` | Impact and continuous-contact melt damage. |
| `LavaHazard`, `LavaStreamRiver` | Melt-over-time zones; the river also drags your speed down. |
| `FallingObstacle`, `MeteorRainManager` | Meteor rain from above. |
| `RampBoost`, `SpeedBoostPad` | Launch wedges and neon speed pads. |
| `HellEnvironmentManager` | Embers, ash, warm fog and light tuning for the inferno look. |

### Camera

| Script | What it does |
| --- | --- |
| `SlideCameraDirector` | Drives a Cinemachine camera off the player's motion — FOV widens with speed, plus framing and shake. |
| `SlideFollowCamera`, `CameraFollow`, `CameraJuice` | Alternative follow rigs used by different scenes. |
| `SlideImpactShake` | Impulse-based shake on hits, via Cinemachine Impulse. |
| `PlayerLocator` | Single answer to "where is the player" and "how do I shake the camera", regardless of which rig is active. |

### UI and flow

| Script | What it does |
| --- | --- |
| `IceGameHUD` | Builds the whole in-game HUD at runtime — speed, distance, melt bar — plus the game-over and victory panels. Cuts player input the moment the run ends. |
| `ScoreboardManager` | Arcade trick popups and rush score. Disable the GameObject and all scoring disappears. |
| `MainMenuManager` | Menu buttons, audio and quality settings. |
| `IntroVideoPlayer` | Plays the intro video and advances to gameplay. |
| `FinishLineTrigger` | The end-point volume. Fires once, for the player only, and loads the outro. |
| `FinishLinePlacer` | Positions the finish volume along the slope — set the run length from the inspector. |
| `OutroScreen` | Success screen; returns to the menu. |
| `IceAudioManager` | Singleton audio playback. |

---

## Getting started

**Requires Unity `6000.0.81f1`** (Unity 6). Other 6000.x versions will likely work but are
untested.

```bash
git clone https://github.com/eiziiaizii1/Gelistir-Jam26.git
```

Open the folder with Unity Hub, then open `Assets/Scenes/Menu.unity` and press Play. To skip
straight to gameplay, open `Assets/Scenes/Game.unity` instead.

`Library/`, `Temp/`, `Build/`, `Logs/`, `UserSettings/` and `Recordings/` are gitignored — Unity
regenerates them on first open, so the initial import takes a few minutes.

### Key packages

Universal RP 17.0.4 · Cinemachine 3.1.7 · Input System 1.19.0 · Timeline · Splines · Recorder ·
[MCP for Unity](https://github.com/CoplayDev/unity-mcp) (editor tooling, not shipped in builds)

Input goes through the **Input System** package — the UI uses `InputSystemUIInputModule`, so any
new UI scene needs an EventSystem with that module rather than the legacy one.

---

## Tuning

Most of the feel lives in serialized fields with tooltips; the inspector is the intended place to
tune, not the code.

- **Run length** — `FinishLinePlacer` on the `ProceduralTrackLoop` object in `Game`. With
  *Measure From Player Start* on, `Finish Distance` is the literal distance the player travels.
  It runs in edit mode, so the finish volume moves in the Scene view as you drag the value.
- **Difficulty** — `IceMelt.meltRatePerSecond` sets the base clock; individual hazards carry
  their own melt damage.
- **Steering feel** — `IceSlideController`'s slap impulse, speed threshold and reference speed.
- **Track density** — `ProceduralTrackLoop`'s obstacles-per-segment range and segment length.

> One known gap: `ProceduralTrackLoop`'s obstacle prefab fallback is editor-only
> (`AssetDatabase`), so **Obstacle Prefabs must be assigned in the inspector** or recycled
> segments ship empty in a build.

---

## Credits

Built by **Aziz Önder**, **Ozan Arda Buğa**, **Kenan İçöz** and **Mert Kaya** for Geliştir Jam '26.
Artist

Third-party assets: AllSky Free (skyboxes), Manufacturing Consent (font).
