# RESONANCE — Ship checklist

## Where it stands
| Area | Status |
|---|---|
| Code compiles (4 levels, menus, save, tests) | ✅ Checked against the Unity / XREAL / URP / Input System APIs, outside Unity |
| Every sound mapped to a WAV, no missing clips, none unused | ✅ Checked (28 sounds + 5 loops, 35 files) |
| Runs inside Unity | ⬜ **Not yet run** — needs your computer |
| Runs on Air 2 Ultra + Beam Pro | ⬜ **Not yet run** |
| Gesture tuning on real hands | ⬜ Not done |
| Performance on the Beam Pro (target 60 fps) | ⬜ Not profiled |
| Store build (signing, listing, policy) | ⬜ Not started |

The game is not shippable until every box above is ticked.

## 1. Automated tests (in the Unity editor, ~5 min)
1. Window > General > **Test Runner**.
2. **EditMode > Run All.** Checks that every sound has a clip, no clip is silent, loops exist, rock meshes are valid and gesture presets are sane.
3. **PlayMode > Run All.** Plays the **whole game** in the simulator, spending ~1.6 s in each of the 35 steps across all 4 levels. Every step's spawns, effects and audio run. It fails on any exception or error log.
4. Send me the Console output for any failure. The step name is included, so I can fix it directly.

## 2. System Check on the glasses (~4 min)
Title menu > **SYSTEM CHECK**. It runs the same automated pass on the device and ends with a PASS/FAIL report showing the step count, error count and missing audio.
For full logs, watch `adb logcat -s Unity` while it runs.

## 3. Manual playtest script (on the glasses)
Tick each item. Note anything that feels weak and send it to me.

**Menus**
- [ ] Title appears in front of you; buttons respond to **aim + pinch** and to **poking**
- [ ] Settings: volume, ambience, gesture sensitivity and subtitles all change and are saved after restarting the app
- [ ] Pause: look at your **left palm** for 1 s → the pause menu opens. Resume, Restart Level and Quit to Title all work
- [ ] Continue resumes at the last level; Level Select lists unlocked levels only

**Level 0 · The Quarry**
- [ ] The floor is found; pinching places the play area; the border turns red when you step out
- [ ] The wall fractures and the quarry rises; the voice subtitles show
- [ ] Walk to the pedestal and touch the mineral
- [ ] Grab: target highlights blue, the tether shows, the hum plays
- [ ] Throw: release while moving forward; aim assist hits the crystals
- [ ] Force Push knocks over the stack; a two-hand push feels heavier
- [ ] Shield forms from a still open palm; blocks projectiles; thrusting fires the shards
- [ ] Keystones go into the sockets; pushing opens the door
- [ ] Walker / Charger / Thrower can each be beaten with the intended power
- [ ] Holding both palms keeps the passage open; walking forward escapes

**Level 1 · The Survey Tunnels**
- [ ] The tunnel folds in; buttons I, II, III are pressed in order
- [ ] 3 cores (one is torn out of the ceiling) go into the board
- [ ] Gate: clear the rocks, then a two-hand push opens it; the tunnel slides forward
- [ ] Dark: the hand light reveals markings; lurkers only show inside the light
- [ ] Gravity room: 3 floating rocks build the bridge; the drone crosses
- [ ] Beam: pillars twist with your wrist and snap; the hidden arrows show from the left, centre and right; the reactor powers
- [ ] Gravity Pull: open palm → fist pulls stones; open + thrust launches them

**Level 2 · The Research Station**
- [ ] Station A contacts align by twisting; B switches follow the memo order; C works one hand holding, one hand plugging
- [ ] Compression unlocks; crushing a held stone makes a dense shard
- [ ] The Mimic copies your hands; raising both hands exposes its core; plates can be crushed
- [ ] The generator survives 40 s (a failure restarts the wave)

**Level 3 · The Fracture Chamber**
- [ ] Casings crush; parts twist so the connector faces you, then slot in
- [ ] Boss: barrage can be shielded and caught; the rupture warning is readable and dodgeable in 2 × 2 m; push breaks the pull attack
- [ ] Final phase: tear plates off, then a two-hand push shatters it; credits roll

**Comfort / hardware**
- [ ] Hand tracking stays solid at chest height in your lighting
- [ ] HUD text is readable; nothing important sits outside the field of view
- [ ] Holds 60 fps everywhere (check the boss fight and the Mimic vortex in particular)
- [ ] 30+ minutes without the Beam Pro overheating

## 4. Store build
- [ ] Player Settings: company/product name, package id (e.g. `com.yourstudio.resonance`), version 1.0.0, IL2CPP + ARM64, and the minimum API level from XREAL's docs
- [ ] App icon (I can make one) and splash
- [ ] Create a release keystore and **back it up** (losing it means you can never update the app)
- [ ] Choose the store: Google Play (the Beam Pro runs Play apps) and/or XREAL's own app listing. Check their current XR app requirements
- [ ] Privacy policy: the game uses camera-based hand tracking and stores nothing off-device (PlayerPrefs only). Play requires a policy URL
- [ ] Content rating questionnaire (fantasy violence, no blood)
- [ ] Store screenshots must be **real captures** from the game. The renders in this repo are look targets and can't be used as screenshots
- [ ] Closed test track with a few testers before release
