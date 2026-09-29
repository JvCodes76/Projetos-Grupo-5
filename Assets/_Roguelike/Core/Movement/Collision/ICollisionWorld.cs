using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>Filtros de consulta do mundo de colisão (SPEC §5.2).</summary>
    public enum QueryLayer : byte
    {
        /// <summary>Bloqueia movimento: Ground(6) + Default(0) + Enemy(10), sem triggers.</summary>
        Solids = 0,
        /// <summary>Wall slide e wall jump: só Ground(6).</summary>
        WallJumpable = 1,
        /// <summary>Último chão seguro: só Ground(6).</summary>
        SafeGround = 2,
        /// <summary>Linha de visão do gancho: só Ground(6).</summary>
        GrappleObstacle = 3,
        /// <summary>Triggers na layer 7 "KillZone".</summary>
        KillZones = 4,
    }

    /// <summary>Contato de um cast ou raycast.</summary>
    public readonly struct BoxHit
    {
        /// <summary>Distância percorrida pela forma até o contato (≥ 0).</summary>
        public readonly float Distance;

        public readonly Vector2 Normal;

        public BoxHit(float distance, Vector2 normal)
        {
            Distance = distance;
            Normal = normal;
        }
    }

    /// <summary>
    /// Mundo de colisão visto pelo núcleo (SPEC §2.3). Implementado pelo adaptador da física 2D (jogo) e por
    /// <see cref="AabbCollisionWorld"/> (testes, analisador, fuzz). Nenhuma consulta aloca.
    /// </summary>
    public interface ICollisionWorld
    {
        /// <summary>
        /// Cast de caixa alinhada aos eixos; <paramref name="dir"/> ∈ {±X, ±Y}. Retorna o contato mais próximo.
        /// Colisores de inimigo que já sobrepõem a caixa no início são ignorados (DS-11).
        /// </summary>
        bool CastBox(Vector2 center, Vector2 size, Vector2 dir, float distance, QueryLayer layer, out BoxHit hit);

        /// <summary>A caixa sobrepõe algum colisor do filtro?</summary>
        bool OverlapBox(Vector2 center, Vector2 size, QueryLayer layer);

        /// <summary>Raio com <paramref name="dir"/> normalizado.</summary>
        bool Raycast(Vector2 origin, Vector2 dir, float distance, QueryLayer layer, out BoxHit hit);

        /// <summary>Alvos do gancho (layer 8) a até <paramref name="radius"/>; escreve os centros em <paramref name="results"/>.</summary>
        int FindGrappleTargets(Vector2 center, float radius, Vector2[] results);
    }
}
