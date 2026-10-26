using System.Collections.Generic;
using UnityEngine;

/// <summary>Hits each living player once whenever the enemy activates this hitbox.</summary>
public sealed class AttackDamage : MonoBehaviour
{
    [SerializeField, Min(0)] private int m_damageToDo = 10;
    private readonly HashSet<p_PlayerStats> hitPlayers = new HashSet<p_PlayerStats>();

    private void OnEnable() => hitPlayers.Clear();
    private void OnTriggerEnter(Collider other) => TryHit(other);
    private void OnTriggerStay(Collider other) => TryHit(other);

    private void TryHit(Collider other)
    {
        var player = other.GetComponentInParent<p_PlayerStats>();
        if (player == null || player.Health <= 0f || !hitPlayers.Add(player)) return;
        player.TakeDamage(m_damageToDo);
    }
}
