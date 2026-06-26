using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

public class Ball_FixedUpdate_Test
{
    [UnityTest]
    public IEnumerator Ball_MaintainsVelocityWithinSpeedLimits()
    {
        // Arrange
        GameObject ballObject = new GameObject("Ball");
        Ball ball = ballObject.AddComponent<Ball>();
        Rigidbody2D rb = ballObject.AddComponent<Rigidbody2D>();
        ball.rb = rb;

        // Set initial velocity
        Vector2 initialVelocity = new Vector2(2f, 2f);
        rb.linearVelocity = initialVelocity;

        // Activate the ball
        ball.isStoped = false;

        // Wait for one frame to allow Unity to process the FixedUpdate
        yield return null;

        // Assert
        Assert.GreaterOrEqual(rb.linearVelocity.magnitude, ball.minSpeed, "Velocity magnitude should be greater than or equal to minSpeed");
        Assert.LessOrEqual(rb.linearVelocity.magnitude, ball.maxSpeed, "Velocity magnitude should be less than or equal to maxSpeed");
        Assert.AreNotEqual(0, rb.linearVelocity.x, "X velocity should not be zero");
        Assert.AreNotEqual(0, rb.linearVelocity.y, "Y velocity should not be zero");
    }
}