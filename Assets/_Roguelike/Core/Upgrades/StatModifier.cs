using System;
using UnityEngine;

namespace Roguelike.Upgrades
{
    /// <summary>Como um <see cref="StatModifier"/> combina com o valor base. Serializado como int: não renumere.</summary>
    public enum ModifierOperation
    {
        /// <summary>Soma <c>Value</c> ao valor base (ex.: +1 pulo aéreo, +0,08 s de coyote, +5 s de limite).</summary>
        Add = 0,
        /// <summary>Multiplica por <c>Value</c>, que é um FATOR (1,08 = +8 %; 0,7 = −30 %). Deve ser &gt; 0.</summary>
        Multiply = 1,
    }

    /// <summary>
    /// Um modificador de atributo declarado num UpgradeDefinition. Dado imutável depois de criado
    /// (campos privados serializados pela Unity; sem setters públicos).
    /// Fórmula aplicada pelo PlayerStats, por StatType:
    ///   final = max(0, (base + Σ Add) × Π Multiply)
    /// Os fatores Multiply se compõem entre stacks (3 × "+8 %" = ×1,08³). Stats inteiros (MaxAirJumps)
    /// devem usar só Add; a leitura inteira arredonda com Mathf.RoundToInt.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        [SerializeField] private StatType stat;
        [SerializeField] private ModifierOperation operation;
        [SerializeField] private float value;

        public StatModifier(StatType stat, ModifierOperation operation, float value)
        {
            this.stat = stat;
            this.operation = operation;
            this.value = value;
        }

        public StatType Stat => stat;
        public ModifierOperation Operation => operation;
        public float Value => value;

        public override string ToString()
        {
            return operation == ModifierOperation.Add ? $"{stat} +{value}" : $"{stat} ×{value}";
        }
    }
}
