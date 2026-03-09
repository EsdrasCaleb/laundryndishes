using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

public class BallTests
{
    private GameObject ballObject;
    private Ball ball;
    private Rigidbody rb;

    [SetUp]
    public void Setup()
    {
        ballObject = new GameObject();
        rb = ballObject.AddComponent<Rigidbody>();
        ball = ballObject.AddComponent<Ball>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(ballObject);
    }

    [Test]
    public void ResetBall_ResetsVelocityToZero()
    {
        rb.velocity = new Vector3(1, 1, 0);
        ball.ResetBall();
        Assert.AreEqual(Vector3.zero, rb.velocity);
    }

    [Test]
    public void ResetBall_RepositionsToOrigin()
    {
        ballObject.transform.position = new Vector3(1, 1, 1);
        ball.ResetBall();
        Assert.AreEqual(Vector3.zero, ballObject.transform.position);
    }

    [Test]
    public void ResetBall_MarksAsNotLaunched()
    {
        ball.isLaunched = true;
        ball.ResetBall();
        Assert.IsFalse(ball.isLaunched);
    }
}