using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using gs;
using f3;

namespace gsbody
{
    /// <summary>
    /// SingleMeshShapeModel mesh is a pointer to LegSO.Mesh  (should be a copy??)
    /// </summary>
    public class LegModel : SingleMeshShapeModel
    {

        LegSO leg;
        public LegSO SO
        {
            get { return leg; }
        }


        RectifiedLegSO rectified_leg;
        public RectifiedLegSO RectifiedSO {
            get { return rectified_leg; }
        }



        public enum LegDeformationTypes
        {
            Offset = 0,
            Smooth = 1
        }


        public delegate void DeformationAddedEventHandler(SceneObject so, IVectorDisplacementSourceOp op);
        public DeformationAddedEventHandler OnDeformationAdded;

        public delegate void DeformationRemovedEventHandler(SceneObject so, IVectorDisplacementSourceOp op);
        public DeformationRemovedEventHandler OnDeformationRemoved;



        ConstantMeshSourceOp SOMeshSource;
        DisplacementCombinerOp Combiner;
        MeshVertexDisplacementOp VertexDisplace;
        MeshScaleOp PostScale;
        ThreadedMeshComputeOp Compute;

        VectorDisplacementMapOp BrushLayer;

        UniquePairSet<SceneObject, ModelingOperator> SO_Op;


        public LegModel(LegSO legIn, SOMaterial rectifiedMaterial) : base(legIn.Mesh, false, legIn.Spatial)
        {
            leg = legIn;
            leg.Name = "InitialLeg";

            legIn.OnMeshModified += on_leg_scan_modified;

            rectified_leg = leg.DuplicateSubtype<RectifiedLegSO>();
            rectified_leg.EnableSpatial = false;
            leg.GetScene().AddSceneObject(rectified_leg, false);
            rectified_leg.AssignSOMaterial(rectifiedMaterial);
            rectified_leg.Name = "RectifiedLeg";

            //SOMeshSource = new ConstantMeshSourceOp(legIn.Mesh, true, false);
            SOMeshSource = new ConstantMeshSourceOp();

            SO_Op = new UniquePairSet<SceneObject, ModelingOperator>();
            Combiner = new DisplacementCombinerOp();
            VertexDisplace = new MeshVertexDisplacementOp() {
                MeshSource = SOMeshSource,
                DisplacementSource = Combiner
            };
            PostScale = new MeshScaleOp() {
                MeshSource = VertexDisplace
            };
            Compute = new ThreadedMeshComputeOp() {
                MeshSource = PostScale
            };

            // add global brush layer
            BrushLayer = new VectorDisplacementMapOp() {
                MeshSource = SOMeshSource
            };
            Combiner.Append(BrushLayer);


            SOMeshSource.SetMesh(legIn.Mesh, true, false);
        }

        private void on_leg_scan_modified(DMeshSO so)
        {
            // have to do copy in main thread
            SOMeshSource.SetMesh(so.Mesh, true, false);
        }


        public void Update()
        {
            try {
                DMeshOutputStatus result = Compute.CheckForNewMesh();
                if (result.State == DMeshOutputStatus.States.Ready) {
                    ReplaceOutputMesh(result.Mesh, false);
                    if (result.Mesh.VertexCount == rectified_leg.Mesh.VertexCount)
                        rectified_leg.UpdateVertices(result.Mesh, true, true);
                    else
                        rectified_leg.ReplaceMesh(result.Mesh);
                }
            } catch (Exception e) {
                DebugUtil.Log(2, "LegModel.Update: caught exception! " + e.Message);
            }

            if (Compute.HaveBackgroundException) {
                Exception e = Compute.ExtractBackgroundException();
                DebugUtil.Log(2, "LegModel.Update: exception in background compute: " + e.Message);
                DebugUtil.Log(2, e.StackTrace);
            }
        }



        public void Disconnect()
        {
            leg.OnMeshModified -= on_leg_scan_modified;
        }



        public bool HeatMapEnabled {
            get { return VertexDisplace.EnableHeatMap;  }
            set { VertexDisplace.EnableHeatMap = value; }
        }
        public double HeatMapMaxDistance {
            get { return VertexDisplace.HeatMapMaxDistance; }
            set { VertexDisplace.HeatMapMaxDistance = value; }
        }



        public IVectorDisplacementSourceOp AppendRegionOp(EnclosedPatchSO Source, LegDeformationTypes type)
        {
            if (type == LegDeformationTypes.Offset)
                return AppendRegionOffset(Source);
            else if (type == LegDeformationTypes.Smooth)
                return AppendRegionSmoothOp(Source);
            throw new NotImplementedException("LegModel.AppendRegionOp");
        }


        public IVectorDisplacementSourceOp AppendRegionOffset(EnclosedPatchSO Source)
        {
            Frame3f deformF = Frame3f.Identity;
            deformF = SceneTransforms.TransformTo(deformF, Source, leg);

            PolyCurveSOSourceOp curveOp = new PolyCurveSOSourceOp(Source);
            EnclosedRegionOffsetOp deformOp = new EnclosedRegionOffsetOp() {
                Normal = deformF.Y,
                PushPullDistance = 2.0f,
                MeshSource = SOMeshSource,
                CurveSource = curveOp
            };

            Combiner.Append(deformOp);
            SO_Op.Add(Source, deformOp);

            OnDeformationAdded?.Invoke(Source, deformOp);

            return deformOp;
        }


        
        public IVectorDisplacementSourceOp AppendRegionSmoothOp(EnclosedPatchSO Source)
        {
            PolyCurveSOSourceOp curveOp = new PolyCurveSOSourceOp(Source);
            EnclosedRegionSmoothOp deformOp = new EnclosedRegionSmoothOp() {
                SmoothAlpha = 0.5f,
                MeshSource = SOMeshSource,
                CurveSource = curveOp
            };

            Combiner.Append(deformOp);
            SO_Op.Add(Source, deformOp);

            OnDeformationAdded?.Invoke(Source, deformOp);

            return deformOp;
        }




        public IVectorDisplacementSourceOp AppendPlaneBandExpansion(PlaneIntersectionCurveSO Source)
        {
            Frame3f deformF = Frame3f.Identity;
            deformF = SceneTransforms.TransformTo(deformF, Source, leg);

            PlaneBandExpansionOp deformOp = new PlaneBandExpansionOp() {
                Origin = deformF.Origin,
                Normal = deformF.Y,
                BandDistance = 15.0f,
                PushPullDistance = -1.0f,
                MeshSource = SOMeshSource
            };

            Combiner.Append(deformOp);
            SO_Op.Add(Source, deformOp);

            OnDeformationAdded?.Invoke(Source, deformOp);

            return deformOp;
        }





        public IVectorDisplacementSourceOp AppendLegSqueezeOp(Vector3f upperPointL, PlaneIntersectionCurveSO UpperCurveSO)
        {
            LegSqueezeOp deformOp = new LegSqueezeOp() {
                UpperPoint = upperPointL,
                Axis = Vector3d.AxisY,
                MeshSource = SOMeshSource
            };

            Combiner.Append(deformOp);
            SO_Op.Add(UpperCurveSO, deformOp);

            OnDeformationAdded?.Invoke(UpperCurveSO, deformOp);

            return deformOp;
        }






        public IVectorDisplacementSourceOp AppendLengthenOp(PivotSO Source)
        {
            Frame3f deformF = Frame3f.Identity;
            deformF = SceneTransforms.TransformTo(deformF, Source, leg);

            LengthenOp deformOp = new LengthenOp() {
                BasePoint = deformF.Origin,
                Direction = -Vector3d.AxisY,
                BandDistance = 50.0f,
                LengthenDistance = 2.0f,
                MeshSource = SOMeshSource
            };

            Combiner.Append(deformOp);
            SO_Op.Add(Source, deformOp);

            OnDeformationAdded?.Invoke(Source, deformOp);

            return deformOp;
        }




        public void RemoveDeformationOp(IVectorDisplacementSourceOp op)
        {
            SceneObject so = SO_Op.Find(op);
            Combiner.Remove(op);
            SO_Op.Remove(op);

            OnDeformationRemoved?.Invoke(so, op);
        }



        /// <summary>
        /// iterator for [so,operator] associations
        /// </summary>
        public IEnumerable<Tuple<SceneObject, ModelingOperator>> OperatorObjectPairs() {
            return SO_Op;
        }


        /// <summary>
        /// find modeling op associated w/ SO
        /// </summary>
        public ModelingOperator FindOpForSO(SceneObject so) {
            ModelingOperator op = SO_Op.Find(so);
            return op;
        }

        /// <summary>
        /// find SO associated w/ modeling op
        /// </summary>
        public SceneObject FindSOForOp(ModelingOperator op) {
            SceneObject so = SO_Op.Find(op);
            return so;
        }



        public bool HasLengthenOp() {
            foreach ( ModelingOperator op in SO_Op.SecondTypeItr() ) {
                if (op is LengthenOp)
                    return true;
            }
            return false;
        }


        public LegSqueezeOp FindLegSqueezeOp()
        {
            foreach (ModelingOperator op in SO_Op.SecondTypeItr()) {
                if (op is LegSqueezeOp)
                    return op as LegSqueezeOp;
            }
            return null;
        }


        public MeshScaleOp GetPostScaleOp()
        {
            return PostScale;
        }

        public VectorDisplacementMapOp GetBrushLayer()
        {
            return BrushLayer;
        }



        public void SetOpWidgetVisibility(bool bVisible)
        {
            foreach ( SceneObject so in SO_Op.FirstTypeItr() ) 
                SceneUtil.SetVisible(so, bVisible);
        }



    }
}
