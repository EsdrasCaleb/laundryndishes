using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BrickTests
{
    [Test]
    public void Start_InitializesGameManagerReference()
    {
        // Arrange
        var gameObject = new GameObject();
        var gameManager = gameObject.AddComponent<GameManager>();
        var brick = gameObject.AddComponent<Brick>();

        // Act
        brick.Start();

        // Assert
        Assert.IsNotNull(brick.gameManager);
    }
}