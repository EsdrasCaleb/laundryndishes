using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Brick : MonoBehaviour
{
    public BrickData brickData;
    public GameManager gameManager;

    private Renderer _rederer;

    void Awake()
    {
        _rederer = GetComponent<Renderer>();
    }

    public void Initiate(BrickData data, GameManager manager)
    {
        brickData = data;
        gameManager = manager;

        if (_rederer == null) _rederer = GetComponent<Renderer>();

        if (brickData != null && _rederer != null)
        {
            _rederer.material = brickData.brickMaterial;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ball") && gameManager != null)
        {
            gameManager.AddScore(brickData.pointsValue);
            gameManager.BrickDestroyed();
            Destroy(gameObject);
        }
    }
}
