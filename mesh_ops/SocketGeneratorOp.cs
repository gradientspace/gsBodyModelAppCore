using System;
using System.Collections.Generic;
using System.Threading;
using g3;
using gs;

namespace gsbody
{
    public class SocketGeneratorOp : DeviceGeneratorOp
    {

        SocketConnector connector;
        object connector_lock = new object();
        public SocketConnector Connector {
            get { return connector; }
            set {
                lock(connector_lock) { 
                    if (connector != null) {
                        connector.ConnectorModifiedEvent += Connector_ConnectorModifiedEvent;
                        connector = null;
                    }
                    connector = value;
                    if (connector != null)
                        connector.ConnectorModifiedEvent += Connector_ConnectorModifiedEvent;
                }
                invalidate();
            }
        }
        private void Connector_ConnectorModifiedEvent() { invalidate();  }




        public SocketGeneratorOp() : base()
        {
            // set initial connector
            //SocketConnector default_connector = new Type1SocketConnector();
            SocketConnector default_connector = new OttobockSocketAdapter();
            Connector = default_connector;
        }



        protected override void do_base(DMesh3 mesh, bool bFastPreview, out bool bFailed)
        {
            // can early-out if we are in sculpting. but need to let somebody higher
            // up know, in case they need to force a full compute?
            // (maybe add flag that invalidates if modified?)
            bool no_connector = (Connector == null);
            bool skip_connector = false;
            bFailed = false;
            try {
                skip_connector = no_connector || bFastPreview;
                if (skip_connector == false) {
                    bFailed = (compute_connector(mesh) == false);
                }
            } catch (Exception e) {
                bFailed = true;
            }
        }




        double TargetMeshEdgeLength = 3.0;

        // appends connector to mesh. This one is messy
        bool compute_connector(DMesh3 mesh)
        {
            if (DebugStep <= 2)
                return true;

            AxisAlignedBox3d socketBounds = mesh.CachedBounds;
            Vector3d c = socketBounds.Center;
            c.y = socketBounds.Min.y + ConnectorCutHeight;

            /*
             * first we select the outer shell triangles, and plane-cut them some ways up from the bottom.
             * This creates an open loop that we hang onto
             */

            MeshPlaneCut outer_cut = new MeshPlaneCut(mesh, c, CutPlaneNormal);
            outer_cut.CutFaceSet = new MeshFaceSelection(mesh, LastExtrudeOuterGroupID);
            outer_cut.Cut();

            MeshPlaneCut inner_cut = null;
            if (Connector.HasInner) {
                inner_cut = new MeshPlaneCut(mesh, c, CutPlaneNormal);
                inner_cut.CutFaceSet = new MeshFaceSelection(mesh, LastInnerGroupID);
                inner_cut.Cut();
            }

            if (DebugStep <= 3)
                return true;

            // save the loops we created
            EdgeLoop outerCutLoop = outer_cut.CutLoops[0];
            EdgeLoop innerCutLoop = (inner_cut != null) ? inner_cut.CutLoops[0] : null;
            AxisAlignedBox3d cutLoopBounds =
                BoundsUtil.Bounds(outerCutLoop.Vertices, (vid) => { return mesh.GetVertex(vid); });

            /*
             * Now we append the connector mesh and find its open loop
             */

            // figure out where we want to position connector (which will be centered below origin)
            double dy = 0; // socketBounds.Min.y;
            if (socketBounds.Min.y < 0)
                dy = socketBounds.Min.y + 5.0;
            Vector3d shiftxz = Vector3d.Zero;// new Vector3d(cutLoopBounds.Center.x, 0, cutLoopBounds.Center.z);
            Vector3d shiftxyz = shiftxz + dy * Vector3d.AxisY;

            // append the connector
            // [TODO] do we need to hold the lock this entire time? I guess so since it would
            //   be a disaster if connector changes...
            bool bConnectorOK = true;
            lock (connector_lock) {
                SocketConnector.AppendInfo append_info =
                    Connector.AppendConnectorTo(mesh, shiftxyz);

                if (DebugStep <= 4)
                    return true;

                // [TODO] push out of inner offset here...
                if ( Connector.HasInner )
                    merge_loops(mesh, innerCutLoop, append_info.InnerLoop, false);
                // [TODO] in addition to pushing out of outer offset, we should also make sure
                //   we are far enough away from inner merge surface...
                merge_loops(mesh, outerCutLoop, append_info.OuterLoop, true);

                bConnectorOK = Connector.CutHoles(mesh, shiftxyz);
                if (bConnectorOK == false)
                    f3.DebugUtil.Log("Connector.CutHoles failed!");
            }

            return bConnectorOK;
        }





        protected void merge_loops(DMesh3 mesh, EdgeLoop cutLoop, EdgeLoop connectorLoop, bool is_outer)
        {
            /*
             * To join the loops, we are going to first make a circle, then snap both
             * open loops to that circle. Then we sample a set of vertices on the circle
             * and remesh the loops while also snapping them to the circle vertices.
             * The result is two perfectly-matching edge loops.
             */

            //AxisAlignedBox3d cutLoopBounds =
            //    BoundsUtil.Bounds(cutLoop.Vertices, (vid) => { return mesh.GetVertex(vid); });

            AxisAlignedBox3d cutLoopBounds = cutLoop.GetBounds();
            AxisAlignedBox3d connectorLoopBounds = connectorLoop.GetBounds();
            Vector3d midPt = (cutLoopBounds.Center + connectorLoopBounds.Center) * 0.5;
            double midY = midPt.y;

            // this mess construcst the circle and the sampled version
            Frame3f circFrame = new Frame3f(midPt);
            Circle3d circ = new Circle3d(circFrame, connectorLoopBounds.Width * 0.5, 1);
            DistPoint3Circle3 dist = new DistPoint3Circle3(Vector3d.Zero, circ);
            DCurve3 sampled = new DCurve3();
            double target_edge_len = TargetMeshEdgeLength;
            int N = (int)(circ.ArcLength / target_edge_len);
            for (int k = 0; k < N; ++k)
                sampled.AppendVertex(circ.SampleT((double)k / (double)N));

            MergeProjectionTarget circleTarget = new MergeProjectionTarget() {
                Mesh = mesh, CircleDist = dist, CircleLoop = sampled
            };

            EdgeLoop[] loops = new EdgeLoop[2] { cutLoop, connectorLoop };
            EdgeLoop[] outputLoops = new EdgeLoop[2];   // loops after this remeshing/etc (but might be missing some verts/edges!)
            for (int li = 0; li < 2; ++li) {
                EdgeLoop loop = loops[li];

                // snap the loop verts onto the analytic circle
                foreach (int vid in loop.Vertices) {
                    Vector3d v = mesh.GetVertex(vid);
                    dist.Point = new Vector3d(v.x, midY, v.z);
                    mesh.SetVertex(vid, dist.Compute().CircleClosest);
                }

                if (DebugStep <= 5)
                    continue;

                // remesh around the edge loop while we snap it to the sampled circle verts
                EdgeLoopRemesher loopRemesh = new EdgeLoopRemesher(mesh, loop) { LocalSmoothingRings = 3 };
                loopRemesh.EnableParallelProjection = false;
                loopRemesh.SetProjectionTarget(circleTarget);
                loopRemesh.SetTargetEdgeLength(TargetMeshEdgeLength);
                loopRemesh.SmoothSpeedT = 0.5f;
                for (int k = 0; k < 5; ++k)
                    loopRemesh.BasicRemeshPass();
                loopRemesh.SmoothSpeedT = 0;
                for (int k = 0; k < 2; ++k)
                    loopRemesh.BasicRemeshPass();
                EdgeLoop newLoop = loopRemesh.OutputLoop;
                outputLoops[li] = newLoop;

                if (DebugStep <= 6)
                    continue;

                // hard snap the loop vertices to the sampled circle verts
                foreach (int vid in newLoop.Vertices) {
                    Vector3d v = mesh.GetVertex(vid);
                    v = circleTarget.Project(v, vid);
                    mesh.SetVertex(vid, v);
                }

                // [TODO] we could re-order newLoop verts/edges to match the sampled verts order,
                // then the pair of loops would be consistently ordered (currently no guarantee)

                if (DebugStep <= 7)
                    continue;

                // collapse any degenerate edges on loop (just in case)
                // DANGER: if this actually happens, then outputLoops[li] has some invalid verts/edges!
                foreach (int eid in newLoop.Edges) {
                    if (mesh.IsEdge(eid)) {
                        Index2i ev = mesh.GetEdgeV(eid);
                        Vector3d a = mesh.GetVertex(ev.a), b = mesh.GetVertex(ev.b);
                        if (a.Distance(b) < TargetMeshEdgeLength * 0.001) {
                            DMesh3.EdgeCollapseInfo collapse;
                            mesh.CollapseEdge(ev.a, ev.b, out collapse);
                        }
                    }
                }
            }

            if (DebugStep <= 7)
                return;


            /*
             * Ok now we want to merge the loops and make them nice
             */

            // would be more efficient to find loops and stitch them...
            MergeCoincidentEdges merge = new MergeCoincidentEdges(mesh);
            merge.Apply();

            // fill any fail-holes??                

            // remesh merge region
            MeshVertexSelection remesh_roi_v = new MeshVertexSelection(mesh);
            remesh_roi_v.Select(outputLoops[0].Vertices);
            remesh_roi_v.Select(outputLoops[1].Vertices);
            remesh_roi_v.ExpandToOneRingNeighbours(5);
            MeshFaceSelection remesh_roi = new MeshFaceSelection(mesh, remesh_roi_v, 1);
            remesh_roi.LocalOptimize(true, true);

            IProjectionTarget projTarget = null;
            if (is_outer) {
                projTarget = new NoPenetrationProjectionTarget() { Spatial = this.OuterOffsetMeshSpatial };
            } else {
                projTarget = new NoPenetrationProjectionTarget() { Spatial = this.InnerOffsetMeshSpatial };
            }

            RegionRemesher join_remesh =
                RegionRemesher.QuickRemesh(mesh, remesh_roi.ToArray(), TargetMeshEdgeLength, 0.5, 5, projTarget);

            if (DebugStep <= 8)
                return;


            if ( false && is_outer ) {
                Func<int, bool> filterF = (tid) => {
                    return mesh.GetTriCentroid(tid).y > connectorLoopBounds.Max.y;
                };

                MeshFaceSelection tris = new MeshFaceSelection(mesh);
                foreach (int tid in join_remesh.CurrentBaseTriangles) {
                    if (filterF(tid))
                        tris.Select(tid);
                }
                tris.ExpandToOneRingNeighbours(5, filterF);

                MeshVertexSelection verts = new MeshVertexSelection(mesh, tris);
                MeshIterativeSmooth smooth = new MeshIterativeSmooth(mesh, verts.ToArray(), true);
                smooth.Alpha = 1.0f;
                smooth.Rounds = 25;
                smooth.ProjectF = (v, n, vid) => { return projTarget.Project(v); };
                smooth.Smooth();
            }


            // [RMS] this smooths too far. we basically only want to smooth 'up' from top of socket...
            //LaplacianMeshSmoother.RegionSmooth(mesh, join_remesh.CurrentBaseTriangles, 1, 10);

            // need to post-enforce thickness, which we aren't doing above - we could though
        }




        // projection target that snaps boundary vertices to circle
        class MergeProjectionTarget : IProjectionTarget
        {
            public DMesh3 Mesh;
            public DistPoint3Circle3 CircleDist;
            public DCurve3 CircleLoop;
            public Vector3d Project(Vector3d vPoint, int identifier)
            {
                if (Mesh.IsVertex(identifier) && Mesh.IsBoundaryVertex(identifier)) {
                    int nearv = CircleLoop.NearestVertex(vPoint);
                    return CircleLoop[nearv];
                } else
                    return vPoint;
            }
        }


        // projection target that pushes points outside of surface
        class NoPenetrationProjectionTarget : IProjectionTarget
        {
            public DMeshAABBTree3 Spatial;
            public Vector3d Project(Vector3d vPoint, int identifier)
            {
                if (Spatial.IsInside(vPoint)) {
                    return MeshQueries.NearestPointFrame(Spatial.Mesh, Spatial, vPoint).Origin;
                } else
                    return vPoint;
            }
        }




    }
}
