using UnityEngine;

/// <summary>
/// PLACEHOLDER: marks a test enemy as harmful to touch. TestPlayerHealth looks for this on whatever it hits.
/// Leonardo's enemies / the team damage system should replace it.
/// </summary>
public class TestContactDamage : MonoBehaviour
{
    [SerializeField] private int damage = 1;

    public int Damage => damage;
}
