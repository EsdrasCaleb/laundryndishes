using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class BallTests : InputTestFixture
{
    [UnityTest]
    public IEnumerator Ball_DetectsSpacebarPress_WhenNotLaunched()
    {
        var gameObject = new GameObject();
        var ball = gameObject.AddComponent<Ball>(); // Changed from BallScript to Ball (assuming Ball is the correct MonoBehaviour)
        ball.speed = 5f;
        var keyboard = InputSystem.AddDevice<Keyboard>();

        Assert.IsFalse(ball.isLaunched);

        Press(keyboard.spaceKey);
        yield return null;

        Assert.IsTrue(ball.isLaunched);
    }
}