using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadTree
{
    public QuadTreeNode Root { get; private set; }

    //the amount of child before it the grid has to be
    //subdivided again
    Vector2 minSize;
    
    //recursive method to insert the object into the node it belongs to
    public void Insert(Autonomous obj, QuadTreeNode node)
    {
        //if point is not within the bounding box, skip this box
        if (!node.bounds.Contains(obj.transform.position))
        {   
            return;
        }

        if(node.children[0] == null && (node.bounds.size.x <= minSize.x || node.bounds.size.y <= minSize.y))
        {
            //Debug.Log(obj.name + " added at " + node.depth);
            node.objList.Add(obj);
        }
        else
        {
            //if there is no child in this node, it will
            //subdivide further into smaller quads
            if(node.children[0] == null)
            {
                Subdivide(node);
            }

            foreach(var child in node.children)
            {
                Insert(obj, child);
            }
        }
    }

    //method to split the node up into 4 quads
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

        node.objList.Clear();
    }


    public QuadTreeNode FindNode(QuadTreeNode node,Vector2 point)
    {
        if (node.children[0] != null)
        {
            foreach (var child in node.children)
            {
                Debug.Log(child.depth);
                if (!child.bounds.Contains(point))
                {
                    continue;
                }
                return FindNode(child, point);
            }
        }

        return node;
    }

    public List<QuadTreeNode> FindNodeWithPoints(QuadTreeNode node,Vector2[] points)
    {
        List<QuadTreeNode> nodes = new();

        foreach(var point in points)
        {
            nodes.Add(FindNode(node, point));
        }

        return nodes;
    }

    //public List<Autonomous> FindObjInRange(QuadTreeNode node, Autonomous query,AutonomousType type)
    //{
    //    List<Autonomous> objInRange = new();
    //    Rect bounds = query.bounds;
    //    Vector2 TopLeft = new(query.bounds.xMin,query.bounds.yMin);
    //    Vector2 TopRight = new(query.bounds.xMax, query.bounds.yMin);
    //    Vector2 BtmLeft = new(query.bounds.xMin, query.bounds.yMax);
    //    Vector2 BtmRight = new(query.bounds.xMax, query.bounds.yMax);

    //    Vector2[] points = {TopLeft,TopRight,BtmLeft,BtmRight};
    //    List<QuadTreeNode> nodesFound = FindNodeWithPoints(node, points);
    //    Debug.Log(nodesFound.Count);

    //    foreach (var n in nodesFound)
    //    {
    //        objInRange.AddRange(
    //                n.objList.FindAll(
    //                        x => bounds.Contains(x.transform.position)
    //                    )
    //            );

    //        Debug.Log(objInRange.Count);
    //    }

    //    foreach (var item in objInRange)
    //    {
    //        Debug.Log(item.name);
    //    }

    //    return objInRange;
    //}

    public QuadTree(Rect bounds,Vector2 minSize)
    {
        Root = new(bounds,-1);
        this.minSize = minSize;
    }
}

public class QuadTreeNode
{
    public int depth;
    public Rect bounds;
    public List<Autonomous> objList;
    public QuadTreeNode[] children;

    public QuadTreeNode(Rect bound,int depth)
    {
        bounds = bound;
        objList = new();
        children = new QuadTreeNode[4];
        this.depth = depth;
    }
}
