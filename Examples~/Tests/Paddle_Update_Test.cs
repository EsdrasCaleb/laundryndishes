using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class PaddleTests : InputTestFixture
{
    private Paddle paddle;
    private GameObject paddleObject;

    [SetUp]
    public void SetUp()
    {
        paddleObject = new GameObject();
        paddle = paddleObject.AddComponent<Paddle>();
        paddle.speed = 8f;
        paddle.xLimit = 3.5f;
    }

    [UnityTest]
    public IEnumerator PaddleMovesHorizontallyBasedOnInput()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        float initialX = paddleObject.transform.position.x;

        Press(keyboard.dKey);
        yield return null;

        Assert.Greater(paddleObject.transform.position.x, initialX);

        Release(keyboard.dKey);
        Press(keyboard.aKey);
        yield return null;

        Assert.Less(paddleObject.transform.position.x, initialX);
    }

    [UnityTest]
    public IEnumerator PaddlePositionIsClampedWithinLimits()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();

        Press(keyboard.dKey);
        for (int i = 0; i < 100; i++)
        {
            yield return null;
        }

        Assert.LessOrEqual(paddleObject.transform.position.x, paddle.xLimit);

        Release(keyboard.dKey);
        Press(keyboard.aKey);
        for (int i = 0; i < 100; i++)
        {
            yield return null;
        }

        Assert.GreaterOrEqual(paddleObject.transform.position.x, -paddle.xLimit);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(paddleObject);
    }
}