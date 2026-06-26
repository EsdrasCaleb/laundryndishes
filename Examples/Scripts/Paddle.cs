using UnityEngine;

public class Paddle : MonoBehaviour
{
    public float speed = 8f;
    public float xLimit = 3.5f;
    public Ball ball;

    void Start()
    {
        ResetBall();
    }

    void Update()
    {
        float moveInput = Input.GetAxis("Horizontal");
        Vector2 newPosition = transform.position + new Vector3(moveInput * speed * Time.deltaTime, 0, 0);
        newPosition.x = Mathf.Clamp(newPosition.x, -xLimit, xLimit);
        if (ball != null && ball.isStoped)
        {
            ball.transform.position += new Vector3(newPosition.x - transform.position.x, 0, 0);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                LaunchBall();
            }
        }
        transform.position = newPosition;
    }

    public void LaunchBall()
    {
        if (ball != null)
        {
            float x = Random.Range(0, 2) == 0 ? -1 : 1;
            float y = Random.Range(0, 2) == 0 ? -1 : 1;
            ball.rb.linearVelocity = new Vector2(x, y).normalized * ball.initialSpeed;
            ball.isStoped = false;
        }
    }

    public void ResetBall()
    {
        if (ball != null)
        {
            ball.rb.linearVelocity = Vector2.zero;
            ball.lastVelocity = Vector2.zero;
            ball.transform.position = (Vector2)transform.position + new Vector2(0, 1);
            ball.isStoped = true;
        }
    }
}
