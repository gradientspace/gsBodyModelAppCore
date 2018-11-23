using System;
using System.Collections.Generic;
using System.Threading;
using g3;
using gs;

namespace gsbody
{
    public class DeviceGeneratorOp : BaseDMeshSourceOp
    {
        Colorf socket_vtx_color = Colorf.LightGreen;
        public Colorf SocketVertexColor {
            get { return socket_vtx_color; }
            set {
                if (socket_vtx_color != value) { socket_vtx_color = value; invalidate(); }
            }
        }

        Colorf partial_vtx_color = Colorf.SelectionGold;
        public Colorf PartialSocketVertexColor {
            get { return partial_vtx_color; }
            set {
                if (partial_vtx_color != value) { partial_vtx_color = value; invalidate(); }
            }
        }


        double inner_offset = 2.0;
        public double InnerWallOffset {
            get { return inner_offset; }
            set {
                if (Math.Abs(inner_offset - value) > MathUtil.ZeroTolerancef) { inner_offset = value; invalidate(); }
            }
        }


        double thickness = 5.0;
        public double SocketThickness {
            get { return thickness; }
            set {
                if (Math.Abs(thickness - value) > MathUtil.ZeroTolerancef) { thickness = value; invalidate(); }
            }
        }

        double connector_cut_height = 25.0;
        public double ConnectorCutHeight {
            get { return connector_cut_height; }
            set {
                if (Math.Abs(connector_cut_height - value) > MathUtil.ZeroTolerancef) { connector_cut_height = value; invalidate(); }
            }
        }

        Vector3d cut_plane_normal = -Vector3d.AxisY;
        public Vector3d CutPlaneNormal {
            get { return cut_plane_normal; }
            set {
                if (cut_plane_normal.EpsilonEqual(value, MathUtil.ZeroTolerancef) == false) { cut_plane_normal = value; invalidate(); }
            }
        }

        double flare_offset = 0.0;
        public double FlareOffset {
            get { return flare_offset; }
            set {
                if (Math.Abs(flare_offset - value) > MathUtil.ZeroTolerancef) { flare_offset = value; invalidate(); }
            }
        }

        double flare_band_width = 0.0;
        public double FlareBandWidth {
            get { return flare_band_width; }
            set {
                if (Math.Abs(flare_band_width - value) > MathUtil.ZeroTolerancef) { flare_band_width = value; invalidate(); }
            }
        }

        bool flip_trim_side = false;
        public bool FlipTrimSide {
            get { return flip_trim_side; }
            set {
                if (flip_trim_side != value) { flip_trim_side = value; invalidate(); }
            }
        }


        int debug_step = int.MaxValue;
        public int DebugStep {
            get { return debug_step; }
            set { debug_step = value; invalidate(); }
        }





        /// <summary>
        /// This transform is applied to MeshSource and CurveSource. This allows the input
        /// leg to be arbitrarily transformed before generating the socket. For example it means
        /// we can allow the user to rotate the leg to change relative orientation of leg and connector.
        /// this could be done inside the MeshSource, but that would then mean it
        /// has to hold an extra copy of the mesh.
        /// </summary>
        public Frame3f InputsTransform {
            get { return inputsTransform; }
            set {
                inputsTransform = value;
                pending_cache_discard = true;
                invalidate();
            }
        }
        Frame3f inputsTransform;



        DMeshSourceOp mesh_source;
        public DMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_input_modified;
                mesh_source = value;
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_input_modified;
                invalidate();
            }
        }


        ISampledCurve3dSourceOp curve_source;
        public ISampledCurve3dSourceOp CurveSource {
            get { return curve_source; }
            set {
                if (curve_source != null)
                    curve_source.OperatorModified -= on_input_modified;
                curve_source = value;
                if (curve_source != null)
                    curve_source.OperatorModified += on_input_modified;
                invalidate();
            }
        }



        public enum ResultStatus
        {
            FullResult, PreviewResult, ErrorResult
        }
        ResultStatus last_result_status = ResultStatus.ErrorResult;
        public ResultStatus LastResultStatus {
            get { return last_result_status; }
        }



        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();

            // have to discard SDF cache if mesh source is modified
            if (op == MeshSource) {
                pending_cache_discard = true;
                input_mesh_modified_counter++;
            }
        }



        public DeviceGeneratorOp()
        {
        }





        bool pending_cache_discard = false;

        int input_mesh_modified_counter = -1;
        int input_mesh_cache_timestamp = -1;
        DMesh3 cachedInputMesh;
        DMeshAABBTree3 cachedInputMeshSpatial;

        Frame3f cachedInputsTransform;

        double cached_sdf_max_offset = 0;
        MeshSignedDistanceGrid cached_sdf;
        AxisAlignedBox3d cached_sdf_bounds;

        protected DMesh3 InnerOffsetMesh;
        protected DMeshAABBTree3 InnerOffsetMeshSpatial;
        double cached_inner_sdf_offset = 0;

        protected DMesh3 OuterOffsetMesh;
        protected DMeshAABBTree3 OuterOffsetMeshSpatial;
        double cached_outer_sdf_offset = 0;

        protected DMesh3 TrimmedMesh;
        protected DMesh3 InnerMesh;
        protected DMesh3 SocketMesh;


        protected int LastInnerGroupID = -1;
        protected int LastExtrudeOuterGroupID = -1;
        protected int LastStitchGroupID = -1;


        DMesh3 ResultMesh;


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.LastUpdateTimestamp;

            if (MeshSource == null)
                throw new Exception("TrimMeshFromCurveOp: must set valid MeshSource to compute!");
            if (CurveSource == null)
                throw new Exception("TrimMeshFromCurveOp: must set valid CurveSource to compute!");

            LocalProfiler p = new LocalProfiler();
            p.Start("offset");

            bool is_preview = false;

            // cache copy of input mesh and spatial DS
            DMesh3 inputMesh = MeshSource.GetDMeshUnsafe();
            if (cachedInputMesh == null || input_mesh_modified_counter != input_mesh_cache_timestamp) {
                cachedInputMesh = new DMesh3(inputMesh, false, MeshComponents.All);
                cachedInputMeshSpatial = new DMeshAABBTree3(cachedInputMesh, true);
                input_mesh_cache_timestamp = input_mesh_modified_counter;
            }

            // have to cache this in case it changes during compute
            // TODO: should be caching all parameters!
            cachedInputsTransform = InputsTransform;

            // discard caches 
            // TODO: we still have a race condition, because we could get an invalidate() between
            // begin_update() above and here. In that case we will be losing the cache discard flag.
            // Should be using update timestamp...
            if ( pending_cache_discard ) {
                cached_sdf_max_offset = 0;
                cached_inner_sdf_offset = 0;
                cached_outer_sdf_offset = 0;
                pending_cache_discard = true;
            }

            try {
                //compute_offset_meshes();
                compute_offset_meshes_nosdf();
            } catch {
                // we will do nothing here, let later failures handle it
            }

            p.Stop("offset");
            p.Start("trim");

            // trimmed mesh doesn't change unless curve changed...
            try {
                compute_trimmed_mesh();
            } catch {
                set_failure_output(null);
                goto failed;
            }

            p.Stop("trim");
            p.Start("inner_wall");

            try {
                compute_inner_wall();
            } catch {
                set_failure_output(null);
                goto failed;
            }

            p.Stop("inner_wall");
            p.Start("outer_wall");

            try {
                compute_outer_wall();
            } catch {
                set_failure_output(InnerMesh);
                goto failed;
            }

            p.Stop("outer_wall");

            p.Start("base");


            is_preview = (this.CurrentInputTimestamp != start_timestamp);
            try {
                DMesh3 baseMesh = new DMesh3(SocketMesh);
                bool base_failed = false;
                do_base(baseMesh, is_preview, out base_failed);
                if (base_failed) {
                    set_failure_output(SocketMesh);
                    goto failed;
                } else {
                    SocketMesh = baseMesh;
                }
            } catch (Exception e) {
                set_failure_output(SocketMesh);
                goto failed;
            }

            p.Stop("base");
            f3.DebugUtil.Log(p.AllTimes());

            Vector3f setColor = is_preview ? PartialSocketVertexColor : SocketVertexColor;
            foreach (int vid in SocketMesh.VertexIndices())
                SocketMesh.SetVertexColor(vid, setColor);

            ResultMesh = SocketMesh;
            last_result_status = (is_preview) ? ResultStatus.PreviewResult : ResultStatus.FullResult;

        failed:
            base.complete_update();
        }


        protected virtual void do_base(DMesh3 applyToMesh, bool bFastPreview, out bool bFailed)
        {
            bFailed = false;
        }



        void set_failure_output(DMesh3 lastOKMesh)
        {
            if (lastOKMesh == null) {
                Sphere3Generator_NormalizedCube gen = new Sphere3Generator_NormalizedCube() { Radius = 50 };
                ResultMesh = gen.Generate().MakeDMesh();
            } else {
                ResultMesh = new DMesh3(lastOKMesh, false, MeshComponents.None);
            }
            ResultMesh.EnableVertexColors(Colorf.VideoRed);

            last_result_status = ResultStatus.ErrorResult;
        }




        /// <summary>
        /// compute SDF for the scan object, and then compute offset iso-contours
        /// </summary>
        void compute_offset_meshes()
        {
            int sdf_cells = 128;
            int mesh_cells = 128;

            double max_offset = inner_offset + thickness;
            if (max_offset > cached_sdf_max_offset) {
                DMesh3 meshIn = new DMesh3(MeshSource.GetIMesh(), MeshHints.IsCompact, MeshComponents.None);
                MeshTransforms.FromFrame(meshIn, cachedInputsTransform);

                // [RMS] reduce this mesh? speeds up SDF quite a bit...
                Reducer r = new Reducer(meshIn);
                r.ReduceToTriangleCount(2500);

                double cell_size = meshIn.CachedBounds.MaxDim / sdf_cells;
                int exact_cells = (int)((max_offset) / cell_size) + 1;
                MeshSignedDistanceGrid sdf = new MeshSignedDistanceGrid(meshIn, cell_size) {
                    ExactBandWidth = exact_cells
                };
                sdf.Compute();
                cached_sdf = sdf;
                cached_sdf_max_offset = max_offset;
                cached_sdf_bounds = meshIn.CachedBounds;

                cached_inner_sdf_offset = 0;
                cached_outer_sdf_offset = 0;
            }

            if (cached_inner_sdf_offset != inner_offset || cached_outer_sdf_offset != max_offset) {
                var iso = new DenseGridTrilinearImplicit(cached_sdf.Grid, cached_sdf.GridOrigin, cached_sdf.CellSize);

                MarchingCubes c = new MarchingCubes() { Implicit = iso };
                c.Bounds = cached_sdf_bounds;
                c.CubeSize = c.Bounds.MaxDim / mesh_cells;
                c.Bounds.Expand(max_offset + 3 * c.CubeSize);

                if (cached_inner_sdf_offset != inner_offset) {
                    c.IsoValue = inner_offset;
                    c.Generate();
                    InnerOffsetMesh = c.Mesh;
                    Reducer reducer = new Reducer(InnerOffsetMesh);
                    reducer.ReduceToEdgeLength(c.CubeSize / 2);
                    InnerOffsetMeshSpatial = new DMeshAABBTree3(InnerOffsetMesh, true);
                    cached_inner_sdf_offset = inner_offset;
                }

                if (cached_outer_sdf_offset != max_offset) {
                    c.IsoValue = inner_offset + thickness;
                    c.Generate();
                    OuterOffsetMesh = c.Mesh;
                    Reducer reducer = new Reducer(OuterOffsetMesh);
                    reducer.ReduceToEdgeLength(c.CubeSize / 2);
                    OuterOffsetMeshSpatial = new DMeshAABBTree3(OuterOffsetMesh, true);
                    cached_outer_sdf_offset = max_offset;
                }
            }

            //Util.WriteDebugMesh(MeshSource.GetIMesh(), "c:\\scratch\\__OFFESTS_orig.obj");
            //Util.WriteDebugMesh(InnerOffsetMesh, "c:\\scratch\\__OFFESTS_inner.obj");
            //Util.WriteDebugMesh(OuterOffsetMesh, "c:\\scratch\\__OFFESTS_outer.obj");
        }





        /// <summary>
        /// compute offset meshes as simple extrusions
        /// </summary>
        void compute_offset_meshes_nosdf()
        {
            if (cached_inner_sdf_offset != inner_offset) {
                InnerOffsetMesh = new DMesh3(cachedInputMesh);
                MeshTransforms.FromFrame(InnerOffsetMesh, cachedInputsTransform);

                MeshNormals.QuickCompute(InnerOffsetMesh);
                MeshTransforms.VertexNormalOffset(InnerOffsetMesh, inner_offset);
                Reducer reducer = new Reducer(InnerOffsetMesh);
                reducer.ReduceToTriangleCount(5000);
                InnerOffsetMeshSpatial = new DMeshAABBTree3(InnerOffsetMesh, true);
                cached_inner_sdf_offset = inner_offset;
            }

            double max_offset = inner_offset + thickness;
            if (cached_outer_sdf_offset != max_offset) {
                OuterOffsetMesh = new DMesh3(cachedInputMesh);
                MeshTransforms.FromFrame(OuterOffsetMesh, cachedInputsTransform);

                MeshNormals.QuickCompute(OuterOffsetMesh);
                MeshTransforms.VertexNormalOffset(OuterOffsetMesh, max_offset);
                Reducer reducer = new Reducer(OuterOffsetMesh);
                reducer.ReduceToTriangleCount(5000);
                OuterOffsetMeshSpatial = new DMeshAABBTree3(OuterOffsetMesh, true);
                cached_outer_sdf_offset = max_offset;
            }

            //Util.WriteDebugMesh(MeshSource.GetIMesh(), "c:\\scratch\\__OFFESTS_orig.obj");
            //Util.WriteDebugMesh(InnerOffsetMesh, "c:\\scratch\\__OFFESTS_inner.obj");
            //Util.WriteDebugMesh(OuterOffsetMesh, "c:\\scratch\\__OFFESTS_outer.obj");
        }





        void compute_trimmed_mesh()
        {
            // curve is on base leg, map to deformed leg
            // [TODO] really should be doing this via deformation, rather than nearest-point
            DCurve3 curve = new DCurve3(CurveSource.GetICurve());
            for (int i = 0; i < curve.VertexCount; ++i)
                curve[i] = MeshQueries.NearestPointFrame(cachedInputMesh, cachedInputMeshSpatial, curve[i]).Origin;

            TrimmedMesh = new DMesh3(cachedInputMesh);
            TrimmedMesh.EnableTriangleGroups(0);
           
            AxisAlignedBox3d bounds = TrimmedMesh.CachedBounds;

            // try to find seed based on raycast, which doesn't always work.
            // Note that seed is the seed for the *eroded* region, not the kept region
            Vector3d basePt = bounds.Center + 10 * bounds.Extents.y * Vector3d.AxisY;
            int hit_tid = cachedInputMeshSpatial.FindNearestHitTriangle(new Ray3d(basePt, -Vector3d.AxisY));
            Vector3d seed = cachedInputMesh.GetTriCentroid(hit_tid);
            if (flip_trim_side) {
                basePt = bounds.Center - 10 * bounds.Extents.y * Vector3d.AxisY;
                hit_tid = cachedInputMeshSpatial.FindNearestHitTriangle(new Ray3d(basePt, Vector3d.AxisY));
                seed = cachedInputMesh.GetTriCentroid(hit_tid);
            }

            MeshTrimLoop trim = new MeshTrimLoop(TrimmedMesh, curve, seed, cachedInputMeshSpatial);
            trim.Trim();

            if (TrimmedMesh.HasVertexColors == false) {
                TrimmedMesh.EnableVertexColors(SocketVertexColor);
            } else {
                foreach (int vid in TrimmedMesh.VertexIndices())
                    TrimmedMesh.SetVertexColor(vid, SocketVertexColor);
            }

            MeshTransforms.FromFrame(TrimmedMesh, cachedInputsTransform);
        }



        void compute_inner_wall()
        {
            if (DebugStep <= 0) {
                SocketMesh = new DMesh3(TrimmedMesh);
                return;
            }

            InnerMesh = new DMesh3(TrimmedMesh);

            // compute flare band
            Func<int, double> flareOffsetF = (vid) => { return 0; };
            if (flare_offset > 0 && flare_band_width > 0) {
                MeshBoundaryLoops loops = new MeshBoundaryLoops(InnerMesh);
                if (loops.Count != 1)
                    goto done_inner_wall;

                DijkstraGraphDistance dist = DijkstraGraphDistance.MeshVertices(InnerMesh);
                dist.TrackOrder = true;
                foreach (int vid in loops[0].Vertices)
                    dist.AddSeed(vid, 0);
                dist.ComputeToMaxDistance(1.25f * (float)flare_band_width);

                flareOffsetF = (viD) => {
                    float d = dist.GetDistance(viD);
                    if ( d < flare_band_width) {
                        double t = d / flare_band_width;
                        t = 1 - t * t;
                        t = t * t * t;
                        return t * flare_offset;
                    }
                    return 0;
                };
            }


            gParallel.ForEach(InnerMesh.VertexIndices(), (vid) => {
                Vector3d v = InnerMesh.GetVertex(vid);
                Vector3d n = InnerMesh.GetVertexNormal(vid);
                double offset = inner_offset + flareOffsetF(vid);
                InnerMesh.SetVertex(vid, v + offset * n);
            });


        done_inner_wall:
            MeshNormals.QuickCompute(InnerMesh);
        }


        bool compute_outer_wall()
        {
            SocketMesh = new DMesh3(InnerMesh);
            LastInnerGroupID = SocketMesh.AllocateTriangleGroup();
            FaceGroupUtil.SetGroupID(SocketMesh, LastInnerGroupID);

            if (DebugStep <= 1)
                return true;

            double use_thickness = thickness;
            MeshExtrudeMesh extrude = new MeshExtrudeMesh(SocketMesh);
            extrude.ExtrudedPositionF = (v, n, vid) => {
                return v + use_thickness * (Vector3d)n;
            };
            if (extrude.Extrude() == false)
                return false;

            LastExtrudeOuterGroupID = extrude.OffsetGroupID;
            LastStitchGroupID = extrude.StitchGroupIDs[0];

            return true;
        }












        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return ResultMesh;
        }

        public override DMesh3 GetDMeshUnsafe() {
            return (DMesh3)GetIMesh();
        }

        public override bool HasSpatial {
            get { return false; }
        }
        public override ISpatial GetSpatial()
        {
            return null;
        }

        public override DMesh3 ExtractDMesh()
        {
            Update();
            var result = ResultMesh;
            ResultMesh = null;
            base.result_consumed();
            return result;
        }

    }
}
