using NUnit.Framework;
using UnityEngine;

public class GameManagerTests
{
    private GameManager gameManager;

    [SetUp]
    public void SetUp()
    {
        GameObject gameObject = new GameObject();
        gameManager = gameObject.AddComponent<GameManager>();
    }

    [Test]
    public void AddScore_ShouldIncrementCurrentScoreBySpecifiedPoints()
    {
        // Arrange
        int initialScore = gameManager.currentScore;
        int pointsToAdd = 10;

        // Act
        gameManager.AddScore(pointsToAdd);

        // Assert
        Assert.AreEqual(initialScore + pointsToAdd, gameManager.currentScore);
    }
}