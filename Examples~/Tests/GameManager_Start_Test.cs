using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

public class GameManagerTests
{
    [UnityTest]
    public IEnumerator Start_InitializesGameLevel()
    {
        // Arrange
        var gameObject = new GameObject();
        var gameManager = gameObject.AddComponent<GameManager>();
        gameManager.bricksPerLevel = 20;
        gameManager.brickPrefab = new GameObject("BrickPrefab");

        // Act
        gameManager.StartGame();
        yield return null;

        // Assert
        Assert.AreEqual(20, gameManager.bricksRemaining, "Bricks remaining should match bricksPerLevel");
        Assert.IsTrue(GameObject.FindObjectsOfType<GameObject>().Length > 1, "Bricks should be instantiated");
    }
}