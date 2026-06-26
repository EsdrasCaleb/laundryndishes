using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

public class Ball_Awake_Test
{
    [UnityTest]
    public IEnumerator Ball_Awake_InitializesRigidbody2D()
    {
        // Arrange
        GameObject ballObject = new GameObject();
        Ball ball = ballObject.AddComponent<Ball>();
        ballObject.AddComponent<Rigidbody2D>();

        // Act
        yield return null;

        // Assert
        Assert.IsNotNull(ball.rb);
    }
}