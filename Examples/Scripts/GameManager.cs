using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public int lives = 3;
    public int currentScore = 0;
    public TMP_Text livesText;
    public TMP_Text scoreText;
    public int bricksPerLevel = 5;
    public GameObject brickPrefab;
    public BrickSpawnerConfig spawnerConfig;
    public float brickWidth = 1.5f;  // Largura do brick (ajuste no Inspector)
    public float brickHeight = 0.5f; // Altura do brick (ajuste no Inspector)
    public float padding = 0.05f;     // Espaço mínimo entre bricks
    public Vector2 minMaxX = new Vector2(-7f, 7f);
    public Vector2 minMaxY = new Vector2(2f, 6f);
    public GameObject nextLevelPanel;
    public Paddle paddle;

    private int bricksRemaining;
    private List<GameObject> activeBricks = new List<GameObject>();

    void Start()
    {
        SetupLevel();
        UpdateUI();
    }

    void SetupLevel()
    {
        // Despawn existing bricks if any
        foreach (GameObject brick in activeBricks)
        {
            Destroy(brick);
        }
        activeBricks.Clear();

        bricksRemaining = 0;
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
            if (index >= 0 && index < availablePositions.Count)
            {
                Vector2 pos = availablePositions[index];
                availablePositions.RemoveAt(index);
                GameObject brickObj = Instantiate(brickPrefab, pos, Quaternion.identity);
                Brick brickScript = brickObj.GetComponent<Brick>();

                BrickData selectedData = spawnerConfig.defaultBrick;

                if (spawnerConfig.rareBricks != null && spawnerConfig.rareBricks.Count > 0)
                {
                    foreach (var rare in spawnerConfig.rareBricks)
                    {
                        if (Random.value <= rare.spawnChance)
                        {
                            selectedData = rare.brick;
                            break;
                        }
                    }
                }

                brickScript.Initiate(selectedData, this);
                activeBricks.Add(brickObj);
                bricksRemaining++;
            }
        }
    }

    void UpdateUI()
    {
        livesText.text = "Lives: " + lives;
        scoreText.text = "Score: " + currentScore;
    }

    public void BrickDestroyed()
    {
        bricksRemaining--;
        UpdateUI();
        if (bricksRemaining <= 0)
        {
            NextLevel();
        }
    }

    public void AddScore(int points)
    {
        Debug.Log("Add score" + points);
        currentScore += points;
        UpdateUI();
    }

    public void LoseLife()
    {
        lives--;
        UpdateUI();
        if (lives <= 0)
        {
            GameOver();
        }
        else
        {
            paddle.ResetBall();
        }
    }

    void NextLevel()
    {
        //Time.timeScale = 0;
        //nextLevelPanel.SetActive(true);
        bricksPerLevel++;
        SetupLevel();
        paddle.ResetBall();
    }

    void GameOver()
    {
        lives = 2;
        currentScore = 0;
        bricksPerLevel = 5;
        SetupLevel();
        paddle.ResetBall();
        UpdateUI();
    }

    public void ResumeGame()
    {
        Time.timeScale = 1;
        nextLevelPanel.SetActive(false);
        SetupLevel();
        paddle.ResetBall();
        UpdateUI();
    }
}
