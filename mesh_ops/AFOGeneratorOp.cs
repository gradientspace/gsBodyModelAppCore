using System;
using System.Collections.Generic;
using System.Threading;
using g3;
using gs;

namespace gsbody
{
    public class AFOGeneratorOp : DeviceGeneratorOp
    {

        double flatten_band_height = 15.0;
        public double FlattenBandHeight {
            get { return flatten_band_height; }
            set {
                if (Math.Abs(flatten_band_height - value) > MathUtil.ZeroTolerancef) { flatten_band_height = value; invalidate(); }
            }
        }




        public AFOGeneratorOp() : base()
        {
        }



        protected override void do_base(DMesh3 mesh, bool bFastPreview, out bool bFailed)
        {
            bFailed = false;

            do_flatten(mesh);

            // no base on AFO yet
            return;
        }



        protected void do_flatten(DMesh3 mesh)
        {
            double BAND_HEIGHT = flatten_band_height;
            Vector3d down_axis = -Vector3d.AxisY;
            double dot_thresh = 0.2;

            AxisAlignedBox3d bounds = mesh.CachedBounds;
            DMeshAABBTree3 spatial = new DMeshAABBTree3(mesh, true);

            Ray3d ray = new Ray3d(bounds.Center - 2 * bounds.Height * Vector3d.AxisY, Vector3d.AxisY);
            int hit_tid = spatial.FindNearestHitTriangle(ray);
            Frame3f hitF;
            MeshQueries.RayHitPointFrame(mesh, spatial, ray, out hitF);
            Vector3d basePt = hitF.Origin;

            Frame3f basePlane = new Frame3f(basePt, Vector3f.AxisY);

            MeshConnectedComponents components = new MeshConnectedComponents(mesh) {
                FilterF = (tid) => {
                    if (mesh.GetTriangleGroup(tid) != LastExtrudeOuterGroupID)
                        return false;
                    Vector3d n, c; double a;
                    mesh.GetTriInfo(tid, out n, out a, out c);
                    double h = Math.Abs(c.y - basePt.y);
                    if (h > BAND_HEIGHT)
                        return false;
                    if (n.Dot(down_axis) < dot_thresh)
                        return false;
                    return true;
                },
                SeedFilterF = (tid) => {
                    return tid == hit_tid;
                }
            };
            components.FindConnectedT();

            MeshFaceSelection all_faces = new MeshFaceSelection(mesh);

            foreach ( var comp in components ) {
                MeshVertexSelection vertices = new MeshVertexSelection(mesh);
                vertices.SelectTriangleVertices(comp.Indices);
                foreach ( int vid in vertices ) {
                    Vector3d v = mesh.GetVertex(vid);
                    v = basePlane.ProjectToPlane((Vector3f)v, 2);
                    mesh.SetVertex(vid, v);
                }
                all_faces.SelectVertexOneRings(vertices);
            }

            all_faces.ExpandToOneRingNeighbours(3, (tid) => {
                return mesh.GetTriangleGroup(tid) == LastExtrudeOuterGroupID;
            });

            RegionRemesher r = new RegionRemesher(mesh, all_faces);
            r.SetProjectionTarget(MeshProjectionTarget.Auto(mesh));
            r.SetTargetEdgeLength(2.0f);
            r.SmoothSpeedT = 1.0f;
            for (int k = 0; k < 10; ++k)
                r.BasicRemeshPass();
            r.SetProjectionTarget(null);
            r.SmoothSpeedT = 1.0f;
            for (int k = 0; k < 10; ++k)
                r.BasicRemeshPass();
            r.BackPropropagate();
        }


    }
}
