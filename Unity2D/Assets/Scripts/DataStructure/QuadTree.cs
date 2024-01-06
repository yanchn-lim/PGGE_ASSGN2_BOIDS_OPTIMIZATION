using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadTree<T>
{
    public QuadTreeNode Root { get; private set; }

    //the amount of child before it the grid has to be
    //subdivided again
    int capacity = 1;

    public void Insert(Vector2 point, QuadTreeNode node)
    {
        //if point is not within the bounding box, skip this box
        if (!node.bounds.Contains(point))
        {
            return;
        }

        if(node.pointList.Count < capacity && node.children[0] == null)
        {
            node.pointList.Add(point);
        }
        else
        {
            if(node.children[0] == null)
            {
                Subdivide(node);
            }

            foreach(var child in node.children)
            {
                Insert(point, child);
            }
        }
    }

    public void Subdivide(QuadTreeNode node)
    {
        float width = node.bounds.width / 2;
        float height = node.bounds.height / 2;
        float x = node.bounds.x;
        float y = node.bounds.y;
        int depth = node.depth + 1;
        node.children[0] = new(new Rect(x, y, width, height),depth);
        node.children[1] = new(new Rect(x + width, y, width, height), depth);
        node.children[2] = new(new Rect(x, y + height, width, height), depth);
        node.children[3] = new(new Rect(x + width, y + height, width, height), depth);

        foreach(var point in node.pointList)
        {
            foreach(var child in node.children)
            {
                Insert(point, child);
            }
        }

        node.pointList.Clear();
    }

    public QuadTree(Rect bounds)
    {
        Root = new(bounds,0);
    }
}

public class QuadTreeNode
{
    public int depth;
    public Rect bounds;
    public List<Vector2> pointList;
    public QuadTreeNode[] children;

    public QuadTreeNode(Rect bound,int depth)
    {
        bounds = bound;
        pointList = new();
        children = new QuadTreeNode[4];
        this.depth = depth;
    }
}
