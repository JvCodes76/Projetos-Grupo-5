using System.Collections.Generic;
using Roguelike.Movement;
using Roguelike.MovementPhysics;
using UnityEngine;

/// <summary>
/// Varredura de triggers no tick (DS-12): depois de cada tick, sobrepõe a caixa do corpo com os triggers da cena e
/// chama <see cref="IPlayerTickTrigger.OnPlayerTickEnter"/> na entrada. Assim o EndGoal dispara no tick exato em que
/// o corpo entra (tempo de fase determinístico), e não no passo de física seguinte. Não aloca depois de aquecer.
/// </summary>
public sealed class TickTriggerScanner
{
    private readonly Collider2D[] results = new Collider2D[16];
    private readonly HashSet<Collider2D> inside = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> current = new HashSet<Collider2D>();
    private readonly ContactFilter2D filter;

    public TickTriggerScanner()
    {
        filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MovementLayers.TickTriggerMask };
    }

    public void Clear()
    {
        inside.Clear();
    }

    public void Scan(Component player, Vector2 bodyCenter, Vector2 bodySize, long tick)
    {
        int count = Physics2D.OverlapBox(bodyCenter, bodySize, 0f, filter, results);
        current.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider2D c = results[i];
            if (c == null || !c.isTrigger) continue;
            current.Add(c);
            if (inside.Contains(c)) continue;

            var trigger = c.GetComponentInParent<IPlayerTickTrigger>();
            trigger?.OnPlayerTickEnter(player, tick);
        }

        inside.Clear();
        foreach (Collider2D c in current) inside.Add(c);
    }
}
