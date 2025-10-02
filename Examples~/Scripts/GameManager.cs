using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public int lives = 3;
    public int currentScore = 0;
    public int bricksPerLevel = 20;
    public GameObject brickPrefab;
    public float brickWidth = 1.5f;  // Largura do brick (ajuste no Inspector)
    public float brickHeight = 0.5f; // Altura do brick (ajuste no Inspector)
    public float padding = 0.2f;     // Espaço mínimo entre bricks
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
        List<Vector2> occupiedPositions = new List<Vector2>();

        for (int i = 0; i < bricksPerLevel; i++)
        {
            Vector2 newPos;
            int attempts = 0;
            do
            {
                newPos = new Vector2(
                    Random.Range(minMaxX.x, minMaxX.y - brickWidth),
                    Random.Range(minMaxY.x, minMaxY.y - brickHeight)
                );
                attempts++;
                if (attempts > 100) // Evita loop infinito
                {
                    Debug.LogWarning("Não foi possível encontrar uma posição válida para o brick.");
                    break;
                }
            }
            while (IsOverlapping(newPos, occupiedPositions));

            if (attempts <= 100)
            {
                occupiedPositions.Add(newPos);
                Instantiate(brickPrefab, newPos, Quaternion.identity);
            }
        }
    }

    bool IsOverlapping(Vector2 newPos, List<Vector2> occupiedPositions)
    {
        foreach (Vector2 pos in occupiedPositions)
        {
            if (Mathf.Abs(newPos.x - pos.x) < brickWidth + padding &&
                Mathf.Abs(newPos.y - pos.y) < brickHeight + padding)
            {
                return true;
            }
        }
        return false;
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