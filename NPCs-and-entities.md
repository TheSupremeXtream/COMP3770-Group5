# GDD v.1 contribution

Established general enemy behavior concepts and identifies enemy parameters

# Prototype summary

Basic enemy movement, single enemy type, two projectile types, basic wave configuration.

# Enemy mechanics architecture:

## EnemyWave,cs

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

## Class diagram

```mermaid
classDiagram
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

  class C3["EnemyWave [Serializable]"] {
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

  class C5["WaveFormation [static]"] {
    +GetOffset(...) : Vector3
  }

  class C6["WaveFormationType] {
    <<Enumeration>>
    +Line
    +VFormation
    +Mixed
  }
  C6 <|-- C5
  C5 <|-- C1 : uses

  class C7["WaveSpawnInfo [struct] "] {
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

  class C8["IWaveSpawnReceiver (interface)"] {
    +OnWaveSpawn(info : WaveSpawnInfo)
  }
  C8 <|-- C7 : passed to
```