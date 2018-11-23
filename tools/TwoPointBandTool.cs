using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using gs;
using f3;

namespace gsbody
{
    public class TwoPointBandToolBuilder : MultiPointToolBuilder
    {
        public float PlaneIndicatorWidthScene = 0.25f;

        // arguments are (TargetSO, CurrentPlaneInScene)
        public Action<SceneObject, Frame3f, Frame3f> OnApplyF = null;

        public override bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.SingleObject && targets[0] is DMeshSO);
        }

        public override ITool Build(FScene scene, List<SceneObject> targets)
        {
            TwoPointBandTool tool = new TwoPointBandTool(scene, targets[0] as DMeshSO);
            configure_tool(tool);
            tool.PlaneIndicatorWidthScene = PlaneIndicatorWidthScene;
            tool.OnApplyF = OnApplyF;
            return tool;
        }

    }





    public class TwoPointBandTool : MultiPointTool
    {
        override public string Name { get { return "TwoPointBandTool"; } }
        override public string TypeIdentifier { get { return "two_point_band"; } }


        float plane_indicator_width = 5.0f;
        public float PlaneIndicatorWidthScene {
            get { return plane_indicator_width; }
            set { plane_indicator_width = MathUtil.Clamp(value, 0.01f, 10000.0f); }
        }

        /// <summary>
        /// called with (TargetSO, CurrentPlaneInScene) when Apply() is called
        /// </summary>
        public Action<SceneObject, Frame3f, Frame3f> OnApplyF = null;


        /// <summary>
        /// called with (TargetSO, CurrentPlaneInScene) when CanApply() is called
        /// </summary>
        public Func<SceneObject, Frame3f, Frame3f, bool> CanApplyF = null;




        public TwoPointBandTool(FScene scene, SceneObject target) : base(scene, target)
        {
        }



        int StartPointID = -1;
        int EndPointID = -1;
        bool points_intialized = false;

        Line3d TargetAxis;


        public override void Setup()
        {
            StartPointID = AppendSurfacePoint("StartPoint", Colorf.Blue);
            EndPointID = AppendSurfacePoint("EndPoint", Colorf.Green);

            //LineIndicator line = new LineIndicator() {
            //    LineWidth = fDimension.Scene(1.0f),
            //    SceneStartF = () => { return GizmoPoints[StartPointID].currentFrameS.Origin; },
            //    SceneEndF = () => { return GizmoPoints[EndPointID].currentFrameS.Origin; },
            //};
            //indicators.AddIndicator(line);
            //indicators.SetLayer(line, FPlatform.HUDOverlay);   // has to be hud overlay or it will be clipped by depth render

            Frame3f TargetFrameS = TargetSO.GetLocalFrame(CoordSpace.SceneCoords);
            TargetAxis = new Line3d(TargetFrameS.Origin, Vector3d.AxisY);

            SectionPlaneIndicator startPlane = IndicatorBuilder.MakeSectionPlaneIndicator(
                100, "startPlane",
                fDimension.Scene(plane_indicator_width),
                () => { return new Frame3f(TargetAxis.ClosestPoint(GizmoPoints[StartPointID].currentFrameS.Origin)); },
                () => { return new Colorf(Colorf.LightGreen, 0.5f); },
                () => { return true; }
            );
            Indicators.AddIndicator(startPlane);

            SectionPlaneIndicator endPlane = IndicatorBuilder.MakeSectionPlaneIndicator(
                101, "endPlane",
                fDimension.Scene(plane_indicator_width),
                () => { return new Frame3f(TargetAxis.ClosestPoint(GizmoPoints[EndPointID].currentFrameS.Origin)); },
                () => { return new Colorf(Colorf.LightGreen, 0.5f); },
                () => { return true; }
            );

            Indicators.AddIndicator(endPlane);
        }

        protected override void OnPointInitialized(ControlPoint pt)
        {
            if (IsPointInitialized(StartPointID) && IsPointInitialized(EndPointID))
                points_intialized = true;
        }



        override public bool HasApply { get { return OnApplyF != null; } }
        override public bool CanApply {
            get {
                return points_intialized && ((CanApplyF == null) ? true : CanApplyF(TargetSO, GetPointPosition(StartPointID), GetPointPosition(EndPointID)));
            }
        }
        override public void Apply()
        {
            OnApplyF(TargetSO, GetPointPosition(StartPointID), GetPointPosition(EndPointID));
        }




        public void InitializeOnTarget(DMeshSO target, double initial_width)
        {
            AxisAlignedBox3d bounds = target.Mesh.CachedBounds;
            Vector3d c = bounds.Center;
            SORayHit nearestPt;
            target.FindNearest(c, double.MaxValue, out nearestPt, CoordSpace.ObjectCoords);
            c = nearestPt.hitPos;

            Vector3d up = c + initial_width * Vector3d.AxisY;
            target.FindNearest(up, double.MaxValue, out nearestPt, CoordSpace.ObjectCoords);
            up = nearestPt.hitPos;

            Vector3d down = c - initial_width * Vector3d.AxisY;
            target.FindNearest(down, double.MaxValue, out nearestPt, CoordSpace.ObjectCoords);
            down = nearestPt.hitPos;

            SetPointPosition_Internal(StartPointID, new Frame3f(up), CoordSpace.ObjectCoords);
            SetPointPosition_Internal(EndPointID, new Frame3f(down), CoordSpace.ObjectCoords);
        }


    }
}
