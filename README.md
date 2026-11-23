# RESONANCE — Act I (XREAL Air 2 Ultra)

## Get an installable APK (no computer needed after one-time setup)
Every push to `main` runs **GitHub Actions**:
1. It downloads the XREAL SDK 3.1.0 straight from XREAL. The SDK isn't stored in this repo.
2. It runs the Unity **EditMode tests and the full-game PlayMode test**. Results show as the "Resonance test results" check.
3. It builds a **debug-signed Android APK** and publishes it under **Releases** as `Resonance.apk`.

On the Beam Pro: open the repo's **Releases** page in the browser, download `Resonance.apk`, allow installing unknown apps, then open it. Run **SYSTEM CHECK** from the title menu first.

**One-time setup:** add 3 repository secrets under Settings > Secrets and variables > Actions:
| Secret | Value |
|---|---|
| `UNITY_EMAIL` | your Unity account email |
| `UNITY_PASSWORD` | your Unity account password |
| `UNITY_LICENSE` | the full text of your Unity license file (`Unity_lic.ulf`) |

To get `Unity_lic.ulf` (free Personal plan), sign in to **Unity Hub** once on any computer and activate a Personal licence. The file is then at:
Windows `C:\ProgramData\Unity\Unity_lic.ulf` · macOS `/Library/Application Support/Unity/Unity_lic.ulf` · Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf`.

The project is based on XREAL's official SDK template: Unity **2022.3.62f2**, XREAL SDK 3.1.0, AR Foundation 5.2, XR Hands 1.4.3 and XRI 2.6.5, with input source set to Hands. You don't need to set up a scene: `RuntimeRig` builds the XR Origin, AR session and managers when the game starts.

AR hand-tracking supernatural action game. Unity + XREAL SDK 3.x. Four levels, roughly an hour of play as built (the design doc's 3–4 hours needs more encounters per level).
All geometry and effects are procedural. The sound library is synthesised (Resources/Audio, no third-party samples).

**Status:** the code compiles against the Unity/XREAL APIs, but nobody has run it in Unity or on the glasses yet.
Expect a first round of fixes and gesture tuning on your machine.

## Levels

| # | Level | What you do | Unlock |
|---|---|---|---|
| 0 | **The Quarry** | The room fractures into the quarry. Walk, touch, grab, throw, push, shield, block and return fire, then the keystone door. Three creature fights (walker, armoured charger, thrower). The collapse: hold the passage open with both palms, then escape. | Telekinesis, Force Push, Rock Shield |
| 1 | **The Survey Tunnels** | Tunnel sections slide toward you so the whole level fits your room. Press the control panel in order. Lift 3 energy cores into the board (one is jammed in the ceiling, so tear it out). Clear the jammed gate and open it with a two-hand push. In the dark section, your **hand light** reveals markings and **lurkers** that only exist where the light touches. In the gravity room, float rocks into place to build a bridge for the drone. **Beam puzzle:** grab and twist 3 mirror pillars; hidden arrows on the far wall only show from the left, centre or right of your space. | Gravity Pull |
| 2 | **The Research Station** | Three stations appear in your space. **A:** twist the floating contacts into line. **B:** press the switches in the order from the incident memo. **C:** hold the broken machine together with one hand while plugging in its power with the other. Then the containment breach and **the Mimic**: it copies your hands, and raising both hands over your head makes it expose its core. Finally, defend the unstable generator from waves of creatures. | Compression |
| 3 | **The Fracture Chamber** | Crush 3 mechanism parts out of their rock casings with Compression, twist each part so its connector faces you, and slot it into the frame. The chamber reshapes. **Boss: The Formation.** Rock barrage (shield it, or pinch the rocks out of the air and throw them back). Ground rupture (step to the other half of your space). Compression pull (push its rocks away before it heals). Final phase: tear its armour off with telekinesis, then shatter it with a two-hand push. The administrative complex rises. | — |

## Powers and gestures

| Gesture | Power |
|---|---|
| Aim + pinch / move forward + release | **Telekinesis** grab / throw (aim-assisted; release while still to place) |
| Twist your wrist while holding | Rotate puzzle pieces |
| Pinch an incoming rock out of the air | Catch it and throw it back |
| Open palm + fast thrust (both hands = heavy) | **Force Push** |
| Open palm held still facing out | **Rock Shield**, which catches shots and charges up; thrust through it to fire the shards |
| Open palm then snap to a fist | **Gravity Pull** pulls up to 6 stones into orbit around your hand; open and thrust to launch them |
| Both hands closed, brought together fast | **Compression** crushes a held stone into a dense shard (5 damage), or crushes armour / casings in front of you |
| Raise a hand in the dark | **Hand light** (Level 1) |

Gesture thresholds are in `Tuning` (Scripts/Input/HandInput.cs).

## Feel layer
- **Hit-stop** on heavy impacts.
- **World shake** (the environment shakes, never your head).
- **Light flashes** on impacts.
- **Layered impacts:** stretched sparks, physics debris chunks, dust and a glow core.
- **Telekinesis:** a multi-strand jittering tether, a resonant hum on held objects, and throw trails.
- **Audio:** 3D pooled audio with a reverb zone per environment (Quarry, SewerPipe, Hallway, Arena) and crossfaded ambience beds.

## Menus, save, pause
- **Title screen:** Continue, New Game, Level Select (unlocked levels), How to Play, Settings, System Check and Quit.
- **Settings** are saved: volume, ambience, gesture sensitivity (relaxed / normal / strict), subtitles and look.
- **Pause:** look at your left palm for 1 s (Esc in the simulator). Options are Resume, Restart Level, How to Play, Settings and Quit to Title.
- **Progress:** auto-saves per level. Credits play at the end of Act I.
- **Dev shortcut:** set `GameBootstrap > Start Level` to 0–3 to skip the menu; -1 is the shipping default.

## Testing
See **SHIP_CHECKLIST.md**:
- **EditMode tests:** content checks.
- **PlayMode test:** the whole game runs automatically.
- **In-game System Check:** the same automated pass on the device.
- **Manual playtest script:** what to verify on the glasses yourself.

## Manual setup (alternative to CI)
1. Use Unity 6 (URP), Android platform. In Package Manager install **XR Plug-in Management**, **XR Interaction Toolkit**, **XR Hands**, **AR Foundation** and **Input System**.
2. Add the **XREAL SDK 3.1** tarball from developer.xreal.com/download.
3. Enable XREAL under XR Plug-in Management (Android). Set **Initial Input Source = Hands** and tracking to **6DoF**.
4. Copy `Assets/Resonance` into your project.
5. Open a new scene, add an XR Origin (AR), then run **Resonance > Setup Current Scene**.
6. Build to the Beam Pro (Build And Run over USB).

`GameBootstrap` has **Start Level** (0–3) and **Start Step** fields for jumping ahead while testing.

## Editor simulator (no glasses)

Press Play. These keys stand in for hand gestures:

| Key | Action |
|---|---|
| WASD | Walk |
| RMB drag | Look |
| Mouse | Aim |
| Scroll | Reach / held-object distance |
| LMB | Pinch / throw (hold **Alt** when you release to place) |
| Space | Push |
| Esc | Pause |
| Shift (hold) | Shield |
| G | Two-hand push |
| F | Both palms |
| Q | Gravity Pull |
| C | Compress |
| Z / X | Twist held object |
| N | Skip step |

## Look: Cinematic vs Glasses
`GameBootstrap > Look` switches the art direction:
- **Cinematic:** dark cold concrete, crushed blacks, one hot accent per level (yellow, cyan, red, amber), fog, and URP post-processing (ACES, desaturation, lift/gamma/gain, bloom, chromatic aberration, film grain, vignette). Use it in the editor, for recordings, and for passthrough headsets.
- **Glasses:** the same direction, but surfaces are lifted and post-processing is reduced to bloom and colour. Darkening effects (vignette, crushed blacks) can't show on an optical see-through display. *Glasses look on device* switches this automatically when the game runs on the Beam Pro.

The HUD has corner brackets, a centre reticle, a segmented resonance meter (it fills while you hold, pull or shield), a live telemetry readout and a per-level accent colour.
**Vortex effects:** heavy pushes, Compression, deaths, the Mimic (a permanent vortex around its core) and The Formation's exposed core all tear stone into a swirl with orbiting spark streaks and turning arcs.

## Display constraints this build works around
- **Additive optical display.** Black is see-through, so everything important glows; turn the glasses' tint to max.
- **~52° diagonal field of view.** Action stays 1.6–7 m away and the HUD lazily follows your view.
- **Hand tracking only works in front of you, chest height.** Every interaction is designed there.
- **2 × 2 m room.** Levels slide toward you or fold around you instead of making you walk.

## Files
```
Scripts/Core      Util (meshes, materials), Sfx (audio), Feel (hit-stop, shake, impacts), Player
Scripts/Input     HandInput (XR Hands → gestures + simulator), SimIn
Scripts/Arena     floor detection, pinch-to-place, anchor, bounds
Scripts/Powers    HandPowers (telekinesis, push, shield, gravity pull), PowerSet (unlocks, compression)
Scripts/World     Env (fold transitions, hand light), Quarry, Environments (tunnels/station/chamber),
                  Throwable, Projectile, StoneDoor, Puzzles (buttons, sockets, beam mirrors, drone, machine)
Scripts/Enemies   Creature (walker/charger/thrower/lurker), Launcher, Mimic, Formation (boss)
Scripts/Level     LevelBase + GameFlow, Level0Director, Level1Tunnels, Level2Station, Level3Chamber
Resources/Audio   35 designed WAVs (impacts, pushes, shields, creatures, boss, ambience loops)
```
