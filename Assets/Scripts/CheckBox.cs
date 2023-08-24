using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CheckBox : MonoBehaviour
{
    private enum Type
    {
        Rectangle,
        Circle
    }

    [SerializeField] private Type type = Type.Rectangle;
    [SerializeField] private Color _color = Color.red;
    [SerializeField] private float width, height;
    [SerializeField] private float radius;
    // [SerializeField] private bool useTrigger2D = false;
    // [SerializeField] private string layerName;
    // private bool triggerEnter = false;

    void OnDrawGizmos()
    {
        // if (useTrigger2D)
        //     return;
        Gizmos.color = _color;
        if (type == Type.Circle)
            Gizmos.DrawWireSphere(transform.position, radius);
        else
            Gizmos.DrawWireCube(transform.position, new Vector3(width, height, 0));
    }

    public bool Detect(LayerMask layer)
    {
        // if (useTrigger2D)
        //     return triggerEnter;
        if (type == Type.Circle)
            return Physics2D.OverlapCircle(transform.position, radius, layer);
        return Physics2D.OverlapBox(transform.position, new Vector2(width, height), 0, layer);
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