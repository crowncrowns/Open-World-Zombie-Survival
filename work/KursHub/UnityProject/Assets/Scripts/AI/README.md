# Enemy AI

Shared actor/faction/damage/noise contracts, the exported ZombieMaleActor prefab, lab navigation, migration and merge checks are documented in [Docs/ActorIntegration.md](../../../Docs/ActorIntegration.md). Read it before integrating new monsters or NPCs. The current character lab includes the source-scene zombie and stealth tests.

Add `EnemyBrain` to an NPC on a baked NavMesh. It requires/adds `NavMeshAgent` and, on first addition in the Editor, adds a configurable `MeleeAttack` if no attack exists. For a Y-axis CapsuleCollider, initial agent dimensions, base offset, and eyes are fitted to the capsule. Check these values for other meshes/scales. Do not combine a driving NavMeshAgent with a non-kinematic Rigidbody. Exclude moving actors from the navigation bake with `NavMeshModifier` → Ignore From Build.

Add `CombatTarget` to an intended target. The player uses the full `Health` component with HUD and `PlayerLifeCycle`; existing simple targets may keep `DamageableHealth`. Both implement the single contract in `Combat/IDamageable.cs`: `IsDead` and `ApplyDamage(DamageInfo)`. CombatTarget prefers Health if both components are present, so attacks affect the player's visible reserve and lifecycle rather than a second hidden pool. DamageableHealth retains its public IsAlive and TakeDamage(float) for existing UnityEvents. Set the target's Aim Offset inside its collider. Targets at zero health are ignored. ActorIdentity now supplies directed hostility masks; unconfigured legacy attackers keep their previous permissive behaviour. New actors must assign a faction.

## Inspector

EnemyBrain: patrol radius around its initial spawn, patrol/maximum speeds, pauses, vision range and full field of view angle, eye offset, obstruction layers, hearing distance and threshold, reference distance, attenuation behind obstacles, uncertainty, search time, and visual memory radius. Chase uses Maximum Speed; actual acceleration/corners remain governed by NavMeshAgent. Runtime State/Search Center/Search Radius aid debugging; select the NPC for gizmos.

Hearing loudness is gameplay data, not decibels. Received loudness = emitted loudness / (1 + (distance/referenceDistance)^2), optionally multiplied by Occluded Noise Multiplier. Both emitter range and listener hearing range apply. Heard uncertainty radius = Minimum Uncertainty + distance × Uncertainty Per Metre; the estimated center is displaced by up to 80% of that radius. Only this estimate survives the event. The NPC searches random reachable points in the estimated area. It never follows an unseen emitter's Transform. Losing sight searches the last seen location until Search Duration expires, then returns to its spawn patrol.

MeleeAttack: range, cooldown measured from attack start, stop delay, windup, recovery, damage. On Started/On Impact can trigger animations or effects. Damage occurs once after windup and rechecks live range, target health, and obstacles. Movement remains stopped through recovery. An attack can miss if its target moves out of range or behind cover. The reusable ZombieMaleActor prefab supplies the current model/animations; other actors can assign their own EnemyAnimationBridge parameters.

## Gameplay noise versus audio

`AudioSource.Play()` alone is **not heard by this AI**. Audio playback and gameplay hearing are independent. Add `NoiseEmitter` and call its `Emit()` from gameplay, an animation event, or a UnityEvent; configure Loudness and Range. Alternatively:

```csharp
ZombieSurvival.AI.GameplayNoise.Emit(transform.position, 8f, 90f, gameObject);
```

The optional emitter is used only for ignoring one's own noise. Hearing does not retain a reference to it. `PlayerNoiseEmitter` uses FirstPersonController.IsSprinting and the common damage contract. It emits footsteps only while grounded, alive and actually moving; running is louder and has greater range. It does not create audio clips. Teleports over five metres in one frame do not generate footsteps.

## Other attacks

Derive from `EnemyAttack`, override `ApplyEffect(CombatTarget target)` for a shot, spell, or other effect. Override `CanReach`/`CanStart` for extra conditions if necessary, retaining range/obstruction checks as appropriate. Attach one or more attacks to the same GameObject as EnemyBrain; it tries ready attacks in component order. Remove the default MeleeAttack if the NPC should only use another attack. Attack components are cached in Awake; configure them before Play.

## Scene navigation

The existing Enemy in SampleScene uses a local `Enemy Navigation` NavMeshSurface, Physics Colliders geometry, humanoid agent type 0. Its 400 × 200 × 400 metre volume is centered horizontally on the original Enemy spawn, from ground Y=-20 to Y=180. This is a local playable slice, not a bake of the entire 3 km terrain. The player can leave it; the NPC can only follow a partial path to the reachable boundary. While that player remains visible it stays in Chase; losing sight or exceeding vision range starts the search timeout, followed by patrol. To expand, resize/reposition the Surface volume (or select All collection), then Bake in its Inspector and save the scene/data. Re-bake after changing static buildings or terrain. Dynamic actors are excluded from the bake.

## ZombieMale animations

The FREE Zombie Male AAB models contain no AnimationClips. The active controller now uses real animations from the separately imported `Assets/Zombie` package. Editable working copies in `Assets/Animations/ZombieMaleImported` preserve the original FBX/import settings:

- `Z_Idle.anim`: from `Assets/Zombie/Animations/Zombie@Z_Idle.FBX`, 2.6667 seconds, loop.
- `Z_Walk_InPlace.anim`: from `Zombie@Z_Walk_InPlace.FBX`, 2 seconds, loop. The in-place shamble is used instead of translating Walk/Walk1 variants.
- `Z_Run_InPlace.anim`: from `Zombie@Z_Run_InPlace.FBX`, 1.5333 seconds, loop.
- `Z_Attack.anim`: from `Zombie@Z_Attack.FBX`, trimmed to frames 0–14 (0.4667 seconds), no loop. The original full second contains two alternating swipes; the working copy keeps the first swipe so one melee action gives one visible strike and one damage application.

All four working clips are Humanoid; source clips use Zombie1Avatar, while the scene Animator retains ZombieMale_AABAvatar for retargeting. Root motion is disabled and root orientation/position are baked into pose in the copies. The controller's IK Pass is enabled; built-in state Foot IK is disabled to avoid double correction with HumanoidGrounding. Source import warnings report discarded intermediate ribcage rotation and finger scale tracks; source files were not modified to suppress these warnings.

`Assets/Animations/ZombieMaleBasic/ZombieMaleBasic.controller` retains its existing asset path, but every active motion has been replaced with a working clip listed above. It uses a Speed float in metres per second, with a 1D blend tree at 0/0.3/3.0. ZombieMale Patrol Speed is 0.3 and Maximum Speed is 3.0. The original translating clips' strides, scaled to the target Avatar, correspond to 0.28919 m/s walk and 4.00539 m/s run. Walk child time scale is 1.03738 and Run child time scale is 0.74899, matching those strides to the lower movement speeds. These settings apply only to ZombieMale; the controller is currently used by no other scene Animator. EnemyAnimationBridge reads actual planar NavMeshAgent velocity, damped over 0.1 seconds. Attack is a trigger; Attacking is a bool; AttackPlayback is a float. Any State enters Attack without self-transition; Attack exits when Attacking becomes false. The bridge emits one trigger per EnemyAttack.AnimationStarted notification and receives AttackFinished, including cancellation.

The melee animation starts after Stop Delay=0.15. Windup=0.2 and Recovery=0.266667 give playback rate 1.0 for the 0.4667-second working clip, with a 0.035-second blend in and a 0.12-second blend out. First contact is around source time 0.2 seconds (43% of the working clip). Damage still occurs only in EnemyAttack after windup, with its existing range/obstacle checks; animation events never apply damage. Cooldown remains 1.5 seconds.

The scene keeps its existing fast Play Mode options. AfterSceneLoad initialization restores AI noise subscriptions and the target registry, refreshes cached attacks, resets cooldown/busy state, and resets health. This supports repeated entries with both Domain Reload and Scene Reload disabled.

The previous BasicIdle/BasicWalk/BasicRun/BasicAttack assets remain in ZombieMaleBasic as unused fallbacks. The legacy Tools > Zombie Survival > Create Basic Zombie Animations command intentionally rebuilds those procedural fallbacks and replaces the controller motions; do not run it to refresh the imported clips. It was not used for the current replacement.

## Grounding and planted feet

`HumanoidGrounding` corrects Animator.bodyPosition in OnAnimatorIK and never changes the root Transform or NavMeshAgent.nextPosition. The measured initial hover was NavMesh raster height (about 11–15 cm above the Terrain collider); the Avatar ankle height already matched Animator.leftFeetBottomHeight, so changing the feet pivot/base offset was unnecessary.

Root and ankle rays use collider surfaces and ignore triggers, self, NavMeshAgent actors, CharacterControllers, and CombatTargets. Maximum body correction is 0.35 m. Sole clearance is 0.008 m. A foot plants below 0.025 m and releases above 0.055 m, at 0.3 m horizontal stretch, at a 25-degree turn, on loss of ground, or during an EnemyAttack. Stance feet keep their world XZ contact; swing feet retain the original animation. Support fades in over 0.12 seconds and out over 0.16 seconds with a clamped SmoothStep independent of frame rate. An active target is never replaced: its weight must reach zero first. After a moving stretch release, the animation must lift through the swing threshold before another contact can be acquired; idle/in-place turning and attack recovery can replant without a locomotion swing. This prevents a release followed by a next-frame target reset and immediate full-weight snap. Body correction remains smoothed separately. Ground normal is limited to 50 degrees. Configure Ground Layers for the environment when adding new geometry.

Foot orientation uses human IK-goal space, not the imported foot bone's local axes. At the first IK callback, each optional toe bone is measured relative to its own GetIKRotation goal to calibrate its anatomical forward yaw. A planted goal then faces the character heading projected onto the ground, preserving the surface normal and applying that per-foot calibration. Position and rotation share the smooth support weight; rotation follows heading smoothly, and release fades back to the swing animation. Humanoid Avatars without optional toe bones fall back to the standard goal-forward axis. This avoids the mirrored bone-axis error that previously turned planted feet sideways.

Before/after dynamic measurements on the same straight Terrain route (100 samples per gait): original walk 1.799 m/s had about 0.153 m mean stance clearance and 1.753 m/s stance slip; adjusted walk 0.299 m/s had 0.0105 m clearance and 0.100 m/s slip. Original run 5.434 m/s had 0.144 m clearance and 1.934 m/s slip; adjusted run 2.999 m/s had 0.0095 m clearance and 0.131 m/s slip. Root-to-agent drift was zero. These are ankle-minus-feet-bottom approximations during detected support, not a promise of zero sliding on every surface. Some repositioning remains at support changes/turns; uneven stairs and extreme terrain have not been exhaustively tuned.

The subsequent transition fix was measured over six complete walk and six run cycles with per-frame LateUpdate recording. The old walk lock reset its active target six times per foot and moved a foot about 0.30 m in one frame; the new transitions had zero active-target replacements and zero frame jumps over 0.10 m. Maximum walk frame displacements were 0.031/0.019 m, and run 0.051/0.062 m. These peaks include legitimate swing motion and depend on frame duration; the key invariant is no active-target reset. Contact clearance remained about 0.012 m walking and 0.007 m running. Brief animation yaw/sliding during acquire/release is allowed, rather than forcing a discontinuous full-weight pose.


### Crawl pursuit / Russian tuning UI

See `Docs/CrawlPursuitAndSettings.md` for observed-entry crawling, dual NavMesh surfaces, 360-degree hearing, Alt quiet walking and noise HUD. Investigating uses its own speed instead of the very slow patrol speed. `EnemyCrawlPursuit` selects posture/navigation; `HumanoidGrounding` delegates low-pose IK to `HumanoidCrawlPose`. Do not drive the navigation root from animation or remove target visibility/faction checks.

Actor settings: `Tools > Zombie Survival > Настройки персонажа и зомби`. Shared faction/health settings are in the first tab. Inspector tooltips explain project parameters in Russian. Inventory contracts for future NPC loot are in `Docs/Inventory.md`; repository AI entry point is `AGENTS.md`.


### Geometry-gated crawl and actor audio

Crawling is not mirrored from the player's posture. EnemyCrawlPursuit checks the standing and low capsule corridor, stores only a timed snapshot of an observed entry, walks to the entrance upright and lowers near the obstruction. Open ground and solid walls must not initiate crawling. Test both before changing detection.

Audio is composed independently: ActorAudioEmitter + ActorAudioProfile, with PlayerMovementAudio or NavMeshActorAudio adapters. Profiles and generated cue provenance are described in `Docs/ActorAudio.md`. Audio playback never emits GameplayNoise, preventing a second hearing simulation or event feedback loop. Select an actor and use the `Звук` tab in the actor settings window. Death and voice use a separate channel from footsteps.


## Оружие игрока (этап 10)

См. Docs/Weapons.md. PlayerWeapons наносит урон общему CombatTarget/Health через WeaponHitQuery; зомби не нуждается в специальном компоненте для пистолета или топора. Твёрдые коллайдеры останавливают удар/выстрел; фракции проверяются от атакующего. Шум Gunshot/Melee отправляется через GameplayNoise и не раскрывает Transform. Вооружённый NPC может использовать WeaponDefinition и WeaponHitQuery, но не PlayerWeapons (он зависит от ввода игрока). Боезапас магазина принадлежит ItemStack конкретного предмета.
