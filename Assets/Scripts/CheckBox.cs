using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CheckBox : MonoBehaviour
{
    [SerializeField] private float width, height;
    [SerializeField] private Color _color = Color.red;

    void OnDrawGizmos()
    {
        Gizmos.color = _color;
        Gizmos.DrawWireCube(transform.position, new Vector3(width, height, 0));
    }

    public bool Detect(LayerMask layer)
    {
        return Physics2D.OverlapBox(transform.position, new Vector2(width, height), 0, layer);
    }
}