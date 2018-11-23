using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;
using g3;
using gs;

namespace gsbody
{
    public class TrimLoopSO : ThreadSafePolyCurveSO
    {

        public override PolyCurveSO Create(SOMaterial defaultMaterial)
        {
            TrimLoopSO so = (TrimLoopSO)base.Create(defaultMaterial);
            return so;
        }

        public override void PreRender()
        {
            base.PreRender();
        }



        override public SOType Type {
            get { return BodyModelSOTypes.TrimLoop; }
        }


        public static TrimLoopSO CreateFromPreview(CurvePreview preview, SOMaterial material, FScene scene)
        {
            TrimLoopSO curveSO = (TrimLoopSO)preview.BuildSO( 
                (curve) => {
                    TrimLoopSO so = new TrimLoopSO() {
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




        public static TrimLoopSO CreateFromPlane(DMeshSO TargetSO, Frame3f PlaneS, SOMaterial material, FScene scene, double fNormalOffset = 0.0f)
        {
            Frame3f PlaneO = SceneTransforms.SceneToObject(TargetSO, PlaneS);

            PlaneIntersectionCurves curves = new PlaneIntersectionCurves(TargetSO.Mesh, PlaneO, 1) {
                NormalOffset = fNormalOffset
            };
            curves.Compute();

            if (curves.Loops.Length != 1)
                throw new Exception("TrimLoopSO.CreateFromPlane: got more than one cut loop?");
            DCurve3 loop = curves.Loops[0];

            // map loop back into plane frame
            for (int i = 0; i < loop.VertexCount; ++i)
                loop[i] = PlaneO.ToFrameP(loop[i]);

            TrimLoopSO curveSO = new TrimLoopSO() { Curve = loop };
            curveSO.Create(material);
            Frame3f curveFrame = SceneTransforms.ObjectToScene(TargetSO, PlaneO);
            curveSO.SetLocalFrame(curveFrame, CoordSpace.ObjectCoords);

            scene.History.PushChange(
                new AddSOChange() { scene = scene, so = curveSO, bKeepWorldPosition = false });
            scene.History.PushInteractionCheckpoint();

            return curveSO;
        }

    }
}
