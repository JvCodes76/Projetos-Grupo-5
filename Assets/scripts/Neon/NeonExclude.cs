using UnityEngine;

/// <summary>
/// Marca este objeto (e os filhos) para NÃO receber o material de neon do <see cref="NeonGlow"/>. Use quando a
/// detecção por cor acende algo que não é neon e não vale mexer nos limiares do jogo todo.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Neon/Neon Exclude")]
public sealed class NeonExclude : MonoBehaviour
{
}
