using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Carregamento de cenas da run (tarefa 3.1). Comando direto do RunManager (mesmo domínio, sem bus).
/// Fica no mesmo GameObject raiz do RunManager, que já é DDOL: não é singleton nem chama DontDestroyOnLoad.
/// Um carregamento por vez, sempre no modo Single e assíncrono.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    /// <summary>Há um carregamento pedido por este loader ainda não concluído.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Começa a carregar <paramref name="sceneName"/> (LoadSceneMode.Single). <paramref name="onLoaded"/> é chamado
    /// quando a cena já está ativa e os Awake/OnEnable dos objetos dela já rodaram (os Start ainda não).
    /// Retorna false, sem carregar nada, se o nome for vazio, a cena não estiver no Build Settings ou já houver um
    /// carregamento em andamento.
    /// </summary>
    public bool Load(string sceneName, Action onLoaded)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] - Nome de cena vazio; nada foi carregado");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] - A cena '{sceneName}' não está no Build Settings; nada foi carregado");
            return false;
        }

        if (IsLoading)
        {
            Debug.LogWarning($"[SceneLoader] - Já há um carregamento em andamento; pedido de '{sceneName}' ignorado");
            return false;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError($"[SceneLoader] - A Unity recusou o carregamento de '{sceneName}'");
            return false;
        }

        IsLoading = true;
        operation.completed += _ =>
        {
            IsLoading = false;

            // O loader pode ter sido destruído durante o carregamento (ex.: saída do Play Mode).
            if (this == null) return;

            onLoaded?.Invoke();
        };

        return true;
    }
}
