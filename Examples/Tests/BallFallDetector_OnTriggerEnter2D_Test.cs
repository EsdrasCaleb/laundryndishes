using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.TestTools;

namespace LnDTests.BallFallDetector_OnTriggerEnter2D_Test
{
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BallFallDetectorTests
{
    [UnitySetUp]
    public IEnumerator LnDDefaultReloadScene()
    {
        var scene = SceneManager.GetActiveScene();

        yield return SceneManager.LoadSceneAsync(
            scene.name,
            LoadSceneMode.Single
        );
    }

    [UnityTearDown]
    public IEnumerator LnDDefaultClearScene()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var go in roots)
        {
            if (go != null)
            {
                // DestroyImmediate força a remoção instantânea da memória
                Object.DestroyImmediate(go);
            }
        }

        yield return null; 
    }

    [UnityTest]
    public IEnumerator BallFallDetector_HandlesLifeLossOnBallCollision()
    {
        // Arrange
        var gameObject = new GameObject();
        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        var gameManagerObject = new GameObject();
        var gameManager = gameManagerObject.AddComponent<GameManager>();
        gameManager.lives = 3;

        var ballFallDetector = gameObject.AddComponent<BallFallDetector>();
        // Assuming BallFallDetector has a reference to GameManager through a public field or property
        // If it's a field, use:
        // ballFallDetector.gameManager = gameManager;
        // If it's a property, use:
        // ballFallDetector.GameManager = gameManager;

        var ballObject = new GameObject();
        ballObject.tag = "Ball";
        var ballCollider = ballObject.AddComponent<CircleCollider2D>();
        var ballRigidbody = ballObject.AddComponent<Rigidbody2D>();

        // Act
        ballObject.transform.position = gameObject.transform.position;
        yield return null;

        // Assert
        Assert.AreEqual(2, gameManager.lives);
    }
}
}