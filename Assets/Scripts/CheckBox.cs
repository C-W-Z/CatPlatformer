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
    [SerializeField] private Color _color = Color.red;
    [SerializeField] private float width, height;
    [SerializeField] private float radius;
    [SerializeField] private Vector2 direction;
    [SerializeField] private float distance = 1f;
    // [SerializeField] private bool useTrigger2D = false;
    // [SerializeField] private string layerName;
    // private bool triggerEnter = false;

    void OnDrawGizmos()
    {
        // if (useTrigger2D)
        //     return;
        Gizmos.color = _color;
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

    public bool Detect(LayerMask layer, int dirScale = 1)
    {
        // if (useTrigger2D)
        //     return triggerEnter;
        switch (type)
        {
            case Type.Rectangle:
                return Physics2D.OverlapBox(tf.position, new Vector2(width, height), 0, layer);
            case Type.Circle:
                return Physics2D.OverlapCircle(tf.position, radius, layer);
            case Type.Ray:
                return (bool)Physics2D.Raycast(tf.position, dirScale * direction, distance, layer);
            default:
                return false;
        }
    }

    public Vector2 GetHitPoint(LayerMask layer, Vector2 defaultPos, int dirScale = 1)
    {
        if (type != Type.Ray)
            return defaultPos;
        RaycastHit2D hit = Physics2D.Raycast(tf.position, dirScale * direction, distance, layer);
        if (hit.collider != null)
            return hit.point;
        Debug.Log("no hit");
        return defaultPos;
    }

    // void OnTriggerEnter2D(Collider2D collision)
    // {
    //     if (!useTrigger2D)
    //         return;
    //     if (collision.gameObject.layer == LayerMask.NameToLayer(layerName))
    //         triggerEnter = true;
    // }

    // void OnTriggerExit2D(Collider2D collision)
    // {
    //     if (!useTrigger2D)
    //         return;
    //     if (collision.gameObject.layer == LayerMask.NameToLayer(layerName))
    //         triggerEnter = false;
    // }
}