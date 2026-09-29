using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Edições de fase como DADOS (SPEC §14.2–14.3, D1 opção B): retângulos de células a preencher (ou limpar) nos
    /// Tilemaps sólidos de uma cena (o de colisão, "IntGrid" do LDtk, e o visual, "AutoLayer"; o LevelPatcher escolhe o
    /// tile de cada um). O LevelPatcher (Editor) aplica de forma idempotente e o analisador de alcançabilidade aceita os
    /// patches "virtuais" para validar a edição antes de mexer no YAML. As edições vão para as cenas, não para o .ldtk:
    /// reimportar o LDtk apagaria os overrides das fases — reaplique os patches depois de qualquer reimport.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Movement/Level Patch", fileName = "LevelPatch")]
    public sealed class LevelPatch : ScriptableObject
    {
        public enum Operation
        {
            Fill = 0,
            Clear = 1,
        }

        [Serializable]
        public struct CellRect
        {
            [Tooltip("Descrição do porquê (aparece no relatório).")]
            public string Note;
            public Operation Operation;
            [Tooltip("Célula mínima (inclusiva) no Tilemap de colisão.")]
            public Vector2Int Min;
            [Tooltip("Célula máxima (inclusiva).")]
            public Vector2Int Max;
        }

        [Tooltip("Nome da cena (sem extensão) a que o patch se aplica.")]
        public string SceneName;

        [Tooltip("Retângulos aplicados em ordem.")]
        public List<CellRect> Rects = new List<CellRect>();

        /// <summary>Aplica os retângulos num mundo AABB (patch virtual para o analisador): só Fill.</summary>
        public void ApplyVirtual(AabbCollisionWorld world, Func<Vector2Int, Vector2> cellToWorld, Vector2 cellSize)
        {
            if (world == null || cellToWorld == null) return;
            foreach (CellRect r in Rects)
            {
                if (r.Operation != Operation.Fill) continue;
                Vector2 min = cellToWorld(r.Min);
                Vector2 max = cellToWorld(r.Max) + cellSize;
                world.AddBox(min, max, AabbKind.Ground);
            }
        }
    }
}
