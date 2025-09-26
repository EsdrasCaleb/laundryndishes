using UnityEngine;

public class Ball : MonoBehaviour
{
    public float speed = 5f;
    private Rigidbody2D rb;
    private bool isLaunched = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        ResetBall();
    }

    void Update()
    {
        if (!isLaunched && Input.GetKeyDown(KeyCode.Space))
        {
            LaunchBall();
        }
    }

    public void LaunchBall()
    {
        float x = Random.Range(0, 2) == 0 ? -1 : 1;
        float y = Random.Range(0, 2) == 0 ? -1 : 1;
        rb.linearVelocity = new Vector2(x, y).normalized * speed;
        isLaunched = true;
    }

    public void ResetBall()
    {
        rb.linearVelocity = Vector2.zero;
        transform.position = Vector2.zero;
        isLaunched = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Adicione lógica adicional se necessário (ex: som ao bater)
    }
}