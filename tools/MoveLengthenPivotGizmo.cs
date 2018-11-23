using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;
using gs;

namespace gsbody
{
    public class MoveLengthenPivotGizmoBuilder : PositionConstrainedGizmoBuilder
    {
        public override ITransformGizmo Build(FScene scene, List<SceneObject> targets)
        {
            if (targets.Count != 1)
                return null;
            LengthenPivotSO pivotSO = targets[0] as LengthenPivotSO;
            if (pivotSO == null)
                return null;

            // [TODO] lost this functionality when we removed OriginalFrameS. 
            // 
            //Frame3f frameS = pivotSO.OriginalFrameS;
            Frame3f frameS = pivotSO.GetLocalFrame(CoordSpace.SceneCoords);

            MoveLengthenPivotGizmo gizmo = new MoveLengthenPivotGizmo();
            gizmo.ScenePositionF = (ray) => {
                DistLine3Ray3 dist = new DistLine3Ray3(ray, new Line3d(frameS.Origin, -frameS.Y));
                dist.Compute();
                // [RMS] disabling clamp here because we don't know original "start" point now
                //if (dist.LineParameter < 0)
                //    return dist.Line.Origin;
                //else
                    return dist.LineClosest;
            };

            gizmo.WidgetScale = WidgetScale;
            gizmo.Create(scene, targets);
            return gizmo;
        }


    }


    public class MoveLengthenPivotGizmo : PositionConstrainedGizmo
    {


        override protected void OnBeginCapture(Ray3f worldRay, Standard3DWidget w)
        {
            //if (Targets[0] is ToothNumberSO) {
            //    ToothNumberSO target = Targets[0] as ToothNumberSO;
            //    if (ConstraintSurfaces.Count == 0) {
            //        ConstraintSurfaces.AddRange((target.IsUpperNumber ? ArchFormGlobal.UpperTeeth : ArchFormGlobal.LowerTeeth).Cast<SceneObject>());
            //    }
            //}
            base.OnBeginCapture(worldRay, w);
        }


        protected override void OnEndCapture(Ray3f worldRay, Standard3DWidget w)
        {
            base.OnEndCapture(worldRay, w);

            //if (base.CurrentContsraintSurface != null && base.CurrentContsraintSurface is ToothSO) {
            //    (Targets[0] as ToothNumberSO).LinkToTooth(base.CurrentContsraintSurface as ToothSO);
            //}
        }

    }
}
