using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ParallaxBackground : MonoBehaviour
{
    // Singleton da câmera
    private static ParallaxCamera _parallaxCameraInstance;
    public static ParallaxCamera ParallaxCameraInstance
    {
        get
        {
            if (_parallaxCameraInstance == null)
            {
                _parallaxCameraInstance = Camera.main.GetComponent<ParallaxCamera>();
                if (_parallaxCameraInstance == null)
                {
                    GameObject cameraObj = new GameObject("ParallaxCamera");
                    _parallaxCameraInstance = cameraObj.AddComponent<ParallaxCamera>();
                }
            }
            return _parallaxCameraInstance;
        }
    }

    List<ParallaxLayer> parallaxLayers = new List<ParallaxLayer>();

    void Start()
    {
        // Usa a instância singleton da câmera
        if (_parallaxCameraInstance != null)
        {
            _parallaxCameraInstance.onCameraTranslate += Move;
        }
        SetLayers();
    }

    void OnDestroy()
    {
        // Remove o evento quando o objeto for destruído
        if (_parallaxCameraInstance != null)
        {
            _parallaxCameraInstance.onCameraTranslate -= Move;
        }
    }

    void SetLayers()
    {
        parallaxLayers.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            ParallaxLayer layer = transform.GetChild(i).GetComponent<ParallaxLayer>();

            if (layer != null)
            {
                layer.name = "Layer-" + i;
                parallaxLayers.Add(layer);
            }
        }
    }

    void Move(float delta)
    {
        foreach (ParallaxLayer layer in parallaxLayers)
        {
            layer.Move(delta);
        }
    }

    // Método público para acessar a câmera de outros scripts
    public static ParallaxCamera GetParallaxCamera()
    {
        return ParallaxCameraInstance;
    }
}