using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

public class Ball_LaunchBall_Test
{
    [UnityTest]
    public IEnumerator LaunchBall_SetsVelocityAndUpdatesLaunchState()
    {
        // Arrange
        GameObject ballObject = new GameObject();
        Ball ball = ballObject.AddComponent<Ball>();
        ball.speed = 5f;
        ball.isLaunched = false;

        // Act
        ball.LaunchBall();

        // Wait for Unity to process the frame
        yield return null;

        // Assert
        Assert.IsTrue(ball.isLaunched);
        Assert.AreEqual(ball.speed, ball.GetComponent<Rigidbody>().linearVelocity.magnitude);
    }
}