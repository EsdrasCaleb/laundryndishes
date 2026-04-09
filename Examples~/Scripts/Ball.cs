using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Ball : MonoBehaviour
{
    public float initialSpeed = 5f;
    public float minSpeed = 3f;
    public float maxSpeed = 10f;
    public Rigidbody2D rb;
    public bool isStoped = true;

    // VARIÁVEL NOVA: Para guardar a velocidade antes da colisão
    public Vector2 lastVelocity;

    private Paddle paddle;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (!isStoped)
        {
            Vector2 currentVelocity = rb.linearVelocity;

            // Ensure Y velocity is not 0
            if (currentVelocity.y == 0)
            {
                currentVelocity.y = Mathf.Sign(currentVelocity.x) * 0.1f; // Add a small non-zero Y velocity
                currentVelocity = currentVelocity.normalized * currentVelocity.magnitude; // Maintain original magnitude
            }

            // Garante velocidade mínima
            if (currentVelocity.magnitude < minSpeed)
            {
                if (currentVelocity == Vector2.zero)
                    currentVelocity = Vector2.up * minSpeed;
                else
                    currentVelocity = currentVelocity.normalized * minSpeed;
            }

            // Garante velocidade máxima
            if (currentVelocity.magnitude > maxSpeed)
            {
                currentVelocity = currentVelocity.normalized * maxSpeed;
            }

            rb.linearVelocity = currentVelocity;

            // SALVA A VELOCIDADE ATUAL PARA USAR NO PRÓXIMO FRAME (Caso haja colisão)
            lastVelocity = rb.linearVelocity;
        }
    }
}
