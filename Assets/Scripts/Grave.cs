using System;
using UnityEngine;

/// <summary>Implement this on an enemy, or on an adapter for its combat system.</summary>
public interface IGraveEnemy
{
    event Action Defeated;
}

[DisallowMultipleComponent]
public sealed class Grave : MonoBehaviour
{
    public IGraveEnemy Enemy { get; private set; }
    public bool IsInUse { get; private set; }

    private GravePool owner;

    internal void Initialize(GravePool pool)
    {
        owner = pool;
        ClearAssociation();
    }

    internal void Bind(IGraveEnemy enemy)
    {
        ClearAssociation();
        Enemy = enemy;
        IsInUse = true;
        Enemy.Defeated += HandleDefeated;
    }

    private void HandleDefeated() => Release();

    /// <summary>Also call this when cancelling an enemy spawn or despawning it alive.</summary>
    public void Release()
    {
        if (owner != null && IsInUse)
            owner.Return(this);
    }

    internal void ClearAssociation()
    {
        if (Enemy != null)
            Enemy.Defeated -= HandleDefeated;
        Enemy = null;
        IsInUse = false;
    }

    private void OnDisable() => Release();

    private void OnDestroy()
    {
        Release();
        ClearAssociation();
    }
}
