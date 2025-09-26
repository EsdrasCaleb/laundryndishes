using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public int lives = 3;
    public int currentScore = 0;
    public int bricksPerLevel = 20;
    public GameObject brickPrefab;
    public Vector2 minMaxX = new Vector2(-7f, 7f);
    public Vector2 minMaxY = new Vector2(2f, 6f);
    public GameObject nextLevelPanel;
    public Ball ball;

    private int bricksRemaining;

    void Start()
    {
        SetupLevel();
    }

    void SetupLevel()
    {
        bricksRemaining = bricksPerLevel;
        for (int i = 0; i < bricksPerLevel; i++)
        {
            float x = Random.Range(minMaxX.x, minMaxX.y);
            float y = Random.Range(minMaxY.x, minMaxY.y);
            Instantiate(brickPrefab, new Vector2(x, y), Quaternion.identity);
        }
    }

    public void BrickDestroyed()
    {
        bricksRemaining--;
        if (bricksRemaining <= 0)
        {
            NextLevel();
        }
    }

    public void AddScore(int points)
    {
        currentScore += points;
    }

    public void LoseLife()
    {
        lives--;
        if (lives <= 0)
        {
            GameOver();
        }
        else
        {
            ball.ResetBall();
        }
    }

    void NextLevel()
    {
        Time.timeScale = 0;
        nextLevelPanel.SetActive(true);
    }

    void GameOver()
    {
        Time.timeScale = 0;
        // Adicione lógica de Game Over (ex: mostrar painel)
    }

    public void ResumeGame()
    {
        Time.timeScale = 1;
        nextLevelPanel.SetActive(false);
        SetupLevel();
        ball.ResetBall();
    }
}