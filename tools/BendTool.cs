using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using f3;
using gs;

namespace gsbody
{


    public class BendToolBuilder : IToolBuilder
    {
        public virtual bool IsSupported(ToolTargetType type, List<SceneObject> targets) {
            return (type == ToolTargetType.SingleObject) && (targets[0] is DMeshSO);
        }

        public virtual ITool Build(FScene scene, List<SceneObject> targets)
        {
            BendTool tool = new BendTool(scene, targets[0] as DMeshSO);
            // configure here
            return tool;
        }
    }







    public class BendTool : ITool
    {
        virtual public string Name { get { return "BendTool"; } }
        virtual public string TypeIdentifier { get { return "bend_tool"; } }


        protected FScene Scene;
        public DMeshSO TargetSO;

        InputBehaviorSet behaviors;
        virtual public InputBehaviorSet InputBehaviors {
            get { return behaviors; }
            set { behaviors = value; }
        }

        ParameterSet parameters = new ParameterSet();
        public ParameterSet Parameters { get { return parameters; } }

        public ToolIndicatorSet Indicators { get; set; }


        double bend_angle = 25;
        public double BendAngle {
            get { return bend_angle; }
            set { if ( Math.Abs(bend_angle-value) > 0.0001) { bend_angle = value; warp_dirty = true; } }
        }



        Vector3d BendPlaneOriginS;
        Vector3d BendPlaneNormalS;
        BendPlanePivotSO bendPlaneGizmoSO;


        DMeshSO PreviewSO;
        Vector3d[] VertexPositions;


        public BendTool(FScene scene, DMeshSO target)
        {
            this.Scene = scene;
            TargetSO = target;

            // do this here ??
            behaviors = new InputBehaviorSet();
            //behaviors.Add(
            //    new MultiPointTool_2DBehavior(scene.Context, this) { Priority = 5 });
            //if (FPlatform.IsUsingVR()) {
            //    behaviors.Add(
            //        new MultiPointTool_SpatialBehavior(scene.Context, this) { Priority = 5 });
            //}

            Indicators = new ToolIndicatorSet(this, scene);
        }



        bool allow_selection_changes = false;
        public virtual bool AllowSelectionChanges { get { return allow_selection_changes; } }


        virtual public bool HasApply { get { return true; } }
        virtual public bool CanApply { get { return true; } }
        virtual public void Apply() {

            TargetSO.EditAndUpdateMesh((mesh) => {
                DMesh3 fromMesh = PreviewSO.Mesh;
                foreach (int vid in mesh.VertexIndices()) {
                    mesh.SetVertex(vid, fromMesh.GetVertex(vid));
                    mesh.SetVertexNormal(vid, fromMesh.GetVertexNormal(vid));
                }
            }, GeometryEditTypes.VertexDeformation);

        }


        public virtual void PreRender()
        {
            Indicators.PreRender();

            update_warp();
        }

        public virtual void Setup()
        {
            // turn on xform gizmo
            Scene.Context.TransformManager.PushOverrideGizmoType(BendPlanePivotGizmo.DefaultTypeName);


            Vector3d ctrPt = TargetSO.Mesh.CachedBounds.Center;
            Frame3f nearestF = MeshQueries.NearestPointFrame(TargetSO.Mesh, TargetSO.Spatial, ctrPt, true);

            BendPlaneOriginS = SceneTransforms.ObjectToSceneP(TargetSO, nearestF.Origin);
            BendPlaneNormalS = Vector3d.AxisY;

            bendPlaneGizmoSO = new BendPlanePivotSO();
            bendPlaneGizmoSO.Create(Scene.PivotSOMaterial, Scene.FrameSOMaterial);
            bendPlaneGizmoSO.OnTransformModified += OnBendPlaneTransformModified;
            Scene.AddSceneObject(bendPlaneGizmoSO);

            Frame3f cutFrameS = new Frame3f(BendPlaneOriginS); cutFrameS.AlignAxis(1, (Vector3f)BendPlaneNormalS);
            bendPlaneGizmoSO.SetLocalFrame(cutFrameS, CoordSpace.SceneCoords);

            allow_selection_changes = true;
            Scene.Select(bendPlaneGizmoSO, true);
            allow_selection_changes = false;


            StandardIndicatorFactory factory = new StandardIndicatorFactory();
            SectionPlaneIndicator bendPlane = factory.MakeSectionPlaneIndicator(
                100, "bendPlane",
                fDimension.Scene(100),
                () => { return new Frame3f(BendPlaneOriginS, BendPlaneNormalS); },
                () => { return new Colorf(Colorf.LightGreen, 0.5f); },
                () => { return true; }
            );
            Indicators.AddIndicator(bendPlane);

            // save initial vtx positions
            VertexPositions = new Vector3d[TargetSO.Mesh.MaxVertexID];
            foreach (int vid in TargetSO.Mesh.VertexIndices())
                VertexPositions[vid] = TargetSO.Mesh.GetVertex(vid);

            PreviewSO = TargetSO.Duplicate() as DMeshSO;
            Scene.AddSceneObject(PreviewSO);
            //PreviewSO.AssignSOMaterial(Scene.TransparentNewSOMaterial);

            fMaterial transMat = MaterialUtil.CreateTransparentMaterial(Colorf.BlueMetal.SetAlpha(0.1f));
            TargetSO.PushOverrideMaterial(transMat);
            TargetSO.SetLayer(FPlatform.WidgetOverlayLayer);
        }

        public virtual void Shutdown()
        {
            if (PreviewSO != null) {
                Scene.RemoveSceneObject(PreviewSO, true);
                PreviewSO = null;
            }
            if (bendPlaneGizmoSO != null) {
                allow_selection_changes = true;
                Scene.RemoveSceneObject(bendPlaneGizmoSO, true);
                allow_selection_changes = false;
                bendPlaneGizmoSO = null;
            }

            Indicators.Disconnect(true);


            TargetSO.PopOverrideMaterial();
            TargetSO.SetLayer(FPlatform.GeometryLayer);

            Scene.Context.TransformManager.PopOverrideGizmoType();
        }





        private void OnBendPlaneTransformModified(SceneObject so)
        {
            Frame3f cutFrameS = bendPlaneGizmoSO.GetLocalFrame(CoordSpace.SceneCoords);
            BendPlaneOriginS = cutFrameS.Origin;
            BendPlaneNormalS = cutFrameS.Z;

            warp_dirty = true;
        }




        bool warp_dirty = true;

        void update_warp()
        {
            if (warp_dirty == false)
                return;

            DMesh3 mesh = PreviewSO.Mesh;

            foreach (int vid in mesh.VertexIndices())
                mesh.SetVertex(vid, VertexPositions[vid]);

            Frame3f f = bendPlaneGizmoSO.GetLocalFrame(CoordSpace.SceneCoords);
            f = SceneTransforms.SceneToObject(PreviewSO, f);
            BendWarp warp = new BendWarp() {
                Mesh = mesh,
                BendPlane = f,
                BendAngle = bend_angle
            };
            warp.Apply();

            MeshNormals.QuickCompute(mesh);

            PreviewSO.NotifyMeshEdited(true);

            warp_dirty = false;
        }






    }







    public class BendPlanePivotSO : PivotSO
    {
        public override bool IsTemporary {
            get { return true; }
        }
    }




    public class BendPlanePivotGizmoBuilder : ITransformGizmoBuilder
    {
        public bool SupportsMultipleObjects { get { return false; } }

        public ITransformGizmo Build(FScene scene, List<SceneObject> targets)
        {
            if (targets.Count != 1 || (targets[0] is BendPlanePivotSO) == false)
                return null;

            var g = new BendPlanePivotGizmo();
            g.ActiveWidgets =
                AxisGizmoFlags.AxisTranslateY | AxisGizmoFlags.AxisTranslateZ | 
                AxisGizmoFlags.PlaneTranslateY |
                AxisGizmoFlags.AxisRotateX | AxisGizmoFlags.AxisRotateY | AxisGizmoFlags.AxisRotateZ;
            g.Create(scene, targets);
            return g;
        }
    }


    public class BendPlanePivotGizmo : AxisTransformGizmo
    {
        public const string DefaultTypeName = "bend_plane_pivot";
    }










    class BendWarp
    {
        public DMesh3 Mesh;
        public Frame3f BendPlane;
        public double BendAngle;



        public void Apply()
        {
            Vector3d Origin = BendPlane.Origin;
            Vector3d BendAxis1 = BendPlane.Z;

            float sideSign = (BendAngle > 0) ? 1 : -1;
            MeshVertexSelection vertices = new MeshVertexSelection(Mesh);
            foreach ( int vid in Mesh.VertexIndices() ) {
                Vector3d v = Mesh.GetVertex(vid);
                float sign = BendPlane.DistanceToPlaneSigned((Vector3f)v, 1);
                if (sign * sideSign > 0)
                    vertices.Select(vid);
            }

            Quaterniond rotation = Quaterniond.AxisAngleD(BendPlane.X, -BendAngle);

            foreach ( int vid in vertices ) {
                Vector3d v = Mesh.GetVertex(vid);
                Vector3d vNew = rotation * (v - Origin) + Origin;
                Mesh.SetVertex(vid, vNew);
            }

        }


    }







    //class MultiPointTool_2DBehavior : Any2DInputBehavior
    //{
    //    FContext context;
    //    MultiPointTool tool;

    //    public MultiPointTool_2DBehavior(FContext s, MultiPointTool tool)
    //    {
    //        context = s;
    //        this.tool = tool;
    //    }

    //    override public CaptureRequest WantsCapture(InputState input)
    //    {
    //        if (context.ToolManager.ActiveRightTool != tool)
    //            return CaptureRequest.Ignore;
    //        if (Pressed(input)) {
    //            int hit_pt_id = tool.FindNearestHitPoint(WorldRay(input));
    //            if (hit_pt_id >= 0) {
    //                return CaptureRequest.Begin(this);
    //            }
    //        }
    //        return CaptureRequest.Ignore;
    //    }

    //    override public Capture BeginCapture(InputState input, CaptureSide eSide)
    //    {
    //        int hit_pt_id = tool.FindNearestHitPoint(WorldRay(input));
    //        if (hit_pt_id >= 0) {
    //            tool.Begin(hit_pt_id, WorldRay(input));
    //            return Capture.Begin(this);
    //        }
    //        return Capture.Ignore;
    //    }


    //    override public Capture UpdateCapture(InputState input, CaptureData data)
    //    {
    //        if (Released(input)) {
    //            tool.End();
    //            return Capture.End;
    //        } else {
    //            tool.Update(WorldRay(input));
    //            return Capture.Continue;
    //        }
    //    }


    //    override public Capture ForceEndCapture(InputState input, CaptureData data)
    //    {
    //        tool.End();
    //        return Capture.End;
    //    }
    //}

}
