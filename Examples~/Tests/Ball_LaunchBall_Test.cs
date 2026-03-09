using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BallTests
{
    [Test]
    public void LaunchBall_AppliesRandomNormalizedVelocityAtSpecifiedSpeed()
    {
        // Arrange
        GameObject ballObject = new GameObject();
        Rigidbody rb = ballObject.AddComponent<Rigidbody>();
        Ball ball = ballObject.AddComponent<Ball>();
        ball.speed = 5f;

        // Act
        ball.LaunchBall();

        // Assert
        Assert.IsTrue(ball.isLaunched);
        Assert.AreEqual(ball.speed, rb.linearVelocity.magnitude);
        Assert.IsTrue(rb.linearVelocity.z == 0);
        Assert.IsTrue(Mathf.Abs(rb.linearVelocity.x) == 1 || Mathf.Abs(rb.linearVelocity.y) == 1);
    }
}