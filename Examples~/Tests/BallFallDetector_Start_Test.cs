using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BallFallDetectorTests
{
    [Test]
    public void BallFallDetector_Start_InitializesGameManagerReference()
    {
        // Arrange
        var gameObject = new GameObject();
        var ballFallDetector = gameObject.AddComponent<BallFallDetector>();
        var gameManager = new GameObject().AddComponent<GameManager>();

        // Act
        ballFallDetector.Start();

        // Assert
        Assert.IsNotNull(gameObject.GetComponent<GameManager>());
    }
}