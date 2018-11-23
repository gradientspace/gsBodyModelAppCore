using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using f3;
using g3;
using gs;

namespace gsbody
{

    /// <summary>
    /// This is a PolyCurveSO that, assuming the curve is a closed loop, finds the enclosed region.
    /// </summary>
    public class EnclosedPatchSO : ThreadSafePolyCurveSO
    {
        fMeshGameObject previewGO;
        fMaterial previewMaterial;

        /// <summary>
        /// If true, copy mesh patch inside curve and show 
        /// </summary>
        public bool EnableRegionOverlay = false;


        override public SOType Type {
            get { return BodyModelSOTypes.EnclosedPatch; }
        }


        public override PolyCurveSO Create(SOMaterial defaultMaterial)
        {
            EnclosedPatchSO so = (EnclosedPatchSO)base.Create(defaultMaterial);

            previewMaterial = MaterialUtil.CreateTransparentMaterialF(Colorf.ForestGreen, 0.1f);

            return so;
        }


        public override void PreRender()
        {
            base.PreRender();
        }


        protected override void on_curve_validated()
        {
            if (previewGO != null) {
                RemoveGO((fGameObject)previewGO);
                previewGO.Destroy();
            }


            if (EnableRegionOverlay) {
                if (TargetModelSO == null)
                    throw new InvalidOperationException("EnclosedPatchSO.on_curve_validated: curve is not connected to a Target");
                if (TransformMode != OutputCurveTransform.ToTargetSO)
                    throw new InvalidOperationException("EnclosedPatchSO.on_curve_validated: curve is not transformed to TargetSO");

                DCurve3 target_curve = RequestCurveCopyFromMainThread();
                MeshFacesFromLoop loop = new MeshFacesFromLoop(TargetModel.SourceMesh,
                    target_curve, TargetModel.SourceSpatial);
                MeshFaceSelection face_selection = loop.ToSelection();

                DSubmesh3 submesh = new DSubmesh3(TargetModel.SourceMesh, face_selection, face_selection.Count);

                MeshNormals normals = new MeshNormals(submesh.SubMesh);
                normals.Compute();
                foreach (int vid in submesh.SubMesh.VertexIndices()) {
                    Vector3d n = normals.Normals[vid];
                    Vector3d v = submesh.SubMesh.GetVertex(vid);
                    v += 0.1 * n;
                    v = SceneTransforms.TransformTo(v, TargetModelSO, this);
                    submesh.SubMesh.SetVertex(vid, v);
                }

                previewGO = GameObjectFactory.CreateMeshGO("patch",
                    new fMesh(submesh.SubMesh), false, true);
                previewGO.SetMaterial(previewMaterial, true);
                previewGO.SetLayer(FPlatform.WidgetOverlayLayer);
                previewGO.SetIgnoreMaterialChanges();
                AppendNewGO(previewGO, root, false);
            }
        }



        public static EnclosedPatchSO CreateFromPreview(CurvePreview preview, SOMaterial material, FScene scene)
        {
            EnclosedPatchSO curveSO = (EnclosedPatchSO)preview.BuildSO( 
                (curve) => {
                    EnclosedPatchSO so = new EnclosedPatchSO() {
                        Curve = curve
                    };
                    so.Create(material);
                    return so;
                }, material, 1.0f);

        
            scene.History.PushChange(
                new AddSOChange() { scene = scene, so = curveSO, bKeepWorldPosition = false });
            scene.History.PushInteractionCheckpoint();

            return curveSO;
        }

    }
}
