namespace LnDTests.Paddle_Start_Test
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
    public IEnumerator Start_InitializesBallPositionAndState()
    {
        // Arrange
        GameObject paddleObject = new GameObject();
        Paddle paddle = paddleObject.AddComponent<Paddle>();

        GameObject ballObject = new GameObject();
        Ball ball = ballObject.AddComponent<Ball>();
        ball.rb = ballObject.AddComponent<Rigidbody2D>();
        paddle.ball = ball;

        // Act
        yield return null; // Wait for Start() to be called

        // Assert
        Assert.AreEqual(Vector2.zero, ball.rb.linearVelocity);
        Assert.AreEqual(Vector2.zero, ball.lastVelocity);
        Assert.AreEqual((Vector2)paddle.transform.position + new Vector2(0, 1), (Vector2)ball.transform.position);
        Assert.IsTrue(ball.isStoped);
    }
}
}