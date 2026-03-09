using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class GameManagerTests
{
    private GameManager gameManager;

    [SetUp]
    public void Setup()
    {
        GameObject gameManagerObject = new GameObject();
        gameManager = gameManagerObject.AddComponent<GameManager>();
        gameManager.bricksPerLevel = 3;
        gameManager.nextLevelPanel = new GameObject();
        gameManager.nextLevelPanel.SetActive(false);
        gameManager.ball = new GameObject().AddComponent<Ball>();
    }

    [Test]
    public void BrickDestroyed_DecrementsBricksRemaining()
    {
        int initialBricks = gameManager.bricksPerLevel;
        gameManager.BrickDestroyed();
        Assert.AreEqual(initialBricks - 1, gameManager.bricksRemaining);
    }

    [Test]
    public void BrickDestroyed_TriggersNextLevelWhenAllBricksDestroyed()
    {
        gameManager.bricksRemaining = 1;
        gameManager.BrickDestroyed();
        Assert.IsTrue(gameManager.nextLevelPanel.activeSelf);
        Assert.AreEqual(0, Time.timeScale);
    }
}