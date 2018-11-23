using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using gs;

namespace gsbody
{
    public abstract class SocketConnector
    {

        public delegate void ConnectorModifiedHandler();

        public event ConnectorModifiedHandler ConnectorModifiedEvent;
        protected void post_modified_event()
        {
            ConnectorModifiedEvent?.Invoke();
        }


        public double TargetEdgeLength = 2.0;      // in mm


        public bool HasInner = true;

        public enum HoleModes
        {
            ThroughHole = 0,
            PartialUp = 1,
            PartialDown = 2
        }

        protected struct HoleInfo
        {
            public int nHole;

            public bool IsVertical;
            public HoleModes CutMode;

            public double Radius;

            // for vertical holes
            public Vector2d XZOffset;

            // for horizontal holes
            public double Height;
            public double AroundAngle;

            // rotation around axis (to align/etc)
            public double AxisAngleD;

            // for polygonal holes
            public int Vertices;

            // for partial holes
            public double PartialHoleBaseHeight;
            public int PartialHoleGroupID;

            // kind of hacky thing
            public Index2i GroupIDFilters;
        }
        List<HoleInfo> Holes;



        public DMesh3 InnerMesh;
        public DMesh3 OuterMesh;
        public AxisAlignedBox3d CombinedBounds;

        public EdgeLoop InnerLoop;
        public EdgeLoop OuterLoop;


        public SocketConnector()
        {
            Holes = new List<HoleInfo>();
        }


        /// <summary>
        /// subclasses must implement this to make sure geometry is valid.
        /// </summary>
        protected abstract void validate_geometry();


        public struct AppendInfo
        {
            public int InnerGID;
            public int OuterGID;

            public EdgeLoop InnerLoop;
            public EdgeLoop OuterLoop;
        }
        public AppendInfo AppendConnectorTo(DMesh3 mesh, Vector3d translate)
        {
            validate_geometry();

            AppendInfo info = new AppendInfo();

            MeshEditor editor = new MeshEditor(mesh);
            int[] mapV;

            if (HasInner) {
                info.InnerGID = mesh.AllocateTriangleGroup();
                editor.AppendMesh(InnerMesh, out mapV, info.InnerGID);
                info.InnerLoop = EdgeLoop.FromVertices(mesh, new MappedList(InnerLoop.Vertices, mapV));

                MeshTransforms.PerVertexTransform(mesh, InnerMesh.VertexIndices(),
                    (vid) => { return mapV[vid]; },
                    (v, old_vid, new_vid) => { return v + translate; });
            } else {
                info.InnerGID = -1;
                info.InnerLoop = null;
            }

            info.OuterGID = mesh.AllocateTriangleGroup();
            editor.AppendMesh(OuterMesh, out mapV, info.OuterGID);
            info.OuterLoop = EdgeLoop.FromVertices(mesh, new MappedList(OuterLoop.Vertices, mapV));

            MeshTransforms.PerVertexTransform(mesh, OuterMesh.VertexIndices(),
                (vid) => { return mapV[vid]; },
                (v, old_vid, new_vid) => { return v + translate; });

            return info;
        }




        public bool CutHoles(DMesh3 mesh, Vector3d translate)
        {
            Vector3d basePoint = CombinedBounds.Center - CombinedBounds.Extents.y * Vector3d.AxisY + translate;

            bool all_ok = true;
            foreach (HoleInfo hi in Holes) {
                bool hole_ok = false;
                switch ( hi.CutMode ) {
                    case HoleModes.ThroughHole:
                        hole_ok = CutThroughHole(mesh, hi, translate);
                        break;
                    case HoleModes.PartialDown:
                        hole_ok = CutPartialHole(mesh, hi, translate, false);
                        break;
                    case HoleModes.PartialUp:
                        hole_ok = CutPartialHole(mesh, hi, translate, true);
                        break;
                }

                all_ok = all_ok && hole_ok;
            }

            return all_ok;
        }






        /// <summary>
        /// Cut through-hole either vertically or horizontally.
        /// 
        /// One current failure mode is if we get more than two ray-hits, which
        /// can happen due pathological cases or unexpected mesh shape. Currently
        /// trying to handle the pathological cases (ie ray hits adjacent triangles cases)
        /// via sorting, not sure if this works spectacularly well.
        /// 
        /// </summary>
        protected bool CutThroughHole(DMesh3 mesh, HoleInfo hi, Vector3d translate)
        {
            Vector3d basePoint = CombinedBounds.Center - CombinedBounds.Extents.y * Vector3d.AxisY + translate;

            // do we need to compute spatial DS for each hole? not super efficient...
            DMeshAABBTree3 spatial = new DMeshAABBTree3(mesh, true);

            Vector3d origin = Vector3d.Zero;
            Vector3d direction = Vector3d.One;

            if (hi.IsVertical) {
                direction = Vector3d.AxisY;
                origin = basePoint + new Vector3d(hi.XZOffset.x, 0, hi.XZOffset.y) - 100 * direction;
            } else {
                origin = basePoint + hi.Height * Vector3d.AxisY;
                direction = Quaterniond.AxisAngleD(Vector3d.AxisY, hi.AroundAngle) * Vector3d.AxisX;
            }

            // Find upper and lower triangles that contain center-points of
            // holes we want to cut. This is the most error-prone part
            // because we depend on ray-hits, which is not very reliable...

            Ray3d ray1 = new Ray3d(origin, direction);
            Ray3d ray2 = new Ray3d(origin + 10000 * direction, -direction);

            if ( hi.GroupIDFilters.a > 0 ) {
                spatial.TriangleFilterF = (tid) => {
                    return mesh.GetTriangleGroup(tid) == hi.GroupIDFilters.a;
                };
            }
            int hit_1 = spatial.FindNearestHitTriangle(ray1);
            spatial.TriangleFilterF = null;

            if (hi.GroupIDFilters.b > 0) {
                spatial.TriangleFilterF = (tid) => {
                    return mesh.GetTriangleGroup(tid) == hi.GroupIDFilters.b;
                };
            }
            int hit_2 = spatial.FindNearestHitTriangle(ray2);
            spatial.TriangleFilterF = null;

            if (hit_1 == DMesh3.InvalidID || hit_2 == DMesh3.InvalidID)
                return false;
            if (hit_1 == hit_2)
                return false;

            List<int> hitTris = new List<int>() { hit_1, hit_2 };


            Frame3f projectFrame = new Frame3f(ray1.Origin, ray1.Direction);

            int nVerts = 32;
            if (hi.Vertices != 0)
                nVerts = hi.Vertices;
            double angleShiftRad = hi.AxisAngleD * MathUtil.Deg2Rad;
            Polygon2d circle = Polygon2d.MakeCircle(hi.Radius, nVerts, angleShiftRad);


            List<EdgeLoop> edgeLoops = new List<EdgeLoop>();
            foreach (int hit_tid in hitTris) {
                try {
                    MeshInsertProjectedPolygon insert = new MeshInsertProjectedPolygon(mesh, circle, projectFrame, hit_tid) {
                        SimplifyInsertion = true
                    };
                    if (insert.Insert()) {
                        // if we have extra edges just randomly collapse
                        EdgeLoop loop = insert.InsertedLoop;

                        if (loop.VertexCount > circle.VertexCount) {
                            loop = simplify_loop(mesh, loop, circle.VertexCount);
                        }

                        edgeLoops.Add(loop);
                    } else {
                        f3.DebugUtil.Log("insert.Insert() failed!!");
                        return false;
                    }
                } catch (Exception e) {
                    // ignore this loop but we might already be in trouble...
                    f3.DebugUtil.Log("insert.Insert() threw exception for hole {0}!!", hi.nHole);
                    f3.DebugUtil.Log(e.Message);
                }
            }
            if (edgeLoops.Count != 2) {
                return false;
            }

            try {
                MeshEditor editor = new MeshEditor(mesh);
                EdgeLoop l0 = edgeLoops[0];
                EdgeLoop l1 = edgeLoops[1];
                l1.Reverse();
                editor.StitchVertexLoops_NearestV(l0.Vertices, l1.Vertices);

                // split edges around the holes we cut. This is helpful
                // if we are going to do additional operations in these areas,
                // as it gives us extra rings to work with
                //MeshEdgeSelection edges = new MeshEdgeSelection(mesh);
                //edges.SelectVertexEdges(l0.Vertices);
                //edges.SelectVertexEdges(l1.Vertices);
                //DMesh3.EdgeSplitInfo splitInfo;
                //foreach ( int eid in edges )
                //    mesh.SplitEdge(eid, out splitInfo);

                return true;
            } catch {
                f3.DebugUtil.Log("stitch threw exception!");
                return false;
            }
        }






        /// <summary>
        /// Cut a "partial" hole, ie we cut the mesh with the polygon once, and then
        /// extrude downwards to a planar version of the cut boundary.
        /// 
        /// Currently only supports extruding downwards from topmost intersection.
        /// 
        /// </summary>
        protected bool CutPartialHole(DMesh3 mesh, HoleInfo hi, Vector3d translate, bool bUpwards)
        {
            if (hi.IsVertical == false)
                throw new Exception("unsupported!");

            Vector3d basePoint = CombinedBounds.Center - CombinedBounds.Extents.y * Vector3d.AxisY + translate;

            // do we need to compute spatial DS for each hole? not super efficient...
            DMeshAABBTree3 spatial = new DMeshAABBTree3(mesh, true);

            Vector3d direction = (bUpwards) ? Vector3d.AxisY : -Vector3d.AxisY;
            Vector3d center = basePoint + new Vector3d(hi.XZOffset.x, 0, hi.XZOffset.y) - 10000 * direction;


            Ray3d ray = new Ray3d(center, direction);
            int hit_tid = spatial.FindNearestHitTriangle(ray);
            if (hit_tid == DMesh3.InvalidID)
                return false;

            IntrRay3Triangle3 intersection = MeshQueries.TriangleIntersection(mesh, hit_tid, ray);
            Vector3d inter_pos = ray.PointAt(intersection.RayParameter);

            Frame3f projectFrame = new Frame3f(ray.Origin, ray.Direction);

            int nVerts = 32;
            if (hi.Vertices != 0)
                nVerts = hi.Vertices;
            double angleShiftRad = hi.AxisAngleD * MathUtil.Deg2Rad;
            Polygon2d circle = Polygon2d.MakeCircle(hi.Radius, nVerts, angleShiftRad);

            try {
                EdgeLoop loop = null;

                MeshInsertProjectedPolygon insert = new MeshInsertProjectedPolygon(mesh, circle, projectFrame, hit_tid) {
                    SimplifyInsertion = false
                };
                if (insert.Insert()) {
                    loop = insert.InsertedLoop;

                    // [RMS] do we need to simplify for this one?
                    //if (loop.VertexCount > circle.VertexCount)
                    //    loop = simplify_loop(mesh, loop, circle.VertexCount);

                    MeshEditor editor = new MeshEditor(mesh);

                    Vector3d base_pos = inter_pos;
                    base_pos.y = basePoint.y + hi.PartialHoleBaseHeight;

                    int N = loop.VertexCount;
                    int[] newLoop = new int[N];
                    for (int k = 0; k < N; ++k) {
                        newLoop[k] = mesh.AppendVertex(mesh, loop.Vertices[k]);
                        Vector3d cur_v = mesh.GetVertex(newLoop[k]);
                        cur_v.y = base_pos.y;
                        mesh.SetVertex(newLoop[k], cur_v);
                    }
                    int base_vid = mesh.AppendVertex(base_pos);
                    int[] fan_tris = editor.AddTriangleFan_OrderedVertexLoop(base_vid, newLoop);
                    FaceGroupUtil.SetGroupID(mesh, fan_tris, hi.PartialHoleGroupID);
                    int[] stitch_tris = editor.StitchLoop(loop.Vertices, newLoop);

                    // need to remesh fan region because otherwise we get pathological cases
                    RegionRemesher remesh = new RegionRemesher(mesh, fan_tris);
                    remesh.SetTargetEdgeLength(2.0);
                    remesh.SmoothSpeedT = 1.0;
                    remesh.PreventNormalFlips = true;
                    for (int k = 0; k < 25; ++k)
                        remesh.BasicRemeshPass();
                    //remesh.EnableCollapses = remesh.EnableFlips = remesh.EnableSplits = false;
                    //for (int k = 0; k < 20; ++k)
                    //    remesh.BasicRemeshPass();
                    remesh.BackPropropagate();

                    return true;

                } else {
                    return false;
                }
            } catch (Exception e) {
                f3.DebugUtil.Log("partial hole {0} failed!! {1}", hi.nHole, e.Message);
                return false;
            }
        }





        /// <summary>
        /// This function tries to remove vertices from loop to hit TargetVtxCount. We call this
        /// when polygon-insertion returns more vertices than polygon. Strategy is to try to find
        /// co-linear vertices, ie that can be removed without changing shape. If that fails, 
        /// we remove vertices that result in smallest length change (probably should do proper simplification
        /// here instead).
        /// 
        /// Basically this is to handle failures in MeshInsertUVPolyCurve.Simplify(), which sometimes
        /// fails to remove extra vertices because it would case triangle flips. Here we don't
        /// care about triangle flips.
        /// 
        /// Note that if the input polygon had splits on edges, this function would remove those
        /// vertices. Which is not ideal. 
        /// </summary>
        protected EdgeLoop simplify_loop(DMesh3 mesh, EdgeLoop loop, int TargetVtxCount)
        {
            while (loop.VertexCount > TargetVtxCount) {
                DCurve3 curve = loop.ToCurve();

                DMesh3.EdgeCollapseInfo cinfo;

                int NV = loop.VertexCount;
                for (int k = 1; k <= NV; ++k) {
                    int prevv = k - 1;
                    int curv = k % NV;
                    int nextv = (k + 1) % NV;
                    //if (curve[prevv].Distance(curve[curv]) < 0.0001 ||
                    //        curve[nextv].Distance(curve[curv]) < 0.0001)
                    //    f3.DebugUtil.Log("DEGENERATE!!");
                    double angle = curve.OpeningAngleDeg(curv);
                    if (angle > 179) {
                        MeshResult r = mesh.CollapseEdge(loop.Vertices[prevv], loop.Vertices[curv], out cinfo);
                        mesh.SetVertex(loop.Vertices[prevv], curve[prevv]);
                        if (r == MeshResult.Ok)
                            goto done_this_iter;
                        else
                            f3.DebugUtil.Log("collinear collapse failed!");
                    }
                }
                f3.DebugUtil.Log("Did not find collinear vert...");

                int i_shortest = -1; double shortest_len = double.MaxValue;
                for (int k = 1; k <= NV; ++k) {
                    int prevv = k - 1;
                    int curv = k % NV;
                    int nextv = (k + 1) % NV;
                    Vector3d pc = curve[curv] - curve[prevv];
                    Vector3d pn = curve[nextv] - curve[curv];
                    double len_sum = pc.Length + pn.Length;
                    if (len_sum < shortest_len) {
                        i_shortest = curv;
                        shortest_len = len_sum;
                    }
                }
                if (i_shortest != -1) {
                    int curv = i_shortest;
                    int prevv = (curv == 0) ? NV - 1 : curv - 1;
                    int nextv = (curv + 1) % NV;
                    Vector3d pc = curve[curv] - curve[prevv];
                    Vector3d pn = curve[nextv] - curve[curv];
                    int iWhich = (pc.Length < pn.Length) ? prevv : nextv;
                    MeshResult r = mesh.CollapseEdge(loop.Vertices[iWhich], loop.Vertices[curv], out cinfo);
                    if (r == MeshResult.Ok)
                        goto done_this_iter;
                    else
                        f3.DebugUtil.Log("shortest failed!");
                }
                f3.DebugUtil.Log("Did not find shortest vert...");

                // if we didn't find a vert to remove yet, just arbitrarily remove first one
                int v0 = loop.Vertices[0], v1 = loop.Vertices[1];
                MeshResult result = mesh.CollapseEdge(v1, v0, out cinfo);

            done_this_iter:
                List<int> new_verts = new List<int>();
                for (int k = 0; k < loop.Vertices.Count(); ++k) {
                    if (mesh.IsVertex(loop.Vertices[k]))
                        new_verts.Add(loop.Vertices[k]);
                }
                loop = EdgeLoop.FromVertices(mesh, new_verts);
            }

            return loop;
        }









        /*
         *  These should only be called inside subclass validate_geometry() implementation
         */

        protected void reset_holes()
        {
            Holes.Clear();
        }

        protected void add_hole(HoleInfo hi)
        {
            hi.nHole = Holes.Count;
            Holes.Add(hi);
        }


        protected void set_output_meshes(DMesh3 inner, DMesh3 outer)
        {
            InnerMesh = inner;
            OuterMesh = outer;

            AxisAlignedBox3d bounds = OuterMesh.CachedBounds;
            if (InnerMesh != null)
                bounds.Contain(InnerMesh.CachedBounds);

            // position center-top at origin
            Vector3d top = bounds.Center + bounds.Extents[1] * Vector3d.AxisY;
            if (InnerMesh != null)
                MeshTransforms.Translate(InnerMesh, -top);
            MeshTransforms.Translate(OuterMesh, -top);

            CombinedBounds = OuterMesh.CachedBounds;
            if (InnerMesh != null)
                CombinedBounds.Contain(InnerMesh.CachedBounds);

            if (InnerMesh != null) { 
                var innerLoops = new MeshBoundaryLoops(InnerMesh);
                InnerLoop = innerLoops[0];
            }
            var outerLoops = new MeshBoundaryLoops(OuterMesh);
            OuterLoop = outerLoops[0];
        }


    }








}
