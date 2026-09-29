using Roguelike.Run;
using UnityEngine;

/// <summary>
/// Fachada legada do controlador de movimento (SPEC §13.1, DS-05). Mantém o arquivo, o .meta e o GUID
/// (07cf2a0327132794e97b608e52064c88) para que cenas, prefabs e scripts antigos continuem resolvendo o tipo; toda a
/// lógica vive no <see cref="PlayerController"/> (núcleo PlayerMotor). Só repassa comandos e leituras.
/// Os campos antigos viraram YAML órfão e somem no próximo save do prefab.
/// Removida na 4.1, quando o grep por "characterMovement" só achar este arquivo.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class characterMovement : MonoBehaviour
{
    private PlayerController controller;

    private PlayerController Controller
    {
        get
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            return controller;
        }
    }

    /// <summary>Morto (ou com a morte já pedida neste frame).</summary>
    public bool IsDead => Controller != null && Controller.IsDead;

    /// <summary>Gancho em uso (viagem ou puxão).</summary>
    public bool IsGrappling => Controller != null && Controller.IsGrappling;

    /// <summary>Morte sem causa conhecida (chamadores antigos).</summary>
    public void Die() => Die(DeathCause.Unknown);

    /// <summary>Morte (RF-41): idempotente; PlayerDied sai uma vez, no próximo tick.</summary>
    public void Die(DeathCause cause)
    {
        if (Controller != null) Controller.Die(cause);
    }

    public void DisableMovement()
    {
        if (Controller != null) Controller.DisableMovement();
    }

    public void EnableMovement()
    {
        if (Controller != null) Controller.EnableMovement();
    }

    /// <summary>"Voltar ao spawn" com as regras de respawn (Q11: o timer continua).</summary>
    public void ResetToSpawnPoint()
    {
        if (Controller != null) Controller.ResetToSpawnPoint();
    }

    /// <summary>
    /// Sem efeito: os stats chegam por PlayerStatsChanged (a 4.1 removeu o PlayerData/ShopManager que chamavam isto).
    /// Mantido só por compatibilidade de assinatura até a 4.1.
    /// </summary>
    public void RefreshStats()
    {
    }
}
