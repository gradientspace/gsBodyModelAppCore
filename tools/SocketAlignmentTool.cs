using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;
using gs;

namespace gsbody
{

    public class SocketAlignmentToolBuilder : MultiPointToolBuilder
    {
        public bool FixedPreviewOrientation = false;

        protected override MultiPointTool new_tool(FScene scene, SceneObject target)
        {
            var tool = new SocketAlignmentTool(scene, target);
            tool.FixedPreviewOrientation = this.FixedPreviewOrientation;
            return tool;
        }
    }


    public class SocketAlignmentTool : MultiPointTool
    {

        // if this is true, preview will stay "to the right" of target, in scene
        // coords. It's a bit weird...
        public bool FixedPreviewOrientation = false;



        int BasePointID = -1;
        int FrontPointID = -1;
        int TopPointID = -1;

        //PlaneIntersectionTarget TopPointTarget;

        AxisAlignedBox3d meshBounds;
        Vector3f initialFrontPt;
        Frame3f lastTargetFrameS;
        Frame3f lastPreviewFrameS;

        Vector3f current_forward_pt_S;
        Vector3f start_forward_pt_S;

        DMeshSO previewSO;


        public SocketAlignmentTool(FScene scene, SceneObject target) : base(scene, target)
        {
        }

        public override void Setup()
        {
            BasePointID = AppendSurfacePoint("BasePoint", Colorf.Blue);
            FrontPointID = AppendSurfacePoint("FrontPoint", Colorf.Green);
            TopPointID = AppendSurfacePoint("TopPoint", Colorf.Magenta);

            //TopPointTarget = new PlaneIntersectionTarget() { NormalAxis = 1 };
            //TopPointID = AppendTargetPoint("MidPoint", new Colorf(Colorf.Magenta,0.5f), TopPointTarget);
            //SetPointColor(TopPointID, new Colorf(Colorf.Magenta, 0.5f), FPlatform.WidgetOverlayLayer);

            Indicators.AddIndicator(new LineIndicator() {
                LineWidth = fDimension.Scene(1.0f),
                SceneStartF = () => { return GizmoPoints[BasePointID].currentFrameS.Origin; },
                SceneEndF = () => { return GizmoPoints[TopPointID].currentFrameS.Origin; },
            });

            Indicators.AddIndicator(new LineIndicator() {
                LineWidth = fDimension.Scene(1.0f),
                ColorF = () => { return Colorf.LightSteelBlue; },
                SceneStartF = () => { return start_forward_pt_S; },
                SceneEndF = () => { return current_forward_pt_S; },
            });

        }


        override public void Shutdown()
        {
            Scene.RemoveSceneObject(previewSO, true);
            base.Shutdown();
        }


        protected override void OnPointUpdated(ControlPoint pt, Frame3f prevFrameS, bool isFirst)
        {
            Vector3f basePt = GetPointPosition(BasePointID, CoordSpace.SceneCoords).Origin;
            Vector3f frontPt = GetPointPosition(FrontPointID, CoordSpace.SceneCoords).Origin;
            Vector3f topPt = GetPointPosition(TopPointID, CoordSpace.SceneCoords).Origin;

            lastTargetFrameS = TargetSO.GetLocalFrame(CoordSpace.SceneCoords);

            Frame3f previewFrameS = lastTargetFrameS;

            // position next to original object
            previewFrameS = previewFrameS.Translated(1.1f * (float)meshBounds.Width * Vector3f.AxisX);

            Vector3f upAxis = (topPt - basePt).Normalized;

            // construct a frame perp to upAxis at midpoint, and project original and current fw points
            Frame3f upFrame = new Frame3f((topPt + basePt) * 0.5f, upAxis);
            Vector3f origFW = upFrame.ProjectToPlane(initialFrontPt, 2);
            origFW = (origFW - upFrame.Origin).Normalized;
            Vector3f curFW = upFrame.ProjectToPlane(frontPt, 2);
            curFW = (curFW - upFrame.Origin).Normalized;
            //float angle = MathUtil.PlaneAngleSignedD(origFW, curFW, upAxis);

            start_forward_pt_S = upFrame.FromFrameP(origFW);
            current_forward_pt_S = upFrame.FromFrameP(curFW);

            // construct rotation that aligns up axis with y-up
            Quaternionf upRotate = Quaternionf.FromTo(upAxis, Vector3f.AxisY);
            previewFrameS.Rotate(upRotate);

            // now rotate so that forward dir points along -Z
            //Quaternionf fwRotate = Quaternionf.AxisAngleD(Vector3f.AxisY, angle);
            //curFW = upRotate * curFW;
            Quaternionf fwRotate = Quaternionf.FromToConstrained(curFW, -Vector3f.AxisZ, Vector3f.AxisY);
            previewFrameS.Rotate(fwRotate);

            previewSO.SetLocalFrame(previewFrameS, CoordSpace.SceneCoords);
            lastPreviewFrameS = previewFrameS;
        }



        public void Initialize_AutoFitBox()
        {
            DMeshSO TargetMeshSO = TargetSO as DMeshSO;

            // initialize w/ auto-fit box
            DMesh3 mesh = TargetMeshSO.Mesh;
            DMeshAABBTree3 spatial = TargetMeshSO.Spatial;
            meshBounds = mesh.CachedBounds;

            create_preview_so();

            ContOrientedBox3 boxFitter = new ContOrientedBox3(
                new RemapItr<Vector3d, int>(mesh.TriangleIndices(), (tid) => { return mesh.GetTriCentroid(tid); }),
                new RemapItr<double, int>(mesh.TriangleIndices(), (tid) => { return mesh.GetTriArea(tid); }));
            //DebugUtil.EmitDebugBox("fitbox", boxFitter.Box, Colorf.Red, TargetSO.RootGameObject, false);
            Box3d fitBox = boxFitter.Box;
            int longest = 0;
            if (fitBox.Extent.y > fitBox.Extent.x)
                longest = 1;
            if (fitBox.Extent.z > fitBox.Extent[longest])
                longest = 2;
            Vector3d vTop = fitBox.Center + fitBox.Extent[longest] * fitBox.Axis(longest);
            Vector3d vBottom = fitBox.Center - fitBox.Extent[longest] * fitBox.Axis(longest);

            int base_tid = spatial.FindNearestTriangle(vBottom);
            int top_tid = spatial.FindNearestTriangle(vTop);
            if ( vTop.y < vBottom.y ) {
                int tmp = base_tid; base_tid = top_tid; top_tid = tmp;
            }
            Vector3d vBasePt = mesh.GetTriCentroid(base_tid);
            Vector3d vTopPt = mesh.GetTriCentroid(top_tid);


            int other1 = (longest + 1) % 3, other2 = (longest + 2) % 3;
            int front_tid = spatial.FindNearestHitTriangle(new Ray3d(fitBox.Center, fitBox.Axis(other1)));
            Vector3d vFrontPt = mesh.GetTriCentroid(front_tid);

            int back_tid = spatial.FindNearestHitTriangle(new Ray3d(fitBox.Center, -fitBox.Axis(other1)));
            Vector3d vBackPt = mesh.GetTriCentroid(back_tid);

            int right_tid = spatial.FindNearestHitTriangle(new Ray3d(fitBox.Center, fitBox.Axis(other2)));
            Vector3d vRightPt = mesh.GetTriCentroid(right_tid);

            int left_tid = spatial.FindNearestHitTriangle(new Ray3d(fitBox.Center, -fitBox.Axis(other2)));
            Vector3d vLeftPt = mesh.GetTriCentroid(left_tid);

            initialFrontPt = (Vector3f)vFrontPt;

            SetPointPosition_Internal(BasePointID, MeshQueries.SurfaceFrame(mesh, base_tid, vBasePt), CoordSpace.ObjectCoords);
            SetPointPosition_Internal(FrontPointID, MeshQueries.SurfaceFrame(mesh, front_tid, vFrontPt), CoordSpace.ObjectCoords);
            SetPointPosition(TopPointID, MeshQueries.SurfaceFrame(mesh, top_tid, vTopPt), CoordSpace.ObjectCoords);

        }






        /// <summary>
        /// initialize points w/ known base point and up direction
        /// </summary>
        public void Initialize_KnownBasePoint(Vector3d basePointL, Vector3f upAxis)
        {
            DMeshSO TargetMeshSO = TargetSO as DMeshSO;

            // initialize w/ auto-fit box
            DMesh3 mesh = TargetMeshSO.Mesh;
            DMeshAABBTree3 spatial = TargetMeshSO.Spatial;
            meshBounds = mesh.CachedBounds;

            create_preview_so();

            /*Frame3f frameO = TargetSO.GetLocalFrame(CoordSpace.ObjectCoords);*/

            // reproject base point onto surface in case somehow it is wrong
            Vector3f basePointUpdatedL = MeshQueries.NearestPointFrame(mesh, spatial, basePointL).Origin;
            Vector3f BasePointS = SceneTransforms.ObjectToSceneP(TargetSO, basePointUpdatedL);

            Vector3f upAxisL = Vector3f.AxisY;
            Vector3f topPointL = basePointUpdatedL + upAxisL * (float)meshBounds.Height;
            topPointL = MeshQueries.NearestPointFrame(mesh, spatial, topPointL).Origin;

            Vector3f TopPointS = SceneTransforms.ObjectToSceneP(TargetSO, topPointL);

            // shoot ray forward in scene, to find front point
            Vector3f forwardL = SceneTransforms.SceneToObjectN(TargetSO, -Vector3f.AxisZ);
            Frame3f fwHitFrameL;
            bool bHit = MeshQueries.RayHitPointFrame(mesh, spatial, new Ray3d(meshBounds.Center, forwardL), out fwHitFrameL);
            if (!bHit)
                throw new Exception("SocketAlignmentTool.Initialize_KnownBasePoint: ray missed!");

            Vector3f FrontPointS = SceneTransforms.ObjectToSceneP(TargetSO, fwHitFrameL.Origin);

            SetPointPosition(BasePointID, new Frame3f(BasePointS), CoordSpace.SceneCoords);
            SetPointPosition(FrontPointID, new Frame3f(FrontPointS), CoordSpace.SceneCoords);
            SetPointPosition(TopPointID, new Frame3f(TopPointS), CoordSpace.SceneCoords);
        }






        void create_preview_so()
        {
            previewSO = TargetSO.Duplicate() as DMeshSO;
            //previewSO.Create(new DMesh3(mesh), TargetSO.GetAssignedSOMaterial());
            previewSO.EnableSpatial = false;
            TargetSO.GetScene().AddSceneObject(previewSO);
        }


        public override void PreRender()
        {
            base.PreRender();

            // for fixed preview orientation, we keep preview "on the right" of scan. 
            if (FixedPreviewOrientation && previewSO != null ) {
                fCamera cam = previewSO.GetScene().ActiveCamera;
                Vector3f camRightW = cam.Right();
                Vector3f camRightS = previewSO.GetScene().ToSceneN(camRightW);

                Frame3f previewF = lastPreviewFrameS;
                Frame3f targetF = TargetSO.GetLocalFrame(CoordSpace.SceneCoords);
                Vector3f dv = previewF.Origin - targetF.Origin;
                dv.y = 0;
                float dist = dv.Length;

                Frame3f rightF = targetF.Translated(dist * camRightS);
                rightF.Rotation = previewF.Rotation;
                previewSO.SetLocalFrame(rightF, CoordSpace.SceneCoords);
            }
        }



        override public bool HasApply { get { return true; } }
        override public bool CanApply { get { return true; } }
        override public void Apply() {

            float VerticalSpaceFudge = 10.0f;

            DMeshSO TargetMeshSO = TargetSO as DMeshSO;

            Frame3f curFrameS = TargetSO.GetLocalFrame(CoordSpace.SceneCoords);
            TransformSOChange change = new TransformSOChange(TargetSO,
                curFrameS, lastPreviewFrameS, CoordSpace.SceneCoords);
            Scene.History.PushChange(change, false);

            Frame3f newFrameS = new Frame3f(SceneTransforms.ObjectToSceneP(TargetSO, meshBounds.Center));
            RepositionPivotChangeOp pivot1 = new RepositionPivotChangeOp(newFrameS, TargetMeshSO);
            Scene.History.PushChange(pivot1, false);

            newFrameS = TargetSO.GetLocalFrame(CoordSpace.SceneCoords);
            AxisAlignedBox3d bounds = TargetMeshSO.Mesh.CachedBounds;
            float h = (float)bounds.Height;
            Vector3f o = newFrameS.Origin;
            Vector3f translate = new Vector3f(-o.x, h * 0.5f-o.y + VerticalSpaceFudge, -o.z);
            Frame3f centeredFrameS = newFrameS.Translated(translate);

            TransformSOChange centerChange = new TransformSOChange(TargetSO,
                newFrameS, centeredFrameS, CoordSpace.SceneCoords);
            Scene.History.PushChange(centerChange, false);

            newFrameS = TargetSO.GetLocalFrame(CoordSpace.SceneCoords);
            o = newFrameS.Origin;
            o.y = 0;
            newFrameS.Origin = o;

            RepositionPivotChangeOp pivot2 = new RepositionPivotChangeOp(newFrameS, TargetMeshSO);
            Scene.History.PushChange(pivot2, false);


            Scene.History.PushInteractionCheckpoint();

        }





    }
}
