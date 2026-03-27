using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

public class Ball_ResetBall_Test
{
    [UnityTest]
    public IEnumerator ResetBall_ResetsBallState()
    {
        // Arrange
        GameObject ballObject = new GameObject();
        Ball ball = ballObject.AddComponent<Ball>();
        Rigidbody rb = ballObject.AddComponent<Rigidbody>();

        // Set initial state
        rb.linearVelocity = new Vector3(1, 2, 3);
        ballObject.transform.position = new Vector3(4, 5, 6);
        ball.isLaunched = true;

        // Act
        ball.ResetBall();

        // Wait for Unity to process the changes
        yield return null;

        // Assert
        Assert.AreEqual(Vector3.zero, rb.linearVelocity);
        Assert.AreEqual(Vector3.zero, ballObject.transform.position);
        Assert.IsFalse(ball.isLaunched);
    }
}