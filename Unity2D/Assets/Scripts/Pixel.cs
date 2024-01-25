using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pixel : MonoBehaviour
{
    public Bounds bound;
    public List<GameObject> boidList;
    // Start is called before the first frame update
    void Start()
    {
        boidList = new();
    }

    private void OnDrawGizmos()
    {
        //Gizmos.DrawWireCube(bound.center,bound.size);
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        boidList.Add(collision.collider.gameObject);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        boidList.Remove(collision.collider.gameObject);
    }
}
