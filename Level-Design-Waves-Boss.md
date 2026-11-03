# Khaled Alhabbash - Level Design, Enemy Waves & Boss

## My responsibilities

- Level design: layout of the encounter space, spawn positions, pacing.
- Enemy waves: when, where and in which formation enemies appear, and when a wave counts as finished.
- Boss: design and later implementation of the end-of-level boss (proposal only for now, see below).

Not mine (I connect to these instead of rewriting them):

| Area | Owner | How waves connect | Used in the integrated demo (Nov 2) |
| --- | --- | --- | --- |
| Enemy behaviour / AI | Leonardo Sato | His enemy prefabs go into each wave's **Enemy Prefabs** list. The spawner never moves or controls enemies. | **Yes - his model.** `Enemy_model.prefab` (main, `1cee9a3`) is the visual of all three demo enemy classes. His prefab has no collider, health or attack yet, and `EnemyMovement` loops scene objects around a polygon forever (no spawn/exit lifecycle), so movement, health and contact damage still come from my test components. |
| Player & rail movement | Abel Tola | The ship and camera should ride his rail; spawns and bounds follow the rig. | **Yes - his player controller.** `PlayerController` + `CharacterController` + `PlayerCollision` (branch `movement-rail-system`, `df07f90`, not yet in `main`) move the ship. His `LevelCamera` does not move yet ("unfinished CameraKeyFrames"), so the rig is static. |
| Weapons & combat | Taha Hammoud | Enemies only need to be destroyed (or disabled) when killed; the wave system notices automatically. | **No code exists yet** (values in `weapons-combat.md` only). The test weapon stays. |
| UI, audio, scoring, game flow | Ronit Mahajan | Can listen to `onWaveStarted`, `onWaveCleared`, `onEncounterCompleted`, `onEncounterFailed` and read the counters. | **No code exists yet.** My test HUD, sounds, restart and defeat handling stay. |

---

## Integrated demo (working prototype for Milestone 2)

**Scene:** `SolarDivers/Assets/Scenes/LevelWaves_Demo_Integrated.unity` (in Unity: `Assets/Scenes/LevelWaves_Demo_Integrated`).
The isolated test scene `LevelWaves_Encounter01_Test` is kept for development and its own tests.

**Controls:** **WASD / Arrow keys** move (Abel's controller, project-wide *Move* action), **Space / Left mouse**
fire (test weapon), **R** restart, **Tab** debug counters. Click the Game view once after pressing Play.

**The encounter - "Mars Orbit Breakout"** (about 73 s if nothing is shot down, shorter when enemies are destroyed):

1. **Introduction:** the mission banner and an objective line ("Survive the Marquis's patrols and keep the route to Earth open").
2. **Wave 1 - Patrol Screen (introduces the patterns):**
   - fighters in lines, one pattern per group: a left/right sweep, a curved approach, a dive with a brief retreat;
   - one single, slow, unleading aimed shot per group, with a 1 s warning;
   - no reactions and no rockets.
3. **Wave 2 - Interceptor Dives (combines them):**
   - V formations with a fast interceptor at the back, mixing dive-and-regroup, sweeps and retreats;
   - a one-off lane shift towards you; interceptors make a short pursuit (at most 0.8 s, then commit);
   - 3-shot bursts with a little lead;
   - the **first homing rocket** of each group, from the V leader.
4. **Wave 3 - Marquis Escort (more pressure):**
   - heavies, fighters and interceptors: heavies sweep slowly and launch rockets, interceptors flank your last
     seen position, fighters pursue briefly;
   - 4-shot bursts with more (still capped) lead.
   - Paths vary a little between runs (width, dive depth, speed, side), within fixed bounds.
5. **Ending:**
   - **Win:** **ROUTE CLEAR** shows destroyed / escaped / time, plus "Next: the frigate (boss encounter not built yet)".
   - **Defeat:** after 3 hits, **SHIP DESTROYED**; spawning stops and the controls turn off.
   - **R** restarts everything.

**Current rules (not invented penalties):** 3 hits per run with 1 s invulnerability, and contact with an enemy or
an enemy shot costs one hit. **Escaped enemies have no penalty**; they are only counted on the result panel until
the team decides otherwise.

**How the team's work is plugged in** (no teammate script, prefab or scene was edited):

- **Leonardo's model:** each demo enemy prefab (`Assets/Prefabs/LevelWaves/Demo/LW_Enemy_Heavy`, `_Fighter`,
  `_Interceptor`) nests `Enemy_model.prefab` as its visual, scaled to the class size (heavy 2.4, fighter 1.5,
  interceptor 1.2 units wide). Changes Leonardo makes to his prefab flow through automatically. Hitboxes are
  triggers, so Abel's CharacterController is not physically blocked by enemies.
- **Abel's player:** his `PlayerController` (speed 0.15 per frame), a `CharacterController` and `PlayerCollision`
  are on the player, which is tagged `Player`. Invisible walls tagged `Boundary` (his idea from `railSystemTest`)
  keep the ship in the play area (x ±9, z -8..0).
  - The test weapon (`TestPlayerShip` with *Handle Movement* off) only fires.
  - The test health sits on a child trigger hitbox, so it does not fight the CharacterController.
  - On defeat the harness switches his controller off; restart switches it back on and teleports the
    CharacterController safely.
- **Frame rate:** his controller moves *Speed* per frame (not per second), so `DemoFrameRateCap` holds the demo at
  60 fps (VSync off) while it runs and restores the previous settings afterwards. Remove it once that movement
  uses `Time.deltaTime`.
- **Rail (Abel's moving camera, `1fcae16`):**
  - The camera, player, boundaries, spawn origin, spawned enemies, attack director, entry warnings and backdrop
    are all children of **`RailRig`**. `WaveEncounter` *Bounds Space* points at it, so bounds follow the rig.
  - **`RailPath (Abel's LevelCamera)`** carries his `LevelCamera`, moving at 1.5 units/s along his `KeyFrame`
    objects (`RailKeyFrames`, 1,200 units apart). This is set up only through his own serialized fields.
  - `RailPathFollower` on the rig follows that path. It moves first each frame and syncs physics, so his
    CharacterController player rides along; it also puts both back at the start on R, since `LevelCamera` has no reset.
  - The test enemies, enemy shots, rockets and warnings work in their parent's space, so routes and safety
    heights move with the rig.
  - In his own `railSystemTest`, the camera is a sibling of the player and its keyframes lower it from height 15
    to 12 (a zoom). The demo uses his component for forward travel instead.
- **Enemy appearance (checked, not changed):**
  - The demo shows Leonardo's exact model, `Spaceship_BarbaraTheBee` from `Enemy_model.fbx`, with his -90°
    rotation, scaled only per class.
  - Its single material is named `Atlas`, but no texture is referenced in the FBX or committed, so Unity renders
    it plain light grey (0.906).
  - His test-scene camera sees the ship from in front and 30° above (canopy window facing the viewer). Abel's
    rail camera looks straight down, so the demo shows its top: round body, two domes, swept wings.
  - Renders of both views were made in 6000.6.0f1 (see Validation).
  - **For Leonardo:** commit the colour texture if the source pack has one, and record the model's source and licence.
- **Built from code:** `Solar Divers > Level Waves > Rebuild Integrated Demo Scene` (or
  `WaveTestSceneBuilder.BuildDemoFromCommandLine`) rebuilds the test scene, the demo prefabs and the demo scene.

**Boss: not implemented.** No Issue, milestone or repository document asks for a boss in this milestone; the GDD
routes all end at the final boss (the Marquis's frigate). The result panel says the boss is still to come, and the
boss proposal and its tasks are at the end of this document.

---

## Proposed level and wave design (PROPOSAL)

Encounter 01 is a short top-down test encounter (camera looking straight down, enemies enter from the top of
the screen and fly toward the player). Target length: **about 60-90 seconds** if the player lets enemies
through, shorter if they kill everything quickly. This is a development target I chose, not a course requirement.

Pacing idea: pressure rises gradually and predictably (no randomness). Each wave adds one enemy per group and
shortens the gaps. Groups alternate left/right entry positions, so there is always an open lane to dodge into;
the last group of each wave comes down the middle.

| Wave | Formation / style | Enemies | Entry positions (x) | Gap between groups | Idea |
| --- | --- | --- | --- | --- | --- |
| 1 - Basic Line | Line, whole row at once | 3 groups x 4 basic | -4, +4, 0 | 3.5 s | Warm-up: "shoot what's in front of you", dodge to the open side. |
| 2 - V Formation | V, whole V at once | 3 groups x 5 basic | +4.5, -4.5, 0 | 3 s | Focus the leader or sweep across the arms. |
| 3 - Mixed | Mixed flanks, streamed in one by one (0.5 s) | 3 groups x 6, alternating basic + weaver | -2, +2, 0 | 2 s | Peak pressure: two enemy types, wider spread, player must move. |

Total: 45 enemies. Short pauses before waves (2 / 2.5 / 2.5 s) show an "INCOMING" banner.

Overall game structure (team design): **three alternative routes, each leading to Earth and the final boss**.
Encounter 01 is a building block for one segment of one route; the routes themselves are not built.

Later level ideas (not built): rail-camera segments where waves are triggered by camera keyframes,
environmental hazards (asteroids) between waves.

Proposed player-damage rules (prototype values, to agree with the team): **3 hits** per run, **1 s**
invulnerability after a hit (red flash, then blinking), defeat on the third hit. Escaped enemies currently have
**no penalty**; whether they should cost score or hull is a team decision.

---

## What is actually implemented (DONE on branch `khaled/level-waves-prototype`)

- `WaveEncounter` component that runs a list of waves in order: per-wave delay, several groups per wave,
  per-group entry positions, and a choice of spawn style:
  - **Formation**: the whole group appears in the same frame, so a line is a straight row and a V is a V.
  - **Stream**: one enemy every *Spawn Interval* seconds (earlier enemies keep moving, so the shape stretches out).
  The original diagonal "line" came from streaming: each enemy started moving before the next one appeared.
- Three formations: `Line`, `VFormation`, `Mixed` (`WaveFormation.cs`, pure maths, unit-tested).
- A wave finishes when every enemy it spawned is **destroyed/disabled** or has **left the encounter bounds**
  (escaped enemies are removed and counted separately).
- **Cleared is not the same as all destroyed**: when the last wave finishes the encounter is *cleared*;
  `AllEnemiesDestroyed` is only true if nothing escaped or timed out. The end screen shows destroyed and escaped totals.
- **Failure**: `FailEncounter(reason)` stops spawning immediately, fires `onEncounterFailed` once, and makes
  victory impossible until a restart. Enemies already on screen keep flying until they leave.
- Safeguards:
  - **Clear timeout** per wave: if enemies are still alive N seconds after the last spawn (stuck, off-screen
    but inside bounds, AI bug...), they are force-removed with a Console warning, so the encounter never waits forever.
  - Destroyed enemies (null references) are swept every frame, so it does not matter which system destroyed them.
  - Missing prefab slots give a **Console error naming the wave and element**; a wave with no usable prefab
    (or 0 enemies) is skipped with an error instead of hanging.
- Events: `onWaveStarted(int)`, `onWaveCleared(int)`, `onEncounterCompleted()`, `onEncounterFailed()`, and
  `onGroupIncoming(centre, width, seconds)`, raised *Entry Warning Seconds* (default 1.2 s) before each group spawns.
  The warning time is taken out of the existing delays, so pacing does not change.
  Completion fires **exactly once** and never after a failure.
- Optional spawn info: right after spawning, the encounter calls `OnWaveSpawn(WaveSpawnInfo)` on any component of
  the enemy that implements `IWaveSpawnReceiver` (wave, group, slot, slot offset, group entry offset).
  Enemies without one are unaffected; the spawner still never moves enemies.
- Restart: `RestartEncounter()` removes this encounter's enemies and resets every counter. All runtime state is reset
  when the encounter starts, because this project has *Enter Play Mode Options* on with domain **and** scene reload disabled.
- Test scene with a decorative space backdrop, spawn origin, scene-view gizmos showing
  the encounter bounds and every wave's spawn positions.
- Editor menu **Solar Divers > Level Waves > Rebuild Encounter 01 Test Scene** that regenerates the test scene,
  placeholder prefabs and materials if they get broken.
- PlayMode tests (see Validation).

### Presentation pass (test scene only)

Goal: make the prototype readable for a class demonstration without touching teammates' systems.
Everything below lives in the test scene, `TEST_` prefabs/materials/meshes, or `Placeholders/` scripts.

- **Ships** (original low-poly meshes generated by `Editor/LowPolyShipMeshes.cs`, saved in `Assets/Models/LevelWaves/`):
  - Player *Arrow*: white fuselage, pointed nose, blue swept wings and canards, glass cockpit, twin cyan engines.
  - Basic enemy *Dart*: dark red narrow hull, forward-swept blade wings, red eye, single orange engine.
  - Weaving enemy *Manta*: wide purple/orange flying wing with forward pincers, yellow eye, twin magenta engines.
  - Different silhouettes, not just colours. Models are a `Model` child with **no colliders**; the gameplay
    hitbox stays on the root (Dart 1.5 x 1.5, Manta 2.2 x 1.2, player 1.2 x 1.6 - slightly inside the wingspan).
  - `TestShipBanking` rolls the model into sideways movement and eases back to level (player and enemies).
  - Engine exhaust: small world-space particle trails per nozzle (stops when the player is destroyed).
- **Shooting feedback** (test weapon only; damage 10, 5 shots/s, speed 25, 2 s lifetime are unchanged):
  laser bolt mesh with a trail, muzzle flash, hit sparks at the impact point, white hit flash on enemies,
  explosion when an enemy is destroyed by damage (escaped/timed-out enemies vanish silently), larger one for the player.
- **Cleanup**: all effects are parented to `PLACEHOLDER_FX` and destroy themselves when finished; **R** removes
  every effect, projectile, warning and enemy and stops all sounds. Generated audio clips are destroyed with the scene.
- **Post-processing**: a scene-only global volume (`TEST_EncounterPostFX`: bloom for HDR glows only, light vignette).
  The project's URP assets and default volume are unchanged.

### Readability, handling and feedback pass (test scene only)

Requested after playing the presentation pass: text was small/blurry, the scene too dark, gunfire sounded thin,
damage lacked impact and enemy movement was predictable. Still placeholder content, still separate from teammates' systems.

- **Blurry text: cause and fix.** The HUD was a *Screen Space - Camera* canvas on the scene camera, so URP ran
  **FXAA, bloom and vignette over the text** (in URP 17, FXAA runs as a final pass over the whole camera output).
  Now the HUD renders through its own **UI-only overlay camera with post-processing off**, stacked on the scene
  camera. The scene camera no longer draws the UI layer and uses SMAA instead of FXAA. The canvas is
  pixel-snapped. Font sizes at 1920x1080: 28-34 px for HUD/body text, 52-56 px for the banner and result headings.
  The HUD scales with the window but never below 0.6x, so body text stays at least about 18 px in a small Game view.
  Text on panels uses no edge effect; text over the scene (banner, "HIT -1") gets a thin outline.
  Remaining blur can come from the Editor itself: Game view **Scale** above 1x, or a fixed resolution larger than
  the Game view window (it is then shrunk to fit). See the replay notes.
- **Lighting:** brighter key light from the upper left, stronger cool fill, higher ambient, lighter hull colours,
  low metallic values (there is no environment to reflect, so metallic surfaces looked black), a faint emissive
  "lift" so silhouettes read against space, and a lighter vignette.
- **Player handling** (`TestPlayerShip`): velocity now eases towards the input. Acceleration is 60 u/s², so top
  speed is reached in about 0.13 s; deceleration is 75 u/s², so the ship stops in about 0.11 s and slides
  under 0.45 units. Reversing brakes at the deceleration rate. The integration is exact, so 30 and 144 fps cover
  the same distance. Hitting a limit clears velocity into it. Top speed (8), limits and fire rate are unchanged.
- **Banking** (`TestShipBanking`): roll (player up to 30°), optional yaw into the turn (enemies 16°), frame-rate
  independent easing back to level. Only the `Model` child rotates; the root and hitbox never do.
- **Enemy movement** (`TestEnemyMover`, placeholder, separate from Leonardo's AI). Each path is a fixed function
  of distance travelled, so it is smooth and frame-rate independent:
  - *Dart* (basic): **curved pass**. One smooth sideways sweep of 2.6 units. Groups entering on a side sweep
    towards the centre, opening the outer lane; the centre group splits apart, opening the middle. The middle
    ship of an odd group flies straight.
  - *Manta* (weaver): **flank and weave**. Swings out up to 2.2 units towards its own flank and back, with a
    gentle 0.5-unit weave on top.
  - Safety: all sideways motion has faded out by **9.5 units** of travel, i.e. before z = 0.5. The player's zone
    starts at z = 0, so enemies never turn near the player. Peak sideways speed is under 4 u/s. Enemies still
    enter from off-screen (spawn z = 10, top of view about z = 9.2).
  - **Entry warnings** (`TestEntryWarnings`): blinking amber chevrons at the top edge, across the incoming
    group's width, 1.2 s before it appears.
- **Damage feedback:** at the moment of a hit the ship flashes bright red for 0.12 s, then blinks for the rest
  of the 1 s invulnerability. The lost hull pip pops white and "HIT -1" appears briefly. A red flash covers only
  the screen edges (0.3 s; 0.5 s on defeat), and an **optional** camera shake lasts about 0.2-0.3 s
  (`TestCameraShake`, untick *Shake Enabled* to turn it off). The HUD camera does not shake. Hit count,
  invulnerability, defeat and restart behave as before.
- **Sound** (`TestFxManager`, generated in code). Separate sounds for firing, enemy impact, enemy destroyed,
  player hit and player destroyed:
  - *Firing*: three variants, each layering a soft click, a falling tone with a little edge, a short low thump
    and an airy noise tail (0.16 s).
  - *Enemy impact*: three variants of a metallic ping plus a tick.
  - *Enemy destroyed*: two variants of a thump, rumble and crackle.
  - *Player hit*: a gritty crunch plus a two-tone warning blip.
  - *Player destroyed*: a longer, deeper explosion with a falling tone.
  - Mixing: ±5% random pitch, never the same variant twice in a row. An 8-voice pool allows at most 3 copies of
    one sound at once (the oldest is cut), and gunfire is the first to give way. Every clip is peak-normalised
    with fade-in and fade-out, so nothing clicks or clips.
- **Effects:** star-shaped muzzle flare with random spin and size; brighter and longer bolt trail (white core
  to cyan); impact sparks with a star flash; red-orange player-hit sparks; explosions made of a star flash,
  fireball puffs, debris sparks and a faint shock ring (under 0.5 s); exhaust with a hot white core cooling to
  the engine colour.
- **Space environment** (`PLACEHOLDER_Background (decor only)`). Every body is a flat image with its lighting
  painted in, which works because the camera always looks straight down:
  - **Planet:** an ocean world with continents, beaches, a small ice cap, streaky clouds, sun glint on the
    oceans, a clear day/night edge with a thin warm twilight band, and a blue atmosphere rim lit on the day side.
    It sits in the bottom-right corner, outside the lanes the ships use.
  - **Moon:** a small cratered moon on the left, below the HUD panel, lit from the same direction.
  - **Stars:** three layers at different depths and speeds (far, mid and faint near dust streaks), plus two
    faint drifting nebula clouds.
  - **Sun preset** (off by default): select `PLACEHOLDER_Background (decor only)`, set `TestBackdropPreset` >
    *Preset* to *Sun*, then right-click the component > *Apply Preset* (or set it before pressing Play). The Sun
    replaces the planet and moon, and the key light turns to match it. Only one backdrop shows at a time.
  - Placement: everything sits 4-90 units below the play plane, with no colliders. The rocks from the previous
    pass were removed to keep the screen uncluttered.

### Difficulty, speed, sound and background pass (test scene only)

Requested after playtesting the previous pass. Still the same single encounter with three waves; the enemies are
still placeholders, separate from Leonardo's AI.

- **Progressive difficulty through behaviour, not health.** Enemy health and counts are unchanged. Each wave
  reads its settings from a reusable asset, `Assets/Prefabs/LevelWaves/TEST_Difficulty_Encounter01.asset`
  (`TestDifficultyProfile`, one entry per wave; later encounters can copy it or point to their own). The test
  enemies receive their wave number through the optional `IWaveSpawnReceiver` hook, so `WaveEncounter` did not change.
  - **Wave 1 - readable curved passes:** the same paths as before (side groups sweep in, centre groups split
    apart), normal speed, no repositioning, no attacks.
  - **Wave 2 - alternating flanks and a limited lane shift:**
    - Speed is ×1.1 and the sweeps are 25% wider.
    - The centre group sweeps to one side as a unit, alternating left and right per group, instead of splitting.
    - Each ship reads your lane **once**, after 2 units of travel, then eases up to **1.8 units** towards it,
      finishing by 7 units. It never tracks you after that.
  - **Wave 3 - coordinated mixed entries and occasional aimed shots:**
    - Darts and Mantas enter as mirrored pairs on opposite flanks (the existing Mixed formation), at speed ×1.15,
      with a smaller 1.2-unit lane shift.
    - **Every 3rd ship carries one aimed shot**, which works like this:
      1. The ship glows and plays a rising whine.
      2. A thin orange aim line follows you for 0.6 s.
      3. The line **locks** 0.3 s before firing, getting brighter and wider.
      4. The shot flies **straight** at 8 u/s towards the locked point. It never homes in.
  - **Fairness rules** (`TestAttackDirector`, settings in the profile):
    - At most 2 enemy shots in flight or charging at once.
    - At least 1.4 s between attack starts.
    - Attacks start only in the upper band (z 3 to 8.5), on screen, and at least 7 units from you.
    - No new attacks while you are invulnerable after a hit, after defeat, or after the encounter ends.
    - Each ship fires at most once.
  - **Dodge space:** all sideways motion, including the lane shift, has finished before z ≈ 0.5, so ships fly
    straight in your zone. There are no spawns near you and no unlimited pursuit. The worst-case sideways speed is
    under 5 u/s in every wave; your top speed is 9.6.
- **Faster player:** top speed is 9.6 (20% above 8). Crossing the 18-unit-wide play area takes **1.95 s**,
  measured in a test, and the area was not changed. Acceleration is 75 u/s² (top speed in about 0.13 s);
  deceleration is 100 u/s² (stops in about 0.1 s, sliding under 0.5 units). Diagonals are normalised, bounds are
  unchanged, and the result is the same at 30, 60, 144 and 240 fps.
- **Sounds (all generated in code):**
  - **Player autocannon** (4 variants, ±4% pitch, 0.15 s, shorter than the 0.2 s fire interval). Layers: a
    sharp high-passed crack, a short pitched punch kept above ~70 Hz (about 5% of the energy is below 60 Hz), a
    saturated mechanical knock, a faint sci-fi zap and a very short dark tail.
  - **Explosions** (3 enemy variants plus a longer player version). A deeper initial impact (a fast pitch drop
    to about 38 Hz with a crunchy crack), brief debris clicks in the first 0.45 s, and a dark low tail with the
    sub-bass below about 30 Hz filtered out, fading over about 0.3 s. Only 12-17% of the energy is below 60 Hz.
  - **New enemy sounds:** a charge whine and an enemy shot "thoom", deliberately unlike the player's sounds.
  - **Separate volumes:** shot, impact, enemy destroyed, player hit, player destroyed, enemy charge and enemy shot.
  - **Overlap limits:** a 10-voice pool, at most 3 copies of one sound (2 for the charge whine, 1 for player
    sounds), and every clip peaks at -2 dBFS (0.8 full scale) with no clipping. Weapon damage and fire rate are unchanged.
- **Enemy destruction effect:** a shorter 0.1 s star flash, an expanding fireball and shock ring, sparks, and
  **7 tumbling hull fragments** that slow down and shrink away within 0.75 s. Every part is gone within 1 s, so
  it does not hide incoming attacks.
- **Brighter background:**
  - The camera background is a lifted deep blue (0.03, 0.042, 0.08) instead of near-black.
  - Stars are larger and fully opaque, the nebulae about 50% stronger, and the vignette lighter (0.12).
  - The planet and moon images are tinted slightly above white (still under the bloom threshold), with their
    night sides lifted a little.
  - Only scene materials and the scene's own volume changed; project settings and URP assets did not.

### Enemy movement and aircraft-combat sound pass (test scene only)

Requested after the next playtest. Still the same single encounter with three waves; every experimental behaviour
is in the labelled test components (`TestEnemyMover`, `TestDifficultyProfile`, `TestFlightAudio`, `TestFxManager`),
separate from Leonardo's enemy AI. No routes, no boss.

**Enemy speed classes** (existing models, re-coloured or re-scaled; health unchanged except the new class):

| Class | Prefab / model | Speed | Turn rate | Health | Notes |
| --- | --- | --- | --- | --- | --- |
| Heavy | `TEST_Enemy_Weaver` (Manta, ×1.1) | 1.7 u/s | 55°/s | 30 (3 hits) | Slow, wide sweeps; the wave 3 aimed shooters. |
| Fighter | `TEST_Enemy_Basic` (Dart) | 2.6 u/s | 110°/s | 20 (2 hits) | The main body of every wave. |
| Interceptor | `TEST_Enemy_Interceptor` (Dart ×0.8, pale gold, white-blue engine) | 4 u/s | 150°/s | 10 (1 hit) | New; fast, flanks in wave 3. |

**Steering instead of fixed curves.** Each ship flies a short route of waypoints on the gameplay plane. Its heading
turns at no more than its turn rate, and the turn rate itself ramps up and down (turn acceleration), so there are no
instant direction changes. The ship's root faces its heading and the model rolls into turns. Patterns:

- **Curved Pass:** a curved approach into a neighbouring lane.
- **Flank Weave:** swings out to its flank, then back in.
- **Side Sweep:** a left/right sweep across the upper screen, then a descent on the far side. It is shortened so
  the whole group stays inside the lane limits.
- **Pass And Retreat:** dives to mid-screen (z ≈ 4.6), pulls back up (z ≈ 7) for a brief retreat, then makes a
  second pass down a nearby lane.
- **Straight:** straight down its lane.

**Introduced gradually** (difficulty asset `TEST_Difficulty_Encounter01`):

- **Wave 1 (fighters only):** groups 1-2 curved approaches, group 3 one left/right sweep. No reactions, no shots.
- **Wave 2 (four fighters + one interceptor per V):** pass-and-retreat dives (groups 1 and 3) and curved passes,
  speed ×1.05. Each ship reads your lane **once** after 2.5 units and shifts its descent up to **1.8 units**
  towards it.
- **Wave 3 (fighter, interceptor, heavy, repeated):** mixed curved, pass-and-retreat and sweep groups, with the
  heavies always sweeping slowly. Speed ×1.1, turn rate ×1.1, lane shift up to 1.2. **Interceptors flank your
  last observed position:** they descend **2.4 units beside it** on the side they are already on, follow your x
  for at most **0.6 s**, then commit and never follow again. The heavies carry the occasional telegraphed aimed
  shot (the same fairness rules as before).

**Fairness and safety:**

- **No turning near you:** below z = 1.5 ships fly straight, and from about z = 3.3 they line up for that
  descent. They cross your zone within 30° of straight down.
- **Retreats stay high:** they happen well above your zone; across every class, pattern and wave the lowest
  retreat measured in the tests is z = 3.14 (your zone ends at z = 0).
- **Spawns are far away:** ships spawn at the top (z = 10); your zone ends at z = 0.
- **Escape lane:** before applying a lane shift or flank, a ship checks the descent lanes of nearby ships and
  shrinks its own move until a free gap of at least **2.6 units** (Min Escape Gap; your hitbox is 1.2 wide)
  remains across the play area.
- **No endless pursuit:** after Max Lifetime (16 s) a ship abandons its route and leaves straight down.
  Waypoints are passed once inside the ship's own turning circle, so a slow ship cannot orbit one.

**Sound: recorded, licensed clips instead of code-generated ones.** Every clip is an edit of a **CC0**
(public-domain) recording found on Freesound and OpenGameArt. Nothing was bought; sources are listed below and in
`Assets/Audio/LevelWaves/CREDITS.md`.

- **Player engine:** a seamless 5.9 s loop of a recorded jet roar. Its level, pitch (0.95 to 1.05) and brightness
  (low-pass 1.6 to 7 kHz) follow your speed and acceleration. It spools up in about 0.15 s, winds down in
  about 0.45 s, and fades out on defeat. A restrained pitch range: no "revving".
- **Enemy flybys:** when a test enemy will pass within 4.5 units of you, a recorded jet pass starts so its
  loudest point lines up with the closest approach. The voice then follows the enemy:
  - level and brightness from distance;
  - stereo position from its side;
  - a small pitch change from the closing speed (at most ±3 %; the recordings already contain a real pass).
  
  At most 2 flybys play at once, at least 0.6 s apart. A ship shot down mid-pass fades out in 0.25 s.
- **Autocannon:** single near-distance rifle reports (Mosin Nagant 7.62x54R, AK-47 7.62x39) played at 0.8x for a
  heavier calibre, with a short low "thump" layer from a ship-cannon recording. Each shot rings for about 0.12 s,
  so 5 shots a second stay separate; harsh highs are softened and the sub-bass is kept low (4-6 % below 60 Hz).
  Four variants, ±3 % pitch.
- **Metal impacts:** short recorded metal clinks (three variants).
- **Player damage:** a heavy hull thud, a resonant metal bong and a crunch, with no electronic beep.
- **Explosions:** recorded firework/cannon bangs played at about 0.65x for depth, a low thump, and a 0.3 s
  metal-debris clatter tail. Each lasts 1.1 s (the player's 1.8 s). At most 2 ring at once; each one already
  ringing lowers the next by about 25 %.
- **Enemy aimed attack:** the charge warning is the recorded jet's turbine whine spooling up (high-passed); the
  enemy shot is a shortened ship-cannon "thoom".
- **Separate volume controls:** Engine and Flyby (on `PLACEHOLDER_FlightAudio`); Firing, Impact, Explosion,
  Player Damage, Enemy Attack and Master (on `PLACEHOLDER_FX`).
- **Cleanup:** restart (R) stops the engine loop, every flyby and every one-shot sound, and the engine fades back
  in from silence. Disabling the objects (leaving Play mode) stops them all. Weapon damage and fire rate are unchanged.

This is a cinematic sound choice for a space game; there is no atmospheric flight physics.

Asset credits:
- **Made for this prototype:** all meshes, materials, textures (planet, moon, Sun, nebulae, flare, ring, chevron,
  soft dot) and the post-processing profile were created in code by Khaled Alhabbash (with an AI coding assistant).
  `Editor/ProceduralSpaceTextures.cs` generates the textures deterministically and saves them as PNGs, which Git
  LFS tracks per the repository's `.gitattributes`. Font: Unity's built-in `LegacyRuntime.ttf`.
- **Sounds:** edited CC0 recordings in `Assets/Audio/LevelWaves/` (44.1 kHz mono 16-bit WAV, tracked by LFS):

| Clips | Source recording | Author | Licence | Edit |
| --- | --- | --- | --- | --- |
| `LW_Flyby_A/B/C`, `LW_EnemyCharge` | [Jet Plane Flyby.flac](https://freesound.org/people/qubodup/sounds/189446/) (from a US government video, public domain; Freesound high-quality preview) | qubodup | CC0 1.0 | 3.3-3.9 s around the pass (pass at 1.4 s), ±3 % speed, 60 Hz-9 kHz, fades. Charge: 0.95 s of the rising whine, high-passed at 1.4 kHz |
| `LW_Engine_PlayerLoop`, `LW_Flyby_D` | [Fighter Jets Passing Overhead](https://freesound.org/people/Nice_Audio/sounds/420560/) (field recording; Freesound high-quality preview) | Nice_Audio | CC0 1.0 | Engine: steady 15-21.5 s section, 45 Hz-8 kHz, -4 dB at 3 kHz, 0.6 s loop crossfade. Flyby D: second jet's pass |
| `LW_Cannon_A-D` (player cannon) | [The Free Firearm Sound Library](https://opengameart.org/content/the-free-firearm-sound-library) (mid-distance single shots: Springfield 1917 .30-06 `B_16P`, Mosin Nagant `M_26P`, Savage .300 BLK `T_17P`) | Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney | CC0 1.0 | Cut at the measured onset, 0.97-1.02x speed, 45 Hz-11 kHz, +2 dB at 140 Hz, natural report kept to 0.52 s (gentle decay from 0.28 s); cannon body layered underneath |
| `LW_Cannon_A-D` (low body), `LW_Explosion_B/C`, `LW_PlayerDestroyed` (layer), `LW_RocketBlast_A` | [Cannon Shot](https://freesound.org/people/qubodup/sounds/187767/) (from a US government video, public domain; clips briefly at the two shots) | qubodup | CC0 1.0 | Body: first 0.45 s, 45-700 Hz. Explosions: each of the two shots with ~1.9 s of its own rumble. Rocket blast: second shot, 1.4 s, 90 Hz high-pass |
| `LW_Explosion_A`, `LW_PlayerDestroyed`, `LW_RocketBlast_B` | [Explosion with debris - authentic. 4kg TNT](https://freesound.org/people/sidohzen/sounds/165808/) (land-mine demolition, camcorder field recording) | sidohzen | CC0 1.0 | Low-passed at 5.5 kHz and +5 dB below 150 Hz (the camcorder mic is thin and bright). Explosion: 1.9 s from the blast; player destroyed: 3.6 s with the long rumble and debris rain; rocket blast: 1.3 s |
| Explosion debris, `LW_PlayerHit*` rattle, `LW_PlayerDestroyed` metal crash | [Scrap metal dropping / crashing](https://freesound.org/people/SamsterBirdies/sounds/587443/) | SamsterBirdies | CC0 1.0 | Separate crashes from the recording, 200-400 Hz high-pass, faded; mixed at 0.2-0.35 from ~0.1 s after the blast |
| `LW_PlayerHit`, `_B`, `_C` (hull struck) | [Rubber Metal Wood Impact Collision Bang](https://freesound.org/people/qubodup/sounds/54850/) (rubber hammer on metal) | qubodup | CC0 1.0 | Three blows cut at their onsets, 80 Hz high-pass, 0.6 s; with the `thud3` hull thud and a short metal rattle |
| `LW_Impact_Metal_A-C`, `LW_PlayerHit*` (thud) | [Metal Impact Sounds](https://opengameart.org/content/metal-impact-sounds) (`clink1-3`, `thud3`) | BMacZero | CC0 1.0 | Clinks: 0.22 s, 350 Hz high-pass (unchanged). Thud: +3 dB below 120 Hz |
| `LW_EnemyGun_A-C` (enemy gunfire) | [The Free Firearm Sound Library](https://opengameart.org/content/the-free-firearm-sound-library) (AK-47 `C_28P`, three single near-distance shots) | Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney | CC0 1.0 | 0.32 s each, 260 Hz-11 kHz, +4 dB at 3 kHz: lighter and brighter than the player's cannon |
| `LW_RocketLaunch`, `_B`, `LW_RocketLoop` | [Rocket Launch.flac](https://freesound.org/people/qubodup/sounds/182794/) (from a US government video, public domain) | qubodup | CC0 1.0 | Launch: the two launches in the recording, 1.5 s each (blast + first second of thrust), faded. Motor loop: the steady thrust right after the first launch, 60 Hz-10 kHz, level-flattened, 0.5 s loop crossfade |

  Every clip is peak-normalised to -1 dBFS. Clips from this pass have no soft limiting and no large speed changes
  (variants differ by at most 3 %). No purchased or unclear-licence assets are used. The edits were made with a
  small offline script (decoding, trimming at measured onsets, filters, mixing); the steps in the Edit column are
  enough to repeat them in any audio editor. `CREDITS.md` in the audio folder lists the same sources, plus the
  recordings that were looked at and rejected.
- **Code-generated sounds removed:** the code-generated sounds from earlier rounds were removed; `TestFxManager`
  now only plays clips from `TEST_SoundBank`.
- **Not used:**
  - The Kenney "Impact Sounds" pack (CC0) could not be downloaded without a browser session.
  - A Wikimedia airliner flyover was rejected: it is CC BY-SA and not a fighter.

The team had no existing models, effects, textures or audio to reuse (checked `main` and `movement-rail-system`).

### Combat, movement and rail pass (Nov 2; test enemies in both scenes)

Still the single encounter, no routes and no boss. All of this lives in the labelled test components, so it can
be dropped once Leonardo's enemy AI and Taha's weapons exist. Neither has code in the repository yet, so there
was no AI or projectile interface to build on.

- **Movement** (`TestEnemyMover`):
  - **Dive and regroup** (new): the group dives spread out, pulls back up into a tighter formation around its
    centre, then makes a second pass together.
  - **Short pursuit** (new, per class): the ship follows your x for at most *Pursuit Seconds* (0.8), then commits
    to that lane while still high above your zone. It never follows continuously.
  - **Bounded variation between runs:** a run seed (new on every start and restart) varies each group's path
    width (up to ±20 %), dive depth (up to ±0.4 units) and, for centre groups, which side they sweep to; each
    ship's speed varies up to ±7 %. There's no per-frame jitter. *Fixed Seed* in the profile makes runs repeatable.
  - Kept: speed classes, turn-rate and turn-acceleration limits, the straight descent near you, retreats, flanks,
    lane shifts, the free-lane check and Max Lifetime.
- **Attacks by role** (`TestEnemyShooter`, `TestAttackDirector`). Each armed ship attacks once, always with a warning.
  - **Fighters:** aimed **bursts** (3 shots in wave 2, 4 in wave 3) along one locked aim line.
  - **Interceptors:** a quicker single shot with a little more lead.
  - **Limited prediction:** the aim leads you by *Lead Factor* (0 / 0.35 / 0.5) of your motion during the shot's
    flight, capped at *Max Lead* (1.5-1.8 units). The line locks 0.3 s before firing, so it shows exactly
    where the burst goes.
  - **Rockets:** heavies (wave 3) and each wave 2 V leader carry one **homing rocket**. First a red pulse, a red
    lock line and a lower lock tone for 1-1.1 s, then the launch.
  - **Fairness:**
    - at most 2 gun attacks busy at once (a burst counts once), at least 1.2 s apart;
    - at most 2 rockets in flight or being readied, at least 3 s apart, launched from at least 8 units away;
    - nothing while you recover from a hit, after defeat or after the encounter ends.
- **Homing rocket** (`TestHomingRocket`, `TEST_HomingRocket`):
  - **Look:** dark metal body, red nose, blinking red beacon, orange flame and grey smoke trail, clearly unlike
    the round orange shots.
  - **Flight:** straight for 0.35 s, then homing at no more than **70°/s** while it speeds up from 3 to
    **6.5 units/s** (slower than your 9.6).
  - **Burn-out and expiry:** homing stops after **2.6 s** and it flies straight, then it's removed after **5 s**
    or on leaving the area.
  - **Counter-play:** its turning circle at full speed is about 5 units, so a late side-step makes it miss.
    It can also be shot down (10 HP), costs one hit on contact, and plays its engine loop on itself (louder and
    panned as it approaches).
- **Sound:** every sound is an edited **recorded** CC0 clip; nothing is synthesised.
  - New this pass: enemy gunfire (PPSh reports), rocket launch (a firework launch hiss), rocket engine loop.
  - The ship-cannon report is now the rocket detonation.
  - Separate volume for rockets (*Rocket Volume*; replaced by the Weapons / Explosions / Engines groups in audio pass 2). Enemy gunfire is panned by position and capped like the other
    one-shots. The rocket warning uses the turbine whine at a lower pitch.
  - Ronit has no audio system in the repository yet, so these stay in `TestFxManager` / `TEST_SoundBank`.
- **Restart (R)** also removes rockets (and with them their engine sound), clears rocket and attack
  reservations, picks a new run seed, and puts Abel's rail path and the rig back at the start.

### Audio pass 2 (Nov 2; sound only, both scenes)

Only sound changed: no gameplay, model, movement, damage or wave values. Ronit has no audio system in the
repository yet, so this still lives in the **temporary** `TestFxManager` / `TestFlightAudio` / `TEST_SoundBank`.

**Why the previous sounds were weak** (measured with the edit script and the in-game volumes):

- **Mix balance:** the player's cannon played at master 0.6 × firing 0.32. That made it the quietest sound in
  the game (about -31 dBFS at its loudest), below enemy gunfire (-29) and even below hits on enemies (-29). The
  rocket launch (-29) was also far below the explosions (-20).
- **Pitch and speed:** the variation at playback was small (±3 %). The edits were the problem: the gun reports
  were slowed to 0.8x and the explosion bangs to 0.55-0.68x. Slowing a recording smears its attack, which gives
  the soft, toy-like thud.
- **Trimming:** the gun reports were cut to 0.3 s, which removed their natural report and reflections, and the
  explosions to 1.1 s. A rifle crack alone (~40 ms) sounds like a cap gun.
- **Limiting:** every clip went through a soft limiter after peak normalisation, flattening the transients a
  little more.
- **Sources:** the explosions were firework bangs, the rocket launch was a firework launch hiss, and the rocket
  motor was mostly sub-rumble (about 290 Hz) whose volume fell off with the square of distance. A rocket
  launched 8+ units away was nearly silent until it was close.
- **Not a cause:** Unity's import settings were already uncompressed PCM, mono, decompressed on load.

**What replaced them** (all CC0 recordings; sources in `CREDITS.md` and the credits table below):

| Sound | Now |
| --- | --- |
| Player cannon (4 variants) | Mid-distance shots of full-power rifles (.30-06, 7.62x54R, .300 BLK) at their own speed, with the natural report kept (~0.5 s) and the first 0.3 s of a recorded cannon shot underneath for weight |
| Enemy gunfire (3) | AK-47 single shots: shorter, lighter and brighter, with no low layer, so they can't be confused with the player's cannon |
| Enemy explosions (3) | A recorded blast with its own rumble (a land-mine demolition, or one of two recorded cannon shots), then metal debris falling from ~0.15 s; 1.9 s |
| Player damage (3) | A rubber hammer striking metal (the hull being hit), a low hull thud and a short metal rattle. Metallic and dull, with no gun crack |
| Player destruction | The full land-mine blast with its long rumble and debris rain, a cannon shot layered on the blast and a large metal crash; 3.6 s |
| Rocket launch (2) and motor | A recorded rocket launch: the launch blast plus the first second of thrust, then the steady motor from the same recording as a loop on the rocket |
| Rocket detonation (2) | Short cuts of the cannon shot and the land-mine blast with less low end: smaller than a kill explosion |

**Mix and playback** (`TestFxManager`):
- Separate volumes: **Weapons** (both guns and rocket launches), **Impacts** (hits on enemies and hull damage),
  **Explosions**, **Engines** (player engine, flybys and rocket motor) and **Warnings** (charge whine and rocket
  lock), plus Master.
- Within each group a fixed balance, set from the measured clip loudness. Loudest 0.1 s at game level, before →
  after:
  - player cannon -31 → -24 dBFS;
  - enemy gunfire -29 → -28;
  - hits on enemies -29 → -28;
  - player damage -19 → -20;
  - kill explosion -20 → -18;
  - player destroyed -18 → -17;
  - rocket launch -29 → -21;
  - rocket blast -24 → -20;
  - warning -23 → -19.
- **Warnings stay audible:** explosions that start while a warning sounds are lowered by 45 %. Explosions also
  still duck each other, and at most 2 ring at once.
- **Repeated gunfire:**
  - pitch variation ±2.5 % (was ±3 %);
  - never the same variant twice in a row;
  - at most 3 shots of one kind overlap. At 5 shots/s each 0.52 s report rings under no more than two following
    shots.
- **Headroom:** the busiest rendered scene peaks at -4.9 dBFS, so nothing clips.
- **Rocket motor:** heard from the launch (at least 35 % of its close level at the edge of hearing). It fades in
  over 0.6 s under the launch blast and gets louder and slightly higher as the rocket closes in. It stops with the
  rocket: detonation, shot down, expiry, leaving the area or restart.
- **Cleanup:** R silences every one-shot, flyby and motor at once (the engine fades back in). After defeat
  everything falls silent once the last sounds end. After completion only the player's engine remains. Stopping
  Play mode leaves nothing.

**Before/after samples** (rendered by the edit script at the in-game volumes, outside the repository):
`C:\Users\hp\Documents\COMP3770-Group5-Work\AudioComparison\`. `AB_*.wav` plays the old version, one second of
silence, then the new one. Scenes: gunfire, explosions, player damage, rocket (warning → launch → motor →
detonation), and a full mix with the engine. They leave out stereo panning and the per-play pitch variation.
Whether the result sounds convincing still has to be judged by ear; the tests only check behaviour and levels.

### Why touching enemies used to do nothing (fixed)

The placeholder ship had no collider and no Rigidbody (the scene builder removed them), the test enemies have a
collider but no Rigidbody, and Unity only sends collision/trigger events when at least one side has a Rigidbody.
There was also no damage code at all: the team has no damage system yet (the only collision script,
Abel's `PlayerCollision.cs` on `movement-rail-system`, only prints "BOUNDARY"). So I added a prototype-only one:
the ship now has a trigger `BoxCollider` + kinematic `Rigidbody` + `TestPlayerHealth`, and test enemies have `TestContactDamage`.

### Placeholders (NOT final game content)

Everything named `TEST_` / `PLACEHOLDER_` or in `GameScripts/LevelWaves/Placeholders/` exists only so the
encounter can be played on `main`, which currently has no player, weapons, enemies, damage or game flow:

- `TEST_PlayerShip` + `TestPlayerShip.cs` - simple WASD ship with eased handling (not Abel's player). Tracks its own shots so restart can remove them.
- `TestPlayerHealth.cs` - 3 hits, 1 s invulnerability, hit flash/blink, defeat event (not a team damage system).
- `TEST_Projectile` + `TestProjectile.cs` - simple shot (not Taha's weapons).
- `TEST_Enemy_Basic` (fighter), `TEST_Enemy_Weaver` (heavy), `TEST_Enemy_Interceptor` + `TestEnemyMover.cs`,
  `TestEnemyHealth.cs`, `TestContactDamage.cs` - steering movers with speed classes and limited reactions that hurt
  on contact (not Leonardo's AI). Enemies survive touching the player.
- `PLACEHOLDER_TestHarness` / `EncounterTestHarness.cs` - defeat -> fail encounter + disable ship; **R** -> full restart (not Ronit's game management).
- `PLACEHOLDER_HUD` / `EncounterDebugHUD.cs` + `PLACEHOLDER_UICamera` - test HUD, hit feedback, result panel and Tab debug counters (not Ronit's UI).
- `PLACEHOLDER_FX` / `TestFxManager.cs`, `TEST_FX_HitSpark`, `TEST_FX_PlayerHit`, `TEST_FX_Explosion` - effects and one-shot sounds (not a team audio/VFX system).
- `PLACEHOLDER_FlightAudio` / `TestFlightAudio.cs` - player engine loop and enemy flybys.
- `TEST_SoundBank` / `TestSoundBank.cs` + `Assets/Audio/LevelWaves/LW_*.wav` - the edited CC0 recordings (see `CREDITS.md` there).
- `PLACEHOLDER_EntryWarnings` / `TestEntryWarnings.cs` - entry chevrons driven by `onGroupIncoming`.
- `TEST_Difficulty_Encounter01` / `TestDifficultyProfile.cs` - per-wave movement/attack settings and shot fairness rules.
- `PLACEHOLDER_AttackDirector` / `TestAttackDirector.cs`, `TestEnemyShooter.cs`, `TEST_EnemyShot` + `TestEnemyBullet.cs` -
  telegraphed aimed shots and bursts for the test enemies (not Leonardo's AI or Taha's weapons).
- `TEST_HomingRocket` / `TestHomingRocket.cs` + `TEST_Rocket_Body`, `TEST_Rocket_Nose`, `TEST_FX_RocketBeacon` -
  the warned, turn-limited homing rocket (not Taha's or Leonardo's weapons).
- `TestCameraShake.cs` (on the Main Camera), `TestShipBanking.cs`, `TestBackgroundDrift.cs`, `TestBackdropPreset.cs`,
  `PLACEHOLDER_Background`, `PLACEHOLDER_PostFX` - visual dressing for this scene only.
- Integrated demo only (`GameScripts/LevelWaves/Integration/`):
  - `RailPathFollower` - makes the `RailRig` (camera, player, spawns, bounds) follow the path that Abel's
    `LevelCamera` moves, and syncs physics for his CharacterController. It's an adapter; his files are unchanged.
  - `TestRailScroll` - fallback rail motion, used only if Abel's `LevelCamera` / `KeyFrame` types are missing.
  - `DemoFrameRateCap` - 60 fps for Abel's per-frame movement.
  - `LW_Enemy_*` demo prefabs - Leonardo's model plus the test enemy logic.

The test scene is a wave-system prototype. The integrated demo is a short playable level built from the
team's current pieces; its weapon, health, HUD and sounds are still placeholders.

---

## Test scene and controls

- Scene: `SolarDivers/Assets/Scenes/LevelWaves_Encounter01_Test.unity` (in Unity: `Assets/Scenes/LevelWaves_Encounter01_Test`)
- Controls (placeholder ship): **WASD / Arrow keys** move, **Space / Left Mouse** fire, **R** restart everything
  (ship position, hull, your shots, enemy shots, effects and debris, sounds, enemies, waves), **Tab** show/hide debug counters.
- Click the Game view once after pressing Play so it receives keyboard input.
- For sharp text, keep the Game view's **Scale** slider at **1x**. If you pick a fixed resolution (1920x1080), the
  Game view panel must be at least that big; otherwise Unity shrinks the image to fit and text softens. That
  softening comes from the Editor preview, not the game. *Free Aspect* always renders at the panel's real size.

What you should see:

1. Layered stars and faint nebulae drifting downwards (forward travel), a lit ocean planet in the bottom-right
   corner and a small moon on the left. Top-left: hull pips, "3/3" and the wave. Bottom-left: controls including **R restart**.
2. The banner "WAVE 1 - BASIC LINE" and amber chevrons at the top edge where the first group will enter; 1.2 s
   later a row of 4 red Dart fighters. Group 1 sweeps sideways across the upper screen and dives on the far side,
   group 2 curves in, group 3 dives and pulls back up. Paths differ slightly every run. One ship per group
   glows and shows an orange aim line (rising turbine whine), the line locks, and a single slow shot flies along it.
   Ships turn in smooth arcs, bank into turns and fly straight once they are level with the top of your area.
   A steady jet engine plays all the time and rises as you move; ships passing near you whoosh past from their side.
3. Wave 2: three V formations (four red Darts plus one small gold interceptor). One group dives spread out, pulls
   back up into a tight formation and makes a second pass. Interceptors follow your lane for under a second, then
   commit. Fighters fire **3-shot bursts** that lead you a little (the locked line shows where). The V leader
   glows red, shows a red pulsing line with a low lock tone, and launches a **homing rocket** (red nose, blinking
   beacon, smoke trail, engine sound) that curves slowly towards you: side-step late or shoot it.
4. Wave 3: Darts, interceptors and slow, larger Manta heavies. Heavies sweep sideways and launch rockets (never
   more than 2 at once, at least 3 s apart); fighters fire 4-shot bursts and make short pursuits; interceptors come
   down **beside** where you were (never on top of you). There is always a free lane to dodge into.
5. Your ship is about 20% faster than before, eases into motion and stops quickly when you let go, banking up to 30°.
6. Shooting: a heavy autocannon report, star-shaped muzzle flash, cyan bolts with trails, sparks and a metallic
   clink on hits. Destroyed enemies: brief flash, expanding blast and ring, sparks and tumbling fragments, with a
   deep bang and a short metal-debris clatter.
7. Touching an enemy: a heavy hull thud with a metal ring, the ship flashes red then blinks for about 1 s, a hull pip pops
   and empties, "HIT -1", a brief red flash at the screen edges and a small camera shake.
   Third hit: bigger explosion and sound, ship greys out and stops, **SHIP DESTROYED** panel. Press **R** to retry.
8. Surviving: **ENCOUNTER CLEARED** panel with destroyed / escaped totals and time, plus "ALL ENEMIES DESTROYED" only if nothing got past.
9. **Tab**: detailed counters (state, alive, spawned, destroyed, escaped, timed out, time) top-right.

---

## Inspector settings (select `WaveEncounter_01` in the Hierarchy, change them while Play mode is stopped)

`WaveEncounter`:

| Setting | Meaning |
| --- | --- |
| Waves | List of waves, run top to bottom. Add/remove/reorder freely. |
| Spawn Origin | Where formations are centred. Its blue arrow = the direction enemies face when spawned. |
| Spawn Parent | Optional Hierarchy parent for spawned enemies (test scene uses `SpawnedEnemies`). |
| Encounter Bounds | Play area (orange box gizmo). Leaving it = escaped. Must contain the Spawn Origin. |
| Bounds Space | Optional transform the bounds move with (the demo uses `RailRig`). Empty = world space. |
| Auto Start | Start on Play. Untick to start it from another script with `StartEncounter()`. |
| Entry Warning Seconds | 1.2. How early `onGroupIncoming` fires before each group (taken from the existing delays). |
| On Wave Started / Cleared / Encounter Completed / Encounter Failed / Group Incoming | Hook UI, audio, score or the next level step here. |

Each wave:

| Setting | Default in scene (W1 / W2 / W3) | Meaning |
| --- | --- | --- |
| Wave Name | - | Shown in logs and the debug HUD. |
| Formation | Line / VFormation / Mixed | Formation shape. |
| Enemy Prefabs | Basic / Basic ×4 + Interceptor / Basic + Interceptor + Weaver | Cycled per enemy (a list as long as the group gives each slot a class). |
| Enemies Per Group | 4 / 5 / 6 | Enemy count per group. |
| Group Count | 3 / 3 / 3 | Total enemies = per group x groups. |
| Spacing | 2.5 / 1.8 / 2 | Distance between enemies. |
| Spawn Style | Formation / Formation / Stream | Formation = whole group at once; Stream = one by one. |
| Spawn Interval | 0 / 0 / 0.5 s | Stream only: gap between single spawns. |
| Delay Between Groups | 3.5 / 3 / 2 s | Gap between groups. |
| Group Entry Offsets | (-4, 4, 0) / (4.5, -4.5, 0) / (-2, 2, 0) | Sideways entry position of group 1, 2, 3... (cycled). |
| Delay Before Wave | 2 / 2.5 / 2.5 s | Pause before the wave (after the previous one clears). |
| Clear Timeout | 15 / 15 / 20 s | Safeguard after the last spawn. |

Placeholder settings added in this pass. Values are the scene defaults. Change them on the scene objects or in
the prefabs in `Assets/Prefabs/LevelWaves/`. Rebuilding the scene from the menu resets them.

| Object > component | Setting (default) | Meaning |
| --- | --- | --- |
| `TEST_PlayerShip` > `TestPlayerShip` | Speed (9.6), Acceleration (75), Deceleration (100), X/Z Limits (±9 / -8..0), Fire Cooldown (0.2), Muzzle Flash Seconds (0.06) | Handling. Higher deceleration = less sliding. |
| `TEST_PlayerShip` > `TestPlayerHealth` | Max Hits (3), Invulnerability Seconds (1), Hit Flash Seconds (0.12), Blink Rate | Damage rules (proposal values). |
| Ships > `TestShipBanking` | Max Bank Degrees (player 30, enemies 32), Full Bank Speed, Max Yaw Degrees (player 0, enemies 0), Full Bank Turn Rate (enemies: 0.7 × their turn rate), Responsiveness | Visual tilt only. Enemies roll into turns. |
| Enemy prefabs > `TestEnemyMover` | Enemy Class, Pattern (fighter / interceptor: Curved Pass, heavy: Side Sweep), Speed (heavy 1.7, fighter 2.6, interceptor 4), Acceleration (1.5 / 3 / 5), Turn Rate (55 / 110 / 150 °/s), Turn Acceleration (110 / 300 / 450 °/s²) | Speed class and turning limits. |
| same > Route shape | Curve Width (2.6), Flank Width (2.2), Sweep Width (6), Sweep Z (6.5), Dive Z (4.6), Retreat Z (7), Retreat Shift (1.6) | Pattern sizes (scaled per wave by Path Width Multiplier). |
| same > Safety | Straight Below Z (1.5), Line Up Band (1.8), Lane Limit X (8), Max Lifetime (16 s), Half Width (heavy 1.1, fighter 0.75, interceptor 0.6) | Keep Straight Below Z above the player's zone (z ≤ 0) and Dive Z well above it. |
| Enemy prefabs > `TestEnemyHealth` | Max Health (fighter 20, heavy 30, interceptor 10), Hit Flash Seconds, Explosion Scale | Placeholder health (player shots do 10). |
| `PLACEHOLDER_FX` > `TestFxManager` | Sounds (`TEST_SoundBank`); Master (0.8), Weapons (0.6), Impacts (0.7), Explosions (0.6), Engines (0.7), Warnings (0.55) volumes; Duck Under Warnings (0.45); Pitch Variation (0.025); Max Voices (10); Max Per Sound (3); Max Explosions (2); Explosion Ducking (0.35); Pan Width (12) / Max Pan (0.6) | Mix groups and overlap limits (temporary until the team has an audio system). Master x Engines also scales the player engine, flybys and rocket motor. The balance inside each group is fixed in `TestFxManager.Volume()`. |
| `PLACEHOLDER_FlightAudio` > `TestFlightAudio` | Engine Volume (0.3), Idle Level (0.55), Idle / Full Pitch (0.95 / 1.05), Idle / Full Cutoff (1600 / 7000 Hz), Acceleration Boost (0.35), Spool Up / Down Seconds (0.15 / 0.45), Fade Seconds (0.4) | Player engine response. |
| same > flybys | Flyby Volume (0.45), Pass Distance (4.5), Full Volume Distance (2.5), Hearing Distance (14), Max Flybys (2), Min Gap Seconds (0.6), Doppler Amount (0.03), Doppler Speed (6), Pan Width (8) / Max Pan (0.8), Far / Near Cutoff (1800 / 9000 Hz) | Which passes get a flyby and how they are positioned. |
| `TEST_SoundBank` (asset in `Assets/Audio/LevelWaves/`) | Clip lists per sound, Player Engine Loop, Flybys, Flyby Pass Seconds (1.4) | Swap clips here without code changes. |
| `TEST_Difficulty_Encounter01` (asset in `Assets/Prefabs/LevelWaves/`) > each wave | Speed Multiplier (1 / 1.05 / 1.1), Turn Rate Multiplier (1 / 1 / 1.1), Path Width Multiplier (1 / 1.1 / 1), Alternate Flanks (off / on / on), Group Patterns (Side Sweep, Curved, Pass And Retreat / Dive Regroup, Side Sweep, Pass And Retreat / Dive Regroup, Curved, Side Sweep), Keep Own Pattern (- / - / Heavy) | Movement per wave. Waves beyond the list reuse the last entry. |
| same > reactions | React After Distance (- / 2.5 / 2.5), Lane Seek Distance (0 / 1.5 / 1.2), Flanking Classes (- / - / Interceptor), Flank Offset (2.4), Tracking Seconds (0.6), Max Reposition (4), Pursuit Classes (- / Interceptor / Fighter), Pursuit Seconds (0.8) | Limited reactions to the player. Pursuit is short and then commits to a lane. |
| same > variation | Variation (0.15 / 0.2 / 0.2) | Per-run spread: group width ±Variation, dive depth ±2×Variation units, ship speed ±Variation/3. 0 = identical runs. |
| same > aimed shots | Aimed Shooter Every Nth (4 / 3 / 2), Telegraph Seconds (1 / 0.9 / 0.85), Shot Speed (7 / 8 / 8.5), Shots Per Burst (1 / 3 / 4), Burst Interval (0.14 / 0.14 / 0.13), Lead Factor (0 / 0.35 / 0.5), Max Lead (1.5 / 1.5 / 1.8) | Gun attacks. Interceptors fire a single shot with +0.25 lead and a shorter warning (×0.8, at least 0.6 s). |
| same > rockets | Rocket Classes (- / - / Heavy), Group Leader Rocket (off / on / off), Rocket Warning Seconds (- / 1.1 / 1) | Which ships carry the homing rocket and how long the red warning lasts. |
| same > all waves | Min Escape Gap (2.6); Max Shots In Flight (2, a burst counts once), Min Seconds Between Attacks (1.2), Min Fire Distance (7), Fire Window Z (3-8.5), Fire Window X (9.5), Aim Lock Seconds (0.3), Hold Fire While Player Recovers (on); Max Rockets In Flight (2), Min Seconds Between Rockets (3), Min Rocket Distance (8); Fixed Seed (0 = new variation every run) | Escape lane, attack and rocket fairness, repeatability. |
| `PLACEHOLDER_AttackDirector` > `TestAttackDirector` | Profile, Player, Player Health, Encounter, Shot Prefab, Rocket Prefab | Wiring only; point Profile at another asset to try a different difficulty. |
| `TEST_EnemyShot` > `TestEnemyBullet` | Lifetime (4), Area | Enemy shot clean-up. |
| `TEST_HomingRocket` > `TestHomingRocket` | Launch Speed (3), Max Speed (6.5), Acceleration (4), Turn Rate (70 °/s), Arming Seconds (0.35), Homing Seconds (2.6), Lifetime (5), Beacon Rate (7 Hz), Loop Volume (0.5), Far Level (0.35), Motor Fade In (0.6 s), Area | Rocket flight and burn-out. Keep Max Speed below the player's 9.6 and Turn Rate low so a side-step evades it. |
| `TEST_HomingRocket` > `TestEnemyHealth` / `TestContactDamage` | Max Health (10) / 1 hit | The rocket can be shot down; it costs one hit on contact. |
| `Main Camera` > `TestCameraShake` | Shake Enabled (on), Max Offset (0.3), Frequency (24), Duration (0.35) | Optional shake. |
| `PLACEHOLDER_TestHarness` > `EncounterTestHarness` | Damage Shake (0.55), Defeat Shake (0.9) | Shake strength per event (0-1, squared). |
| `PLACEHOLDER_HUD` > `EncounterDebugHUD` | Minimum Scale (0.6), Damage Flash (on), Show Debug Counters (off) | HUD readability / feedback. |
| `PLACEHOLDER_EntryWarnings` > `TestEntryWarnings` | Marker Z (8.2), Colour, Blink Rate | Warning strip position and look. |
| `PLACEHOLDER_Background (decor only)` > `TestBackdropPreset` | Preset (Planet And Moon / Sun), light angles per preset | One backdrop at a time. |
| Integrated demo: `RailPath (Abel's LevelCamera)` > `LevelCamera` (Abel) | Camera Speed (1.5), Key Frames (`KeyFrame0` at the start, `KeyFrame1` 1200 units ahead) | His component; the demo builder only sets these values. Camera Speed is in units per second (it uses `Time.deltaTime`). |
| Integrated demo: `RailRig` > `RailPathFollower` | Path (`RailPath`) | Rig follows the path with its starting offset, syncs physics; R resets path and rig. |
| Integrated demo: `PLACEHOLDER_TestHarness` > `DemoFrameRateCap` | Frames Per Second (60) | Needed while Abel's controller moves per frame. |
| Integrated demo: `PLACEHOLDER_TestHarness` > `EncounterTestHarness` | Disable On Defeat (Abel's `PlayerController`), On Restart (rail reset) | Teammates' components switched off on defeat and back on at restart. |
| Integrated demo: Player > `PlayerController` (Abel) | Speed (0.15 per frame) | His component; only the value is set by the demo builder. |
| Integrated demo: Player > `TestPlayerShip` | Handle Movement (off) | Weapon only. |
| `PLACEHOLDER_HUD` > `EncounterDebugHUD` | Mission Title, Objective, Cleared Title, Cleared Message | Intro banner, objective line and ending text (empty in the test scene). |

---

## Dependencies and integration steps

Done on Nov 2 (integrated demo): Leonardo's model as the enemy visual, Abel's player controller, a rail rig
for spawns and bounds. Still open below.

1. **Enemies (Leonardo):** when his full enemy prefab exists, drag it into a wave's *Enemy Prefabs*. Requirements:
   it moves on its own, and when killed it is `Destroy`ed or `SetActive(false)`. Nothing else is needed.
   If his enemy uses spawn-time setup, that stays in his scripts (e.g. `Start`). Contact damage moves to his/the team's damage system.
   Optional: implement `IWaveSpawnReceiver` to receive the enemy's formation slot (used by the test movers to pick
   their sweep direction). `TestEnemyMover` is a stand-in only; nothing in `WaveEncounter` depends on it.
   The difficulty profile, attack director and enemy shot are test-only too. If Leonardo's AI wants per-wave
   difficulty, it can read the same `waveIndex` from `WaveSpawnInfo` and use its own settings; the fairness rules
   documented above (telegraph, aim lock, shot caps, minimum distance) are a suggested starting point.
2. **Player (Abel):** done in the demo: his `PlayerController` moves the ship. The branch `movement-rail-system`
   is merged into my feature branch only (not `main`); ideally it reaches `main` first. Open items for his code:
   - speed is per frame, not per second (the demo holds 60 fps until then);
   - his `LevelCamera` + `KeyFrame` now drive the demo rail (done Nov 2, without changing his files): the demo
     builder creates two keyframes and a `RailPath` object with his `LevelCamera`, and `RailPathFollower` moves the
     `RailRig` with it and calls `Physics.SyncTransforms()` so his CharacterController isn't left behind. If he
     later moves the real camera himself, `RailPathFollower` can be deleted.
   Spawns and bounds already follow the rig: `WaveEncounter` *Bounds Space* plus `SpawnOrigin` and *Spawn Parent*
   under the rig.
3. **Damage/health (team decision):** replace `TestPlayerHealth` with the real player health. To keep defeat working,
   it only has to call `WaveEncounter.FailEncounter("player defeated")`.
4. **Weapons (Taha):** his projectiles only need to destroy enemies; remove `TestProjectile`/`TestEnemyHealth` from real enemies.
   The test bolt/muzzle/spark/explosion look can be reused or dropped; it is not tied to the wave system.
5. **Models/effects/audio:** the `TEST_` ships and sounds are stand-ins. Real enemy prefabs bring their own visuals;
   `TestShipBanking` and the exhaust can be copied onto them if the team likes them. `TestFxManager` and
   `TestFlightAudio` should be replaced by the team's audio/VFX approach (for example an AudioMixer with
   engine / weapons / impacts / explosions groups). The `LW_*.wav` clips are CC0 and can be reused anywhere; keep
   `CREDITS.md` with them. Flybys only need an enemy's position and velocity: `TestFlightAudio` reads them from
   `TestEnemyMover.Active`, so real enemies would need an equivalent list.
   Movement ideas for Leonardo (optional): speed classes, turn-rate caps, a straight descent near the player,
   reactions that read the player once or track briefly, and a free-lane check before any reaction.
6. **UI/score/game flow (Ronit):** subscribe to the events (including `onGroupIncoming` if the real HUD wants
   entry warnings) and read `CurrentWaveIndex`, `AliveCount`, `DestroyedCount`, `EscapedCount`, `AllEnemiesDestroyed`;
   then delete `PLACEHOLDER_HUD`, `PLACEHOLDER_UICamera`, `PLACEHOLDER_EntryWarnings` and `PLACEHOLDER_TestHarness`.
   Decide whether escaped enemies should have a penalty. Readability lesson for the real UI: keep text out of the
   post-processed camera (use an overlay UI camera without post-processing, or Screen Space - Overlay), and avoid
   FXAA on a camera that also draws UI.
7. Neither scene is in Build Settings (I did not change them). The local demo build passes the demo scene
   explicitly (`Solar Divers > Level Waves > Build Integrated Demo (Windows)`); the team can add it to Build
   Settings later.

Code notes: the wave code is in its own assembly (`SolarDivers.LevelWaves.asmdef`) so it can have automated
tests. It is auto-referenced, so teammates' normal scripts can use `WaveEncounter` directly.
It only depends on Unity, uGUI and the Input System package (for the placeholder ship and the R key).
The scene builder and tests also reference URP (camera stack, volume profile), which the project already uses.

---

## Validation

### Audio pass 2 (Nov 2)

Unity 6000.6.0f1, actual project folder, Unity closed first. Automated results only; the manual listening checks
are below.

- **Scenes rebuilt.** Ignoring object IDs, the only changes in both scenes are the `TestFxManager` mix settings.
  Two prefabs (`TEST_Enemy_Weaver`, `TEST_PlayerShip`) were regenerated with new object IDs and identical
  content. The rocket prefab gained its three new motor fields. Unity rewrote 4 render/project settings files
  during the build; they were restored and are not part of the change.
- **PlayMode tests: 70 total, 66 passed, 0 failed, 4 skipped** (the opt-in capture and performance tests). 193 s.
  New (`AudioMixTests`):

| Check | Result |
| --- | --- |
| Each volume group (weapons, impacts, explosions, engines, warnings) changes only its own sounds; an explosion during a warning is lowered | Passed: explosion 0.360 alone, 0.198 during a rocket warning |
| Continuous fire (5 s at 5 shots/s): overlap capped, several variants, never the same twice in a row, restrained pitch | Passed: at most 3 ringing, pitch 0.976-1.024, all 4 variants used |
| Rocket motor: heard from the launch, fades in under the launch blast, and stops when the rocket expires, is shot down, is cleared by R, or detonates on the player (detonation sound plays) | Passed: motor 0.010 at launch, 0.149 after 0.75 s |
| Integrated demo: R silences everything except the engine fading back in; after defeat everything falls silent; after completion only the engine remains and no rockets are left; unloading (Stop) leaves no sound | Passed |
| Mixed output of the demo with deliberately heavy combat (continuous fire, bursts, kills, warning, two rockets, hull hit, rocket blast, player destroyed), read from the audio listener | Peak **-4.5 dBFS**, no clipping |
| Recorded sounds (updated rules): credited sources, mono, peak ≤ -0.7 dBFS, no end clicks; cannon report ≥ 0.3 s but ≤ 0.6 s; enemy gunfire brighter than every player shot; explosions 1.5-2.2 s; player destroyed ≥ 1 s longer; hull hits brighter than the cannon and duller than enemy hits; variations present | Passed (cannon 0.78-1.28 kHz, enemy gun 1.70-1.82 kHz, hull hits 1.97-2.05 kHz, enemy hits 3.2-4.9 kHz) |

Earlier audio checks still pass: explosions duck each other (0.36 then 0.27, at most 2), engine pitch and
smoothness, flybys, and restart cleanup in the test scene.

**Windows build:** succeeded (109 MB, 0 errors), at
`C:\Users\hp\Documents\COMP3770-Group5-Work\Builds\SolarDivers_WaveDemo\SolarDivers_WaveDemo.exe`. A 25 s windowed
run showed no errors or exceptions in the player log. Wave 1 started, and the idle ship was defeated after 9.4 s
with nobody at the controls. **Nobody has listened to the build yet.**

Manual listening checks (not done; tests can't judge whether a sound is convincing):

- [ ] Cannon: punchy and heavy, not tiring after 30 s of continuous fire; clearly different from enemy gunfire.
- [ ] Explosions: blast, then rumble and debris; weighty without masking the rocket lock tone.
- [ ] Hull damage sounds like the ship being struck, not like firing; player destruction is clearly bigger.
- [ ] Rocket: the launch blast, then a motor you can track as it approaches, which stops when it explodes or expires.
- [ ] Headphones and laptop speakers: nothing harsh (the land-mine recording is a camcorder recording and the
      cannon recording clips briefly at the shots).
- [ ] Compare `AB_*.wav` in `C:\Users\hp\Documents\COMP3770-Group5-Work\AudioComparison\` with what the game plays.

### This round: combat, movement and rail pass (Nov 2)

Unity 6000.6.0f1, actual project folder, Unity closed first, batch mode with graphics. Automated results only;
manual checks are listed separately below.

- **Compile:** 0 errors. Same 4 unused-field warnings in Abel's files, still untouched.
- **Scenes rebuilt** by `WaveTestSceneBuilder.BuildDemoFromCommandLine`. Unity rewrote 4 render/project settings
  files during the runs; they were restored (`git checkout`) and are not part of the change.
- **PlayMode tests, final full run: 65 tests, 61 passed, 0 failed, 4 skipped** (the opt-in `[Explicit]` capture
  and performance tests, run separately below). 163 s.
- The first full run had **2 failures**, both fixed:
  - the burst-lead test measured the lead after the player had stopped (test bug);
  - the new rocket engine loop "ended with a click" because a seamless loop doesn't end at zero. It's now
    exempt from that check and has a loop-seam check instead.

| Check (new this round unless marked) | Result |
| --- | --- |
| Difficulty profile: per-wave patterns, variation, bursts, lead, rocket carriers, warnings and global caps as listed in the settings table | Passed |
| Whole encounter, attacks per wave (bursts count once) | Gun attacks 3 / 3 / 6, shots 3 / 9 / 15, rockets 0 / 3 / 5. At most 2 gun attacks and at most 2 rockets busy. Shots fly along the locked line, nothing fires after completion, R removes all shots and rockets |
| Burst | 4 shots on one locked line; aim 1.80 units ahead of a player moving at 6 units/s (cap 1.8) |
| Rocket limits | Worst turn 70 °/s (limit 70), top speed 6.50, removed after 6.0 s (lifetime 5 s + flight check) |
| Rocket evasion | Hits a player who stays still (closest 1.05); a late side-step makes it miss (closest 3.49) |
| Rocket caps | At most 2 at once, at least 3 s apart, never launched closer than 8 units |
| Variation | 12 / 12 seeds gave different paths, all within the bounds; the same seed repeats exactly |
| Pursuit | Follows for at most 0.8 s, then commits to the last lane seen |
| Escape lane, whole encounter with the player weaving (previous check, new waves) | Narrowest free lane 3.34 units (minimum 2.6); 6 flanks, 14 retreats; longest on screen 15.4 s; 0 timed out |
| Movement limits (previous check) | Worst turn 165 °/s = 100 % of the class limit; lowest retreat z 3.14; 30 vs 144 fps 0.037 units apart |
| Integrated demo with Abel's `LevelCamera` rail | 189 components and 13 renderers checked. Fighter destroyed by 2 shots. No-input run 73.5 s with all 45 escaped. At camera speed 3, the rail moved 220 units with all enemies inside the bounds and 0 timed out. Defeat and 3 restarts reset rail, rig, shots, rockets, effects and sounds |
| Sounds (previous check plus new clips) | All credited, mono, no clipping. Enemy gun 0.22 s, ~1.9-2.1 kHz (distinct from the player's ~0.7-0.9 kHz cannon); rocket launch 0.85 s; rocket loop 3.3 s with a seamless seam |
| Engine / flybys / explosion ducking (previous checks) | Pitch 0.950 → 1.045; 3 flybys, pitch 0.980-1.019, at most 2 at once; explosions 0.36 then 0.27 |

Opt-in runs (`[Explicit]`, with `SOLAR_CAPTURE_DIR`), outside the repository:

- **Enemy appearance** (`CaptureEnemyAppearance`): top-down as in the demo, Leonardo's scene angle and side view,
  plus model info. Confirms the plain-grey look comes from the untextured `Atlas` material (see "Integrated demo").
- **Runtime screenshots** (`CaptureDemoScreens`): the intro, waves 1-3 (the red rocket lock line in wave 2 and a
  rocket with its smoke trail in wave 3 are visible) and the ROUTE CLEAR panel.
- **Performance** (`MeasureDemoPerformance`, Editor, 1920×1080, demo capped at 60 fps, GeForce 940MX laptop):
  2,285 frames, mean 18.8 ms (53 fps), median 16.7 ms, 95th percentile 22.0 ms, worst 169.7 ms (a single
  hitch). The Editor adds overhead, so the build should be faster, but the build's frame rate was **not measured**.

**Local Windows build:** succeeded (`WaveTestSceneBuilder.BuildWindowsDemo`, demo scene only, 108 MB, 0 errors,
5 warnings, 40 s). Path: `C:\Users\hp\Documents\COMP3770-Group5-Work\Builds\SolarDivers_WaveDemo\SolarDivers_WaveDemo.exe`
(outside the repository). Started windowed for 25 s: wave 1 started, no errors or exceptions in the player
log. With nobody at the controls the idle ship was defeated after 9.5 s (11 s in the previous build), which is
the expected rule. **Keyboard play in the build was not tested.**

Manual checks for this round (not done; tests can't judge feel or sound):

- [ ] Rocket warning (red pulse, red line, low tone) is noticed in time, and the rocket looks different from shots.
- [ ] A late side-step reliably beats a rocket; shooting one down feels possible but not required.
- [ ] Bursts are readable: you can see where the locked line points and step out of it.
- [ ] Wave 1 feels like an introduction, wave 2 combines, wave 3 is harder but fair.
- [ ] Runs feel slightly different, but never unfair.
- [ ] Enemy gunfire, rocket launch, rocket engine and rocket blast sound right on speakers and headphones.
- [ ] The rail moves smoothly at camera speed 1.5 and doesn't make dodging harder.
- [ ] R during a rocket warning, with rockets in flight, and after defeat leaves nothing behind.
- [ ] Keyboard play and frame rate in the Windows build.

### Previous round (integration, Nov 2)

**Editor: Unity 6000.6.0f1 (the team version), run in the actual project folder** after merging `main` and
`movement-rail-system` into `khaled/level-waves-prototype` (Nov 2). Unity was closed first; batch mode with the
graphics device on (without one, URP cannot create render textures and the Editor crashes in rendering).

- **Compile:** all project scripts compile, including Leonardo's and Abel's. 0 errors; the only warnings are
  4 unused fields in Abel's unfinished `KeyFrame.cs` / `LevelCamera.cs` (left untouched).
- **Scenes rebuilt by `WaveTestSceneBuilder.BuildDemoFromCommandLine`:** the test scene, shared prefabs,
  materials and sound bank came out identical except the regenerated enemy and player prefabs and the test
  scene. No team file or project setting was changed by the build.
- **PlayMode tests: 55 passed, 0 failed, 2 skipped.** The two skipped are the opt-in screenshot tests
  (`[Explicit]`). Total run 154 s.

Integrated demo (`IntegratedDemoTests`, new):

| Check | Result |
| --- | --- |
| Demo scene opens with no missing scripts, materials or unsupported shaders; Abel's `PlayerController`, `CharacterController` and `PlayerCollision` are on the player (tagged `Player`), the test weapon does not move it; every wave enemy nests Leonardo's `Enemy_model.prefab` and has a trigger hitbox; heavy, fighter and interceptor all appear; camera, player and bounds use the rail rig | Passed (182 components, 13 renderers checked) |
| The player's weapon destroys an enemy built on Leonardo's model, with hit and explosion effects | Passed: fighter destroyed after 2 shots |
| Whole encounter without input: waves start in order, completion fires exactly once, every enemy leaves on its own, intro banner, objective and **ROUTE CLEAR** panel shown | Passed: 45 spawned, 45 escaped, 0 timed out, 70.6 s |
| Defeat after 3 hits fails the encounter, stops spawning and switches off Abel's controller and the weapon; then 3 restarts in a row each reset hull, controls, enemies, player and enemy shots, effects, sounds, flybys and the player's position (CharacterController teleported cleanly), and waves spawn again | Passed |
| Moving rail: the rig moves at 3 units/s for the whole encounter; spawns stay at the top relative to the rig, enemies stay inside the bounds and all leave, the player stays with the rig, R puts the rig back | Passed: rig moved 213 units, widest \|x\| 8.4, 0 timed out |
| Play / Stop / Play: unloading the scene leaves no enemies, effects, sounds or frame-rate cap behind; a second load starts fresh | Passed |

Movement and sound (`MovementAndAudioTests`), test scene:

| Check | Result |
| --- | --- |
| Every class × pattern × wave × entry position: turn rate never above the class limit, turn rate builds up gradually, no turning and ≤ 30° from straight down near the player, stays inside the play area, retreats stay high, every ship leaves | Passed. Worst turn 165°/s = 100 % of the interceptor's limit; lowest retreat z 3.14; longest path 20.2 s |
| Whole encounter with the player weaving: free escape lane, spawns at the top, every enemy leaves | Passed. Narrowest free lane **3.60 units** (minimum 2.6; player hitbox 1.2); 6 flanks, 15 retreats; longest on screen 15.1 s; 0 timed out |
| Frame-rate independence (30 vs 144 fps after 5 s) | 0.037 units apart |
| Flankers track briefly then commit beside the player's last position; lane-gap maths | Passed |
| Recorded sounds credited, mono, no clipping, no end clicks, controlled bass, distinct families; seamless engine loop | Passed (autocannon 4-6 % below 60 Hz, 0.12-0.13 s ring; explosions 1.1 s, 5-11 % below 60 Hz; impacts 3.2-4.9 kHz RMS) |
| Explosions duck and are limited | 0.36 then 0.27 volume, at most 2 at once |
| Engine follows thrust smoothly and stops on restart / disable; winds down on defeat | Pitch 0.95 → 1.044, largest change per frame 0.0006 |
| Flybys only for close passes, limited, subtle Doppler, stop on restart | 3 flybys, pitch 0.980-1.019, at most 2 at once |

Earlier checks still passing: aimed-shot fairness (6 attacks, 6 shots, at most 2 at once), player speed (crossing
1.95 s), damage, invulnerability, defeat, restart, formations, wave order, missing prefab, timeout, effects
cleanup, HUD outside post-processing, scenery.

Problems found and fixed during this round (by the tests):
- **Rail:** Abel's CharacterController did not follow a moving parent (left 242 units behind), because physics
  transforms are not auto-synced. `TestRailScroll` now moves first in the frame and calls
  `Physics.SyncTransforms()`; his real rail camera will need the same.
- **Orbiting:** slow ships could circle a waypoint inside their turning circle, and fast ones could skip a
  retreat. Waypoints are now passed when far off the nose, the retreat point is kept until the ship turns to it,
  and no waypoint is chased for longer than one full circle. Final turns end above each ship's own line-up height.
- **Presentation:** in the first demo screenshots, the objective panel covered the enemy entry area, and the
  engine exhaust on Leonardo's model sprayed toward the camera. The panel moved to the bottom left and the
  exhaust is now on the ship root.

**Runtime screenshots** of the integrated demo (actual renders, outside the repository; re-create with
`IntegratedDemoTests.CaptureDemoScreens` and `SOLAR_CAPTURE_DIR`): intro banner, wave 1 to 3 with Leonardo's model,
the aim line and flanking interceptors, and the ROUTE CLEAR panel.

**Local Windows build (previous round):** succeeded (Windows 64-bit, demo scene only, 107 MB, 0 errors). Started windowed for 25 s: Direct3D 11 on the GeForce 940MX, wave 1 started, no errors or exceptions in the player log. With nobody at the controls the idle ship was defeated after 11 s, which is the expected rule. Keyboard play in the build was not tested.

Manual checks still needed (not done: nobody has played this build yet; automated tests cannot judge feel or sound):

- [ ] Abel's controller feels responsive at 60 fps; speed 0.15 per frame (~9 units/s) is right.
- [ ] Leonardo's model reads well from above; its nose direction looks right in motion.
- [ ] The intro, wave names, objective and ending make sense to someone seeing it for the first time.
- [ ] Shots hit what they visibly touch; contact damage feels fair with the trigger hitboxes.
- [ ] Engine, flybys, autocannon, impacts and explosions on laptop speakers and headphones (no harshness).
- [ ] Difficulty: wave 1 relaxed, wave 3 hardest but fair.
- [ ] R mid-wave, R after defeat, Stop then Play again.
- [ ] The Windows build starts, plays and quits; frame rate on the demo machine.

---

## Next tasks

- [x] Use Leonardo's enemy model and Abel's player controller in an integrated demo scene (Nov 2).
- [ ] Agree hit count, invulnerability time and escape penalty with the team (currently: 3 hits, 1 s, no penalty).
- [ ] When Leonardo's enemy has its own collider/health/attacks/movement: put it straight into the waves'
      *Enemy Prefabs* (it only needs to move itself and be destroyed or disabled when killed), and remove the
      test mover/health from the demo enemies.
- [ ] When Taha's weapons exist: replace the test weapon (`TestPlayerShip` with *Handle Movement* off) and
      `TestProjectile`; enemies only need to take damage and be destroyed.
- [x] Drive the demo rail with Abel's `LevelCamera` and `KeyFrame` (Nov 2, through `RailPathFollower`).
- [x] Bursts, limited lead, warned homing rockets, short pursuit, dive-and-regroup and per-run variation for the
      test enemies (Nov 2).
- [ ] Ask Leonardo for the texture (or colours) for the `Atlas` material and the model's source and licence. The
      FBX references no texture and none is committed, so it renders plain grey.
- [ ] Remove `DemoFrameRateCap` once Abel's `PlayerController` scales speed by `Time.deltaTime`.
- [ ] When Leonardo's AI and Taha's weapons exist, compare them with the test bursts and rockets. Keep the
      fairness rules (warning, lock, caps, minimum distance, turn-limited homing) or move them into the real systems.
- [x] Audio pass 2: recorded CC0 gunfire, explosions, hull damage, player destruction, rocket launch and motor; volume groups (Nov 2).
- [ ] Listen to the audio pass in the build and adjust the group volumes (manual checks under Validation).
- [ ] When Ronit's audio system exists, move the `LW_*` clips and the group volumes there (e.g. AudioMixer groups) and remove `TestFxManager` / `TestFlightAudio`.
- [ ] Trigger encounters from rail keyframes instead of starting on Play.
- [ ] Hook score/UI/game-flow events with Ronit, then delete the test HUD and harness from the demo.
- [ ] Lay out the three alternative routes to Earth using encounters as building blocks (later goal, not started).

### Boss: pending tasks (not started in code)

Not required for this milestone (no Issue, milestone or repository document asks for it), so the wave
encounter came first. Order of work, smallest playable version first:

1. `BossEncounter` component: phases (turrets, beam, core), `onBossDefeated` / `FailEncounter` hooks,
   restart support. Trigger it from the demo encounter's `onEncounterCompleted`.
2. Destructible turrets as child objects with their own health (needs the team damage system, or the
   test health until then) and telegraphed aimed shots (reuse `TestAttackDirector` fairness rules).
3. Warned beam: a lane warning (like the aim line) for ~1.5 s, then a short damaging beam with a dodge lane.
4. Exposed-core window after the turrets are down; core damage ends the level.
5. Model and art for the Marquis's frigate (team decision; none exists yet), boss UI with Ronit.
6. Tests: phases advance once, no unavoidable damage, defeat/restart reset the boss.

Dependencies: real weapons (Taha) and damage system; enemy projectile/attack conventions (Leonardo);
rail stop/hold for the boss arena (Abel); boss health bar and ending flow (Ronit).

### Boss proposal (PROPOSAL, not implemented)

A first boss built from three readable mechanics:

1. **Turrets** - 2-4 destructible turrets on the boss hull, each with its own health, firing simple
   aimed shots. Destroying them reduces incoming fire and is required before the core opens.
2. **Warned beam attack** - a telegraphed beam: a thin warning line shows the lane for ~1.5 s, then a wide
   damaging beam fires for ~1 s. The player always has time to dodge; the warning time is tunable.
3. **Exposed core** - when all turrets are destroyed (or on a timer per phase) armour opens and the core is
   damageable for a short window, then closes again. Defeating the core ends the level.

Possible reuse of the wave system: escort waves between boss phases via another `WaveEncounter`,
`onEncounterCompleted` to trigger the boss entrance, and `FailEncounter` when the player is defeated.
