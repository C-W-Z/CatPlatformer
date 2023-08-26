using UnityEngine;

public class CheckBox : MonoBehaviour
{
    private enum Type
    {
        Rectangle,
        Circle,
        Ray
    }

    [SerializeField] private Transform tf;
    [SerializeField] private Type type = Type.Rectangle;
    [SerializeField] private Color color = Color.red;
    [SerializeField] private float width, height;
    [SerializeField] private float radius;
    [SerializeField] private Vector2 direction;
    [SerializeField] private float distance = 1f;

    void OnDrawGizmos()
    {
        Gizmos.color = this.color;
        switch (type)
        {
            case Type.Rectangle:
                Gizmos.DrawWireCube(tf.position, new Vector3(width, height, 0));
                break;
            case Type.Circle:
                Gizmos.DrawWireSphere(tf.position, radius);
                break;
            case Type.Ray:
                Gizmos.DrawLine(tf.position, tf.position + (Vector3)(direction * distance));
                break;
        }
    }

    public bool Detect(LayerMask layer)
    {
        return type switch
        {
            Type.Rectangle => (bool)Physics2D.OverlapBox(tf.position, new Vector2(width, height), 0, layer),
            Type.Circle => (bool)Physics2D.OverlapCircle(tf.position, radius, layer),
            Type.Ray => (bool)Physics2D.Raycast(tf.position, direction, distance, layer),
            _ => false
        };
    }

    public Vector2 GetHitPoint(LayerMask layer, Vector2 defaultPos)
    {
        if (type != Type.Ray)
            return defaultPos;
        RaycastHit2D hit = Physics2D.Raycast(tf.position, direction, distance, layer);
        if (hit.collider != null)
            return hit.point;
        Debug.Log("no hit");
        return defaultPos;
    }

    public void FlipDirX()
    {
        direction.x = -direction.x;
    }
}