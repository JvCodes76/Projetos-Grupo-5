using UnityEngine;

public class EndGoal : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            SceneController.instance.NextLevel();
        }
    }
}
