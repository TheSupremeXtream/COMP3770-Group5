using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PLACEHOLDER sound set for the wave test scene: edited CC0 recordings in Assets/Audio/LevelWaves
/// (sources and licences in CREDITS.md there and in Level-Design-Waves-Boss.md). Swap clips here; no code changes needed.
/// </summary>
[CreateAssetMenu(menuName = "Solar Divers/Level Waves/Test Sound Bank", fileName = "TEST_SoundBank")]
public class TestSoundBank : ScriptableObject
{
    [Header("Weapons and hits")]
    public AudioClip[] autocannon = new AudioClip[0];
    public AudioClip[] metalImpacts = new AudioClip[0];
    public AudioClip[] playerHit = new AudioClip[0];

    [Header("Explosions")]
    public AudioClip[] enemyExplosions = new AudioClip[0];
    public AudioClip[] playerDestroyed = new AudioClip[0];

    [Header("Enemy aimed attack")]
    public AudioClip[] enemyCharge = new AudioClip[0];
    public AudioClip[] enemyCannon = new AudioClip[0];

    [Header("Engines")]
    [Tooltip("Seamless loop for the player's engine.")]
    public AudioClip playerEngineLoop;
    [Tooltip("One-shot recorded jet passes for nearby enemies.")]
    public AudioClip[] flybys = new AudioClip[0];
    [Tooltip("Seconds into every flyby clip where the recorded aircraft is closest (lined up with the enemy's closest approach).")]
    [Min(0f)] public float flybyPassSeconds = 1.4f;

    public AudioClip[] For(TestFxManager.Sound sound)
    {
        switch (sound)
        {
            case TestFxManager.Sound.Shot: return autocannon;
            case TestFxManager.Sound.Impact: return metalImpacts;
            case TestFxManager.Sound.EnemyDestroyed: return enemyExplosions;
            case TestFxManager.Sound.PlayerHit: return playerHit;
            case TestFxManager.Sound.PlayerDestroyed: return playerDestroyed;
            case TestFxManager.Sound.EnemyCharge: return enemyCharge;
            case TestFxManager.Sound.EnemyShot: return enemyCannon;
        }
        return null;
    }

    public IEnumerable<AudioClip> AllClips()
    {
        foreach (AudioClip[] set in new[] { autocannon, metalImpacts, playerHit, enemyExplosions, playerDestroyed, enemyCharge, enemyCannon, flybys })
        {
            foreach (AudioClip clip in set)
            {
                if (clip != null)
                {
                    yield return clip;
                }
            }
        }
        if (playerEngineLoop != null)
        {
            yield return playerEngineLoop;
        }
    }
}
