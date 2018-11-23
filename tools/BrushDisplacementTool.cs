using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using gs;
using f3;

namespace gsbody
{
    public class BrushDisplacementToolBuilder : SurfaceBrushToolBuilder
    {
        protected override SurfaceBrushTool new_tool(FScene scene, DMeshSO target)
        {
            return new BrushDisplacementTool(scene, target);
        }
    }




    public class BrushDisplacementTool : SurfaceBrushTool
    {
        VectorDisplacementMapOp op;
        VectorDisplacement Map;
        public VectorDisplacementMapOp DisplacementOp {
            get { return op; }
            set {
                op = value;
                Map = op.GetMapCopy();
            }
        }
        
        // this is used internally to provide map to brushes
        VectorDisplacement get_map() { return Map; }

        public override SurfaceBrushType PrimaryBrush {
            set {
                (value as VectorDisplacementBaseBrush).MapSourceF = this.get_map;
                base.PrimaryBrush = value;
            }
        }

        public override SurfaceBrushType SecondaryBrush {
            set {
                (value as VectorDisplacementBaseBrush).MapSourceF = this.get_map;
                base.SecondaryBrush = value;
            }
        }
        

        public BrushDisplacementTool(FScene scene, DMeshSO target) : base(scene, target)
        {
        }



        public override void Setup()
        {
            base.Setup();

            PrimaryBrush = new VectorDisplacementOffsetBrush();
            SecondaryBrush = new VectorDisplacementSmoothBrush();
            //SecondaryBrush = new VectorDisplacementEraseBrush();
        }

        protected override void update_stroke(Frame3f vLocalF, int nHitTID)
        {
            base.update_stroke(vLocalF, nHitTID);
            if (DisplacementOp != null)
                DisplacementOp.UpdateMap(Map);
        }

        //protected override void end_stroke()
        //{
        //    base.end_stroke();
        //    if (DisplacementOp != null)
        //        DisplacementOp.UpdateMap(Operation.Map);
        //}
    }




    public abstract class VectorDisplacementBaseBrush : SurfaceBrushType
    {
        public Func<VectorDisplacement> MapSourceF;

        DijkstraGraphDistance dist;
        DijkstraGraphDistance Dijkstra {
            get {
                if (dist == null) {
                    dist = new DijkstraGraphDistance(Mesh.MaxVertexID, false, Mesh.IsVertex, vertex_dist, Mesh.VtxVerticesItr);
                    dist.TrackOrder = true;
                }
                return dist;
            }
        }


        float vertex_dist(int a, int b)
        {
            return (float)Mesh.GetVertex(a).Distance(Mesh.GetVertex(b));
        }


        protected override void mesh_changed()
        {
            dist = null;
        }


        public override void Apply(Frame3f vNextPos, int tid)
        {
            if ( MapSourceF == null ) {
                f3.DebugUtil.Log("[VectorDisplacementBaseBrush] .MapSourceF is null!");
                return;
            }
            VectorDisplacement use_map = MapSourceF();
            if (use_map == null || use_map.Count != Mesh.MaxVertexID)
                return;

            DijkstraGraphDistance dj = Dijkstra;
            dj.Reset();

            Index3i ti = Mesh.GetTriangle(tid);
            Vector3d c = Mesh.GetTriCentroid(tid);
            dj.AddSeed(ti.a, (float)Mesh.GetVertex(ti.a).Distance(c));
            dj.AddSeed(ti.b, (float)Mesh.GetVertex(ti.b).Distance(c));
            dj.AddSeed(ti.c, (float)Mesh.GetVertex(ti.c).Distance(c));
            double compute_Dist = MathUtil.SqrtTwo * Radius;
            dj.ComputeToMaxDistance((float)compute_Dist);

            ApplyCurrentStamp(vNextPos, tid, dj, use_map);

            mesh_changed();
        }


        /// <summary>
        /// Subclass implements this
        /// </summary>
        protected abstract void ApplyCurrentStamp(Frame3f vCenter, int tid, DijkstraGraphDistance dj, VectorDisplacement map);

    }



    /// <summary>
    /// This brush adds a soft radial displacement along the center triangle face normal
    /// </summary>
    public class VectorDisplacementOffsetBrush : VectorDisplacementBaseBrush
    {
        public double Power = 0.1;

        protected override void ApplyCurrentStamp(Frame3f vCenter, int tid, DijkstraGraphDistance dj, VectorDisplacement map)
        {
            Vector3d n = Mesh.GetTriNormal(tid);

            List<int> order = dj.GetOrder();
            int N = order.Count;
            for (int k = 0; k < N; ++k) {
                int vid = order[k];
                //double d = dj.GetDistance(vid);
                double d = (Mesh.GetVertex(vid) - vCenter.Origin).Length;
                double t = MathUtil.Clamp(d / Radius, 0.0, 1.0);
                t = MathUtil.WyvillFalloff01(t);
                Vector3d offset = Power * t * n;
                if ( Invert )
                    map[vid] = map[vid] - offset;
                else
                    map[vid] = map[vid] + offset;
            }
        }
    }



    /// <summary>
    /// This brush "erases" the displacement vectors by setting them to zero
    /// </summary>
    public class VectorDisplacementEraseBrush : VectorDisplacementBaseBrush
    {
        public double Power = 0.1;

        protected override void ApplyCurrentStamp(Frame3f vCenter, int tid, DijkstraGraphDistance dj, VectorDisplacement map)
        {
            List<int> order = dj.GetOrder();
            int N = order.Count;
            for (int k = 0; k < N; ++k) {
                int vid = order[k];
                double d = (Mesh.GetVertex(vid) - vCenter.Origin).Length;
                double t = MathUtil.Clamp(d / Radius, 0.0, 1.0);
                t = Power * MathUtil.WyvillFalloff01(t);
                map[vid] = Vector3d.Lerp(map[vid], Vector3d.Zero, t);
            }
        }
    }





    /// <summary>
    /// This brush smooths the current displacement map by laplacian smoothing of
    /// the current offset vectors. 
    /// The smoothing is applied inline, so the results are affected by vertex ordering.
    /// </summary>
    public class VectorDisplacementSmoothBrush : VectorDisplacementBaseBrush
    {
        public double SmoothPower = 0.5;

        protected override void ApplyCurrentStamp(Frame3f vCenter, int tid, DijkstraGraphDistance dj, VectorDisplacement map)
        {
            Vector3d c = Vector3d.Zero, v = Vector3d.Zero, o = vCenter.Origin;
            double r = Radius;

            List<int> order = dj.GetOrder();
            int N = order.Count;
            for (int k = 0; k < N; ++k) {
                int vid = order[k];
                v = Mesh.GetVertex(vid);
                double d = v.Distance(ref o);
                double t = MathUtil.Clamp(d / r, 0.0, 1.0);
                t = 1 - t * t;
                t = t * t * t;
                //t = MathUtil.WyvillFalloff01(t);


                v = map[vid];
                c = Vector3d.Zero; int n = 0;
                foreach (int nbrid in Mesh.VtxVerticesItr(vid)) {
                    c += map[nbrid];
                    n++;
                }
                c /= n;

                map[vid] = Vector3d.Lerp(ref v, ref c, SmoothPower * t);
            }
        }
    }





}
