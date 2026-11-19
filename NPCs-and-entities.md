# GDD v.1 contribution

Established general enemy behavior concepts and identifies enemy parameters

# Prototype summary

Basic enemy movement, single enemy type, two projectile types, basic wave configuration.

# Enemy mechanics architecture:

## EnemyWave.cs

Wave definition class:

- Wave identification
- Wave formation
- Enemy prefabs selection
- Number of enemies per group
- Number of groups per wave
- Spawn behavior (simultaneous/individual)
- Spawn location (left/center/right)
- Wave duration
- Wave delay (display game info)

## WaveEncounter.cs

Executes waves, spawns enemies, monitors their status and manages encounter completion

- Starting and stopping encounters
- Progressing through multiple waves
- Spawning enemy groups
- Tracking active enemies
- Detecting destroyed and escaped enemies
- Handling timeouts
- Reporting encounter events to other systems
- Determining when an encounter is completed or failed

## WaveFormation.cs

Calculates the initial positions of enemies within a group

- Generating line formations
- Generating V-shaped formations
- Generating mixed formations
- Calculating horizontal spacing between enemies
- Determining each enemy's position relative to the spawn origin

## Class diagram

```mermaid
classDiagram
    direction BT

  class C1["WaveEncounter : MonoBehavior"] {
    +State : EncounterState
    +CurrentWaveIndex :  int
    +WaveCount : int
    +AliveCount : int
    +SpawnedCount : int
    +DestroyedCount : int
    +EscapedCount : int
    +TimedOutCount : int
    +ConfigErrorCount : int
    +IsCompleted : bool
    +IsFailed : bool
    +AllEnemiesDestroyed : bool
    +Configure()
    +SetBoundsSpace(...)
    +IsInsideBounds(...)
    +Start()
    +OnDisable()
    +StartEncounter()
    +RestartEncounter()
    +StopEncounter()
    +FailEncounter()
    +ResetProgress()
    +ValidateConfiguration()
    +Update()
    +SweepEnemies()
    +RunEncounter()
    +SpawnWave()
    +Spawn(...)
    +NotifySpawned()
    +WaitThenWarn()
    +GroupExtent(int)
    +ForceRemoveRemaining()
    +CompleteEncounter()
    +SetState(...)
    +HasAnyPrefab()
    +Wait(float)
    +OnDrawGizmos()
  }

  class C2["EncounterState"] {
    <<Enumeration>>
    +Idle
    +WaitingForWave
    +Spawning
    +Cleaning
    +Completed
    +Failed
  }
  C2 <|-- C1

  class C3["EnemyWave (Serializable)"] {
    +waveName : string
    +formation : WaveFormationType
    +enemyPrefabs : GameObject[]
    +enemiesPerGroup : int
    +groupCount : int
    +spacing : float
    +spawnStyle : WaveSpawnStyle
    +spawnInterval : float
    +delayBetweenGroups: float
    +groupEntryOffsets : float[]
    +delayBeforeWave : float
    +clearTimeout : float
    +TotalEnemies : int
    +GetGroupShift(int) : float
  }

  class C4["WaveSpawnStyle"] {
    <<Enumeration>>
    +Formation
    +Stream
  }
  C4 <|-- C3
  C1 <|-- C3 : configured by

  class C5["WaveFormation (static)"] {
    +GetOffset(...) : Vector3
  }

  class C6["WaveFormationType"] {
    <<Enumeration>>
    +Line
    +VFormation
    +Mixed
  }
  C6 <|-- C5
  C5 <|-- C1 : uses

  class C7["WaveSpawnInfo"] {
    +waveIndex : int
    +groupIndex : int
    +slotIndex : int
    +slotCount : int
    +slotOffsetX : float
    +groupShift : float
    +groupMinX : float
    +groupMaxX : float
  }
  C7 <|-- C1
  C7 <|-- C5

  class C8["IWaveSpawnReceiver"] {
    <<Interface>>
    +OnWaveSpawn(info : WaveSpawnInfo)
  }
  C8 <.. C7 : passed to

  class C9["TestEnemyMover : MonoBehavior"] {
    <<IWaveSpawnReceiver>>
    +OnEnable()
    +OnDisable()
    +OnWaveSpawn(...)
    +Hash(...)
    +ApplyDifficulty(...)
    +ToMask(...)
    +PlanRoute()
    +Update()
    +Step()
    +SubStepMove()
    +React()
    +SetFlankLane()
    +SetLane()
    +SafeLane(...)
  }
  C9 <..> C8 : implements

  class C10["TestEnemyShooter : MonoBehavior"] {
    <<IWaveSpawnReceiver>>
    +Awake()
    +OnEnable()
    +OnDisable()
    +OnWaveSpawn(...)
    +Configure()
    +Arm()
    +ArmRocket()
    +Update()
    +Finish()
    +ShowTelegraph()
    +HideTelegraph()
  }
  C10 <..> C8 : implements

  class C11["TestShooterRole"] {
    <<Enumeration>>
    +None
    +Gun
    +Rocket
  }
  C10 <|-- C11

  class C12["TaskAttackDirector : MonoBehavior"] {
    +Configure()
    +OnEnable()
    +OnDisable()
    +Update()
    +ResetState()
    +BasicRulesAllow()
    +TryBeginAttack()
    +EndAttack()
    +PredictedAim()
    +Fire()
    +TryBeginRocket()
    +CancelRocket()
    +LaunchRocket()
    +ClearAll()
  }
  C10 <|-- C12 : queries/uses

  class C13["TestEnemyBullet : MonoBehavior"] {
    +Launch()
    +Update()
    +OnTriggerEnter()
  }
  C12 <|--|> C13 : spawns

  class C14["TestHomingRocket : MonoBehavior"] {
    +Launch()
    +Update()
    +Step()
    +UpdateSound()
    +OnTriggerEnter()
  }
  C12 <|--|> C14 : spawns

  class C15["TestEnemyHealth : MonoBehavior"] {
    +Awake()
    +OnEnable()
    +TakeDamage()
    +Update()
    +SetFlash()
  }

  class C16["TestContactDamage : MonoBehavior"] {
    +Damage : int
  }
  
```