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
    public float padding = 0.05f;     // Espaço mínimo entre bricks
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
        bricksRemaining = bricksPerLevel / 2;
        // Centralized matrix setup with 1.5 width and 0.5 height
        float spacing = padding; // Small gap between bricks
        // Calculate grid dimensions based on minMaxX and minMaxY area
        float areaWidth = minMaxX.y - minMaxX.x;
        float areaHeight = minMaxY.y - minMaxY.x;

        int cols = Mathf.Max(1, Mathf.FloorToInt(areaWidth / (brickWidth + spacing)));
        int rows = Mathf.Max(1, Mathf.FloorToInt(areaHeight / (brickHeight + spacing)));

        // Create list of all available positions in the matrix
        List<Vector2> availablePositions = new List<Vector2>();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Vector2 pos = new Vector2(
                    minMaxX.x + col * (brickWidth + spacing) + (brickWidth + spacing) / 2f,
                    minMaxY.x + row * (brickHeight + spacing) + (brickHeight + spacing) / 2f
                );
                availablePositions.Add(pos);
            }
        }

        // Shuffle the available positions and place bricks
        System.Random rng = new System.Random();
        for (int i = 0; i < bricksPerLevel; i++)
        {
            int index = rng.Next(availablePositions.Count);
            if (index > 0)
            {
                Vector2 pos = availablePositions[index];
                availablePositions.RemoveAt(index);
                Instantiate(brickPrefab, pos, Quaternion.identity);
            }
            //Debug.Log(pos);
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
