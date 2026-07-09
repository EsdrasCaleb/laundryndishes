namespace LnDTests.Paddle_LaunchBall_Test
{
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

public class PaddleTests
{

                [UnityEngine.TestTools.UnitySetUp]
                public System.Collections.IEnumerator LnDDefaultReloadScene()
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scene.name, UnityEngine.SceneManagement.LoadSceneMode.Single);
                }

                [UnityEngine.TestTools.UnityTearDown]
                public System.Collections.IEnumerator LnDDefaultClearScene()
                {
                    var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var go in roots)
                    {
                        if (go != null)
                        {
                            UnityEngine.Object.DestroyImmediate(go);
                        }
                    }
                    yield return null; 
                }
    [UnityTest]
    public IEnumerator LaunchBall_WhenBallExists_SetsVelocityAndMarksNotStopped()
    {
        // Arrange
        GameObject paddleObject = new GameObject();
        Paddle paddle = paddleObject.AddComponent<Paddle>();

        GameObject ballObject = new GameObject();
        Ball ball = ballObject.AddComponent<Ball>();
        ball.rb = ballObject.AddComponent<Rigidbody2D>();
        ball.initialSpeed = 5f;
        ball.isStoped = true;

        paddle.ball = ball;

        // Act
        paddle.LaunchBall();

        // Wait for Unity to process the frame
        yield return null;

        // Assert
        Assert.IsFalse(ball.isStoped);
        Assert.AreEqual(ball.initialSpeed, ball.rb.linearVelocity.magnitude);
    }
}
}