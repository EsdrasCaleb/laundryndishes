using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    public float speed = 5f;
    private Rigidbody rb;
    public bool isLaunched = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
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
        rb.linearVelocity = new Vector3(x, y,0).normalized * speed;
        isLaunched = true;
    }

    public void ResetBall()
    {
        rb.linearVelocity = Vector3.zero;
        transform.position = Vector3.zero;
        isLaunched = false;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Adicione lógica adicional se necessário (ex: som ao bater)
    }
}