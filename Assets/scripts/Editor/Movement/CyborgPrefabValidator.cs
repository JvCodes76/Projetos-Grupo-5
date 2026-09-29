using System.Collections.Generic;
using Roguelike.Movement;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validador do Cyborg (SPEC §13.2, RF-40): root com escala 1 e origem nos pés; BoxCollider2D = BodySize do perfil;
/// Rigidbody2D como na §5.1; tag Player e layer 3; nenhum Missing Script; componentes novos presentes; e o sprite no
/// mesmo lugar em relação ao colisor que no prefab antigo (± 1 px). Menu: Tools/Movement/Validate Cyborg Prefab.
/// </summary>
public static class CyborgPrefabValidator
{
    private const float PixelTolerance = 1f / 37.6f; // 1 px do sprite (64 PPU × 1,7)

    [MenuItem("Tools/Movement/Validate Cyborg Prefab")]
    public static void ValidateMenu()
    {
        GameObject prefab = Selection.activeGameObject;
        if (prefab == null || prefab.GetComponent<PlayerController>() == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MovementProjectSetup.CyborgPrefabPath);
        }

        List<string> errors = Validate(prefab);
        if (errors.Count == 0) Debug.Log($"[Movement] - Validator: '{prefab.name}' OK");
        foreach (string e in errors) Debug.LogError($"[Movement] - Validator: {e}");
    }

    public static List<string> Validate(GameObject root)
    {
        var errors = new List<string>();
        if (root == null)
        {
            errors.Add("prefab não encontrado");
            return errors;
        }

        if (root.transform.localScale != Vector3.one) errors.Add($"escala do root {root.transform.localScale} (esperado 1)");
        if (!root.CompareTag("Player")) errors.Add($"tag '{root.tag}' (esperado Player)");
        if (root.layer != 3) errors.Add($"layer {root.layer} (esperado 3 Player)");

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) errors.Add($"Missing Script em '{t.name}'");
        }

        var controller = root.GetComponent<PlayerController>();
        if (controller == null)
        {
            errors.Add("sem PlayerController");
            return errors;
        }

        MovementProfile profile = controller.Profile;
        if (profile == null) errors.Add("PlayerController sem MovementProfile");
        Vector2 bodySize = profile != null ? profile.BodySize : new Vector2(0.51f, 1.26f);

        var box = root.GetComponent<BoxCollider2D>();
        if (box == null) errors.Add("sem BoxCollider2D");
        else
        {
            if ((box.size - bodySize).sqrMagnitude > 1e-8f) errors.Add($"BoxCollider2D size {box.size} ≠ BodySize {bodySize}");
            if ((box.offset - new Vector2(0f, bodySize.y * 0.5f)).sqrMagnitude > 1e-8f) errors.Add($"BoxCollider2D offset {box.offset} (esperado pés na origem)");
            if (box.isTrigger) errors.Add("BoxCollider2D é trigger");
        }

        var body = root.GetComponent<Rigidbody2D>();
        if (body == null) errors.Add("sem Rigidbody2D");
        else
        {
            if (body.bodyType != RigidbodyType2D.Kinematic) errors.Add($"Rigidbody2D {body.bodyType} (esperado Kinematic)");
            if (!body.useFullKinematicContacts) errors.Add("Rigidbody2D sem Use Full Kinematic Contacts");
            if (body.interpolation != RigidbodyInterpolation2D.None) errors.Add("Rigidbody2D com interpolação da engine");
            if (body.gravityScale != 0f) errors.Add("Rigidbody2D com gravityScale ≠ 0");
        }

        if (root.GetComponent<characterMovement>() == null) errors.Add("sem a fachada characterMovement");
        if (root.GetComponent<PlayerView>() == null) errors.Add("sem PlayerView");
        if (root.GetComponent<PlayerAnimatorDriver>() == null) errors.Add("sem PlayerAnimatorDriver");
        if (root.GetComponent<PlayerFeedback>() == null) errors.Add("sem PlayerFeedback");
        if (root.GetComponent<UnityEngine.InputSystem.PlayerInput>() != null) errors.Add("PlayerInput ainda no prefab (o input é do GameInput)");

        Transform sprite = root.transform.Find("VisualRoot/SquashPivot/Sprite");
        if (sprite == null) errors.Add("sem VisualRoot/SquashPivot/Sprite");
        else
        {
            var sr = sprite.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) errors.Add("Sprite sem SpriteRenderer/sprite");
            if (sprite.GetComponent<Animator>() == null) errors.Add("Sprite sem Animator");
            if ((sprite.lossyScale - new Vector3(1.7f, 1.7f, 1f)).sqrMagnitude > 1e-6f) errors.Add($"escala do sprite {sprite.lossyScale} (esperado 1,7)");

            // No prefab antigo o centro do sprite ficava 0,777 u acima dos pés e +0,088 u em X do centro do colisor.
            Vector3 expected = root.transform.position + new Vector3(0.088f, 0.777f, 0f);
            Vector3 actual = sprite.position;
            if (Mathf.Abs(actual.x - expected.x) > PixelTolerance || Mathf.Abs(actual.y - expected.y) > PixelTolerance)
            {
                errors.Add($"sprite deslocado em relação ao antigo: {actual - expected}");
            }
        }

        if (root.GetComponentInChildren<SpriteRenderer>(true) != null && root.GetComponent<SpriteRenderer>() != null)
        {
            errors.Add("SpriteRenderer ainda no root (deve ficar só no filho Sprite)");
        }

        return errors;
    }
}
