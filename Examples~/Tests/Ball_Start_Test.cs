using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class BallTests : InputTestFixture
{
    [UnityTest]
    public IEnumerator TestBallInitialization()
    {
        var gameObject = new GameObject();
        var ball = gameObject.AddComponent<Ball>();
        var rb = gameObject.AddComponent<Rigidbody>();

        yield return null;

        Assert.IsNotNull(ball);
        Assert.IsNotNull(rb);
        Assert.AreEqual(Vector3.zero, rb.linearVelocity);
        Assert.AreEqual(Vector3.zero, gameObject.transform.position);
        Assert.IsFalse(ball.isLaunched);
    }
}