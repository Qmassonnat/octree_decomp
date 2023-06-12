using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Funnel : MonoBehaviour
{
    private List<CustomNodeScriptable> path;
    private List<Vector3> fov = new List<Vector3>();
    private List<Vector3> normalized_fov = new List<Vector3>();
    private Vector3 anchorPoint;

    public List<Vector3> Funnel3D(List<CustomNodeScriptable> path_)
    {
        path = path_;
        List<Vector3> positionList = new List<Vector3> { path[0].position };
        List<Color> colors = new List<Color> { Color.red, Color.yellow, Color.cyan, Color.blue, Color.gray, Color.white };
        InitializeFunnel(0, path[0].position);
        for (int i=1; i<path.Count; i++)
        {
            CustomNodeScriptable cn = path[i];
            // get the coordinates of the next transition
            List<Vector3> transition = GetTransitionSurface(cn);
            //DrawPath(transition, colors[i%colors.Count]);
            //update the 3d funnel with convex intersection
            bool intersect = ConvexIntersection(transition);
            //DrawPath(fov, Color.black);
            if (!intersect)
            {
                // if the intersection is null, find the new starting point and restart the funnel from there
                Vector3 closest = GetClosestApprox(fov, transition);
                InitializeFunnel(i-1, closest);
                positionList.Add(anchorPoint);
            }
        }
        positionList.Add(path[path.Count-1].position);
        return positionList;
    }

    public void InitializeFunnel(int idx, Vector3 anchor)
    {
        anchorPoint = anchor;
        //GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //g.transform.position = anchorPoint;
        //g.transform.localScale = Vector3.one * 0.5f;
        fov = GetTransitionSurface(path[idx + 1]);
        normalized_fov = new List<Vector3>(new Vector3[fov.Count]);
        // normalize the vectors in fov to be on S(0, 1)
        for (int j = 0; j < fov.Count; j++)
            normalized_fov[j] = (fov[j] - anchorPoint) / Vector3.Distance(fov[j], anchorPoint);
    }

    IEnumerator WaitF()
    {
        yield return new WaitForSeconds(10f);
    }

    // computes the intersection between fov and transition, returning false if the intersection is empty
    public bool ConvexIntersection(List<Vector3> transition)
    {
        // TODO if the transition is just the target point check if it is inside the fov
        List<Vector3> normalized_trans = new List<Vector3>(new Vector3[transition.Count]);
        // normalize the next transition c2
        for (int j = 0; j < transition.Count; j++)
            normalized_trans[j] = (transition[j] - anchorPoint).normalized;
        normalized_fov = Distinct(HandleLine(normalized_fov));
        normalized_trans = Distinct(HandleLine(normalized_trans));
        bool line_transition = normalized_trans.Count == 2;
        // handle the degenerate case where the fov is a single point
        if (normalized_fov.Count == 1)
        {
            //Debug.Log("handling point special case");
            normalized_trans = Distinct(normalized_trans);
            Vector3 u = normalized_fov[0];
            // handling case where transition is a point
            if (normalized_trans.Count == 1)
            {
                if (Vector3.Cross(normalized_trans[0], u) == Vector3.zero)
                {
                    fov = new List<Vector3> { transition[0] };
                    return true;
                }
                else
                    return false;
            }
            // handling case where transition is a line
            if (normalized_trans.Count == 2)
            {
                Vector3 axis = Vector3.Cross(normalized_trans[0], normalized_trans[1]);
                if (Vector3.Dot(u, axis) != 0)
                    return false;
                float a = Vector3.SignedAngle(normalized_trans[0], u, axis);
                if ((a >= 0 && a <= Vector3.SignedAngle(normalized_trans[0], normalized_trans[1], axis)) || (a <= 0 && a >= Vector3.SignedAngle(normalized_trans[0], normalized_trans[1], axis)))
                {
                    // TODO project v on segment   
                    Vector3 x = normalized_trans[0] + (normalized_trans[1] - normalized_trans[0]) * a / Vector3.SignedAngle(normalized_trans[0], normalized_trans[1], axis);
                    fov.Add(x);
                    normalized_fov.Add((x - anchorPoint).normalized);
                    return true;
                }
                else
                    return false;
            }
            // since all points are in S(0,1), fov is inside transition iff the ray [0, fov) intersects the transition plane inside B(0,1)
            Vector3 center = (normalized_trans[0] + normalized_trans[1] + normalized_trans[2] + normalized_trans[3]) / 4;
            bool intersect = false;
            foreach (var v in normalized_trans)
            {
                if (Vector3.Dot(center, u) >= Vector3.Dot(center, v))
                    intersect = true;
            }
            if (!intersect)
                return intersect;
        }

        // handle the degenerate case where a surface is coplanar with the anchor
        else if (normalized_fov.Count == 2)
        {
            Vector3 u_min = normalized_fov[0];
            Vector3 u_max = normalized_fov[1];
            Vector3 axis = Vector3.Cross(u_min, u_max);
            if (Equals(axis, Vector3.zero))
            {
                axis = Vector3.Cross(fov[0] - anchorPoint, fov[2] - anchorPoint);
                Debug.Log("Cross product warning");
            }
            Vector3 above_max = normalized_trans[0];
            Vector3 above_min = normalized_trans[0];
            Vector3 below_max = normalized_trans[0];
            Vector3 below_min = normalized_trans[0];
            float amin = float.PositiveInfinity;
            float amax = float.NegativeInfinity;
            float bmin = float.PositiveInfinity;
            float bmax = float.NegativeInfinity;
            foreach (Vector3 v in normalized_trans)
            {
                Vector3 p = Vector3.ProjectOnPlane(v, axis);
                float a = Vector3.SignedAngle(u_min, p, axis);
                if (a <= amin && Vector3.Dot(v - p, axis) >= 0)
                {
                    amin = a;
                    above_min = v;
                }
                if (a >= amax && Vector3.Dot(v - p, axis) >= 0)
                {
                    amax = a;
                    above_max = v;
                }
                if (a <= bmin && Vector3.Dot(v - p, axis) <= 0)
                {
                    bmin = a;
                    below_min = v;
                }
                if (a >= bmax && Vector3.Dot(v - p, axis) <= 0)
                {
                    bmax = a;
                    below_max = v;
                }
            }
            // if there are not corners on both sides of the segment then the intersection is null
            if (amin == float.PositiveInfinity || bmin == float.PositiveInfinity)
                return false;
            Vector3 vmin;
            if (above_min == below_min)
                vmin = above_min;
            else
                vmin = above_min + (below_min - above_min) * Vector3.Dot(axis, above_min) / Vector3.Dot(axis, above_min - below_min);
            Vector3 vmax;
            if (above_max == below_max)
                vmax = above_max;
            else
                vmax = above_max + (below_max - above_max) * Vector3.Dot(axis, above_max) / Vector3.Dot(axis, above_max - below_max);
            // if vmin and vmax are outside of the segment then the intersection is null
            if (Vector3.SignedAngle(u_min, vmax, axis) < 0)
                return false;
            if (Vector3.SignedAngle(u_min, vmin, axis) > Vector3.SignedAngle(u_min, u_max, axis))
                return false;
            // else intersect [vmin, vmax] and [u_min, u_max]
            if (Vector3.SignedAngle(u_min, vmin, axis) < 0)
                vmin = u_min;
            if (Vector3.SignedAngle(u_min, vmax, axis) > Vector3.SignedAngle(u_min, u_max, axis))
                vmax = u_max;
            normalized_trans = new List<Vector3> { vmin, vmax };

        }
        else { 
            // for each edge in c1 get the plane going through that edge and the anchor
            for (int i = 0; i < normalized_fov.Count; i++)
            {
                // use the normal vector n to this plane to determine if points in c2 are in the right half-space
                Vector3 u = normalized_fov[i];
                Vector3 v = normalized_fov[(i + 1) % normalized_fov.Count];
                Vector3 n = Vector3.Cross(u, v);
                if (n == Vector3.zero)
                {
                    Debug.LogError("u and v are colinear" + u + " " + v + "size"+normalized_fov.Count);
                }
                // make n point towards the half space where the rest of c1 is
                if (Vector3.Dot(n, normalized_fov[(i + 2) % normalized_fov.Count]) < 0)
                    n = -n;

                List<Vector3> new_trans = new List<Vector3>();
            
                for (int j = 0; j < normalized_trans.Count; j++)
                {
                    Vector3 u2 = normalized_trans[j];
                    Vector3 v2 = normalized_trans[(j + 1) % normalized_trans.Count];
                    float f = Vector3.Dot(n, u2);
                    float g = Vector3.Dot(n, v2);
                    // x is the intersection of [u2, v2] and the plane if there is one
                    Vector3 x = new Vector3();
                    if (Vector3.Dot(n, u2 - v2) != 0)
                        x = u2 + (v2 - u2) * Vector3.Dot(n, u2) / Vector3.Dot(n, u2 - v2);
                    // compare to -1e-6 rather than 0 to prevent approximation errors with points on the edge
                    if (Vector3.Dot(n, u2) >= -1e-6 && Vector3.Dot(n, v2) < -1e-6)
                    {
                        new_trans.Add(u2);
                        new_trans.Add(x);
                    }
                    else if (Vector3.Dot(n, u2) < -1e-6 && Vector3.Dot(n, v2) >= -1e-6)
                    {
                        new_trans.Add(x);
                    }
                    else if (Vector3.Dot(n, u2) >= -1e-6 && Vector3.Dot(n, v2) >= -1e-6)
                    {
                        new_trans.Add(u2);
                    }
                    // if u2 and v2 are on the wrong side we continue
                }
                normalized_trans = Distinct(new_trans);
                // normalize c2 again
                for (int j = 0; j < normalized_trans.Count; j++)
                    normalized_trans[j] = normalized_trans[j].normalized;
            }
        }
        // if the intersection of c1 and c2 becomes null then return the closest point of c1 to c2
        if (normalized_trans.Count == 0)
            return false;
        else if (line_transition)
        {
            // if the transition is a line, project normalized_trans on that line

            fov = new List<Vector3>();
            normalized_fov = new List<Vector3>();
            
            foreach (Vector3 v in normalized_trans)
            {
                Vector3 x = ProjectOnLine(v, transition); 
                fov.Add(x);
                normalized_fov.Add((x - anchorPoint).normalized);
            }
        }
        else
        {
            if (transition[0] == transition[1] && transition[0] == transition[2])
                // if the transition is just one point then we have reached the target
                return true;

            // fov is the intersection of the vision cone and transition
            fov = new List<Vector3>();
            normalized_fov = new List<Vector3>();
            Plane trans = new Plane (Vector3.Cross(transition[0] - transition[1], transition[0] - transition[2]), transition[0]);
            foreach (Vector3 v in normalized_trans)
            {
                Ray ray = new Ray(anchorPoint, v);
                float enter = 0.0f;
                if (trans.Raycast(ray, out enter))
                {
                    fov.Add(ray.GetPoint(enter));
                    normalized_fov.Add((ray.GetPoint(enter) - anchorPoint).normalized);
                }
                else
                    Debug.LogError("plane raycast error");
            }
            fov = Distinct(fov);
            normalized_fov = Distinct(normalized_fov);
        }
        return true;
    }

    public List<Vector3> HandleLine(List<Vector3> surface)
    {
        if (surface.Count == 1)
            return surface;
        Vector3 u = surface[0];
        Vector3 v = surface[1];
        Vector3 n = Vector3.Cross(u, v);
        bool line = true;
        for (int i = 0; i < surface.Count; i++)
        {
            if (Vector3.Dot(n, surface[i]) != 0)
                line = false;
        }
        // if the entire surface is coplanar with the anchor point, reduce it to its 2 extremal points
        if (line)
        {
            //Debug.Log("handling line special case");
            float a_min = 0;
            float a_max = 0;
            Vector3 u_min = u;
            Vector3 u_max = u;
            Vector3 axis = Vector3.Cross(u, v);
            foreach(Vector3 x in surface)
            {
                float a = Vector3.SignedAngle(u, x, axis);
                if (a <= a_min)
                {
                    a_min = a;
                    u_min = x;
                }
                if (a >= a_max)
                {
                    a_max = a;
                    u_max = x;
                }
            }
            surface = new List<Vector3> { u_min, u_max };
        }
        return surface;
    }

    public Vector3 ProjectOnLine(Vector3 v, List<Vector3> transition)
    {
        Vector3 x = new Vector3();
        float min_dist = Mathf.Infinity;
        Vector3 axis = Vector3.Cross(transition[0] - transition[1], transition[1] - transition[2]).normalized;
        for (int i=0; i<transition.Count; i++)
        {
            Vector3 a = transition[i];
            Vector3 b = transition[(i + 1) % transition.Count];
            Vector3 direction = (b-a).normalized;
            Vector3 p = Vector3.Scale(anchorPoint, direction) + Vector3.Scale(Vector3.one - direction, a);
            Vector3 x_tmp = anchorPoint + v / Vector3.Dot(v, p - anchorPoint) * Vector3.Dot(p - anchorPoint, p - anchorPoint);
            // check that the potential intersection is on the segment and take the closest
            if (Vector3.SignedAngle(x_tmp - anchorPoint, a-anchorPoint, axis) * Vector3.SignedAngle(x_tmp - anchorPoint, b-anchorPoint, axis) <= 0 && Vector3.Distance(anchorPoint, x_tmp) < min_dist)
            {
                x = x_tmp;
                min_dist = Vector3.Distance(anchorPoint, x_tmp);
            }
        }
        return x;
    }

    public List<Vector3> GetTransitionSurface(CustomNodeScriptable cn)
    {
        List<Vector3> li = new List<Vector3> {
                cn.position - cn.scale.y / 2 * Vector3.up - cn.scale.z / 2 * Vector3.forward,
                cn.position + cn.scale.y / 2 * Vector3.up - cn.scale.z / 2 * Vector3.forward,
                cn.position + cn.scale.y / 2 * Vector3.up + cn.scale.z / 2 * Vector3.forward,
                cn.position - cn.scale.y / 2 * Vector3.up + cn.scale.z / 2 * Vector3.forward
            };
        if (cn.scale.x < 1e-5)
            return new List<Vector3> {
                cn.position - cn.scale.y / 2 * Vector3.up - cn.scale.z / 2 * Vector3.forward,
                cn.position + cn.scale.y / 2 * Vector3.up - cn.scale.z / 2 * Vector3.forward,
                cn.position + cn.scale.y / 2 * Vector3.up + cn.scale.z / 2 * Vector3.forward,
                cn.position - cn.scale.y / 2 * Vector3.up + cn.scale.z / 2 * Vector3.forward
            };
        else if (cn.scale.y < 1e-5)
            return new List<Vector3> {
                cn.position - cn.scale.x / 2 * Vector3.right - cn.scale.z / 2 * Vector3.forward,
                cn.position + cn.scale.x / 2 * Vector3.right - cn.scale.z / 2 * Vector3.forward,
                cn.position + cn.scale.x / 2 * Vector3.right + cn.scale.z / 2 * Vector3.forward,
                cn.position - cn.scale.x / 2 * Vector3.right + cn.scale.z / 2 * Vector3.forward
            };
        // scale.x, y or z must be equal to 0
        else
            return new List<Vector3> {
                cn.position - cn.scale.x / 2 * Vector3.right - cn.scale.y / 2 * Vector3.up,
                cn.position + cn.scale.x / 2 * Vector3.right - cn.scale.y / 2 * Vector3.up,
                cn.position + cn.scale.x / 2 * Vector3.right + cn.scale.y / 2 * Vector3.up,
                cn.position - cn.scale.x / 2 * Vector3.right + cn.scale.y / 2 * Vector3.up
            };
    }

    // returns the point of c1 that is the closest to c2 (assuming that c1 and c2 are rectangles)
    public Vector3 GetClosest(List<Vector3> c1, List<Vector3> c2)
    {
        // compute the min and max coordinates of c1 and c2
        Vector3 closest = new Vector3();
        Vector3 c1_min = Vector3.positiveInfinity;
        Vector3 c1_max = Vector3.negativeInfinity;
        Vector3 c2_min = Vector3.positiveInfinity;
        Vector3 c2_max = Vector3.negativeInfinity;
        foreach (Vector3 u in c1)
        {
            c1_min = Vector3.Min(c1_min, u);
            c1_max = Vector3.Max(c1_max, u);
        }
        foreach (Vector3 v in c2)
        {
            c2_min = Vector3.Min(c2_min, v);
            c2_max = Vector3.Max(c2_max, v);
        }
        
        for (int i=0; i<3; i++)
        {
            if (c2_min[i] > c1_max[i])
                closest[i] = c1_max[i];
            else if (c1_min[i] > c2_max[i])
                closest[i] = c1_min[i];
            else
            {
                // when the closest points between c1 and c2 form a line, get the point on the line that is the closest to anchor_point
                float min = Mathf.Max(c1_min[i], c2_min[i]);
                float max = Mathf.Min(c1_max[i], c2_max[i]);
                if (anchorPoint[i] > max)
                    closest[i] = max;
                else if (anchorPoint[i] < min)
                    closest[i] = min;
                else
                    closest[i] = anchorPoint[i];

            }
        }

        return closest;
    }

    // returns the closest point between c1's corners and along its edges to c2 (rectangle)
    public Vector3 GetClosestApprox( List<Vector3> c1, List<Vector3> c2)
    {
        int approx = 50;
        float min_dist = float.PositiveInfinity;
        Vector3 closest = new Vector3();
        Vector3 closest2 = new Vector3();
        List<Vector3> approx_c1 = new List<Vector3>();
        // sample points along the edges of c1 TO FIX
        for (int i = 0; i < c1.Count; i++)
        {
            Vector3 u = c1[i];
            Vector3 v = c1[(i + 1) % c1.Count];
            for (int j = 0; j < approx; j++)
                approx_c1.Add(u + (float)j / approx * (v-u));
        }
        foreach (Vector3 u1 in approx_c1)
        { 
            float dist = DistToRect(u1, c2) + Vector3.Distance(anchorPoint, u1);
            if (dist < min_dist)
            {
                min_dist = dist;
                closest = u1;
                closest2 = u1;
            }
            else if (dist == min_dist)
                // in case the closest part of c1 to c2 is a segment, store its extremities
                closest2 = u1;
        }
        // if c1 is different than c2 return the closest point in [c1,c2] from the anchor point
        if (closest != closest2)
        {
            Vector3 p = Vector3.Project(anchorPoint - closest, closest2 - closest);
            if (Vector3.Dot(p, closest2 - closest) < 0) { }
            // closest is the closest to anchor point
            else if (Vector3.Dot(p, closest2 - closest) > (closest2 - closest).sqrMagnitude)
                closest = closest2;
            else
                // if the closest point to anchor is between the two points
                closest = p + closest;
        }
        return closest;
    }

    public float DistToRect(Vector3 v, List<Vector3> rect)
    {
        Vector3 rmin = Vector3.positiveInfinity;
        Vector3 rmax = Vector3.negativeInfinity;
        float dist = 0;
        foreach (Vector3 u in rect)
        {
            rmin = Vector3.Min(rmin, u);
            rmax = Vector3.Max(rmax, u);
        }
        for (int i=0;i<3;i++)
        {
            if (v[i] < rmin[i])
                dist += Mathf.Pow(v[i] - rmin[i], 2);
            if (v[i] > rmax[i])
                dist += Mathf.Pow(v[i] - rmax[i], 2);
        }
        return Mathf.Pow(dist, 0.5f);
    }

    public void DrawPath(List<Vector3> path, Color color)
    {
        GameObject drawPath = new GameObject();
        drawPath.name = "DrawPath";
        for (int i = 0; i < path.Count ; i++)
        {
            Vector3 u = path[i];
            Vector3 v = path[(i + 1) % path.Count];
            GameObject myLine = new GameObject();
            myLine.transform.parent = drawPath.transform;
            myLine.AddComponent<LineRenderer>();
            LineRenderer lr = myLine.GetComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
            lr.SetPosition(0, u);
            lr.SetPosition(1, v);
            Destroy(myLine, 1000);
        }
    }

    public List<Vector3> Distinct(List<Vector3> li)
    {
        List<Vector3> distinct_li = new List<Vector3>();
        for (int i=0; i<li.Count; i++)
        {
            bool seen = false;
            for (int j=0; j<i; j++)
            {
                if (Vector3.Distance(li[i], li[j]) < 1e-6)
                    seen = true;
            }
            if (!seen)
                distinct_li.Add(li[i]);
        }
        return distinct_li;
    }
}
