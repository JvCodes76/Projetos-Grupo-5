using UnityEngine;

public class GameController : MonoBehaviour
{
    [SerializeField] private int FrameLimit = 60;
    void Start()
    {
        Application.targetFrameRate = FrameLimit;
    }

}
