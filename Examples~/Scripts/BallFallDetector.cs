using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BallFallDetector : MonoBehaviour
{
    [SerializeField]
    private GameManager gameManager;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ball") && gameManager != null)
        {
            gameManager.LoseLife();
        }
    }
}
