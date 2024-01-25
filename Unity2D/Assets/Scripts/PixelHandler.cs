using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PixelHandler : MonoBehaviour
{
    public BoxCollider2D box;
    public Bounds bounds;

    public float visibility;
    List<Bounds> gridList;

    public GameObject pixelPrefab;
    public Vector3 offset;

    private void Awake()
    {
        bounds = box.bounds;
        
    }

    private void Start()
    {
        InitializeGrid();
    }

    void InitializeGrid()
    {
        gridList = new();
        int xNum = Mathf.FloorToInt(bounds.size.x / visibility);
        int yNum = Mathf.FloorToInt(bounds.size.y / visibility);

        for (int x = 0; x < xNum; x++)
        {
            for (int y = 0; y < yNum; y++)
            {
                Vector3 pos = new(bounds.min.x + (x * visibility), bounds.min.y + (y * visibility));
                pos += offset;

                GameObject p = Instantiate(pixelPrefab,pos,Quaternion.identity);
                Pixel pix = p.GetComponent<Pixel>();
                BoxCollider2D c = p.GetComponent<BoxCollider2D>();
                c.size = new(visibility,visibility);
                gridList.Add(c.bounds);
            }
        }
    }

    void CheckIfBoidIn()
    {

    }

    private void OnDrawGizmos()
    {
        foreach (var item in gridList)
        {
            Gizmos.DrawWireCube(item.center, item.size);

        }
    }
}
