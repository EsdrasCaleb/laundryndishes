using UnityEngine;

public class Brick : MonoBehaviour
{
    public int points = 100;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
        {
            gameManager.AddScore(points);
            gameManager.BrickDestroyed();
            Destroy(gameObject);
        }
    }
}