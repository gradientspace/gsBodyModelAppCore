using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;
using gs;

namespace gsbody
{
    public class PlaneIntersectionCurveSO : PolyCurveSO
    {
        override public SOType Type { get { return BodyModelSOTypes.PlaneIntersectionCurve; } }


        public override PolyCurveSO Create(SOMaterial defaultMaterial)
        {
            PlaneIntersectionCurveSO so = (PlaneIntersectionCurveSO)base.Create(defaultMaterial);
            return so;
        }




        public static PlaneIntersectionCurveSO CreateFromPlane(DMeshSO TargetSO, Frame3f PlaneS, SOMaterial material, FScene scene, double fNormalOffset = 0.0f )
        {
            Frame3f PlaneO = SceneTransforms.SceneToObject(TargetSO, PlaneS);

            PlaneIntersectionCurves curves = new PlaneIntersectionCurves(TargetSO.Mesh, PlaneO, 1) {
                NormalOffset = fNormalOffset
            };
            curves.Compute();

            if (curves.Loops.Length != 1)
                throw new Exception("PlaneIntersectionSO.CreateFromPlane: got more than one cut loop?");
            DCurve3 loop = curves.Loops[0];

            // map loop back into plane frame
            for (int i = 0; i < loop.VertexCount; ++i)
                loop[i] = PlaneO.ToFrameP(loop[i]);


            PlaneIntersectionCurveSO curveSO = new PlaneIntersectionCurveSO() {
                Curve = loop
            };
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
