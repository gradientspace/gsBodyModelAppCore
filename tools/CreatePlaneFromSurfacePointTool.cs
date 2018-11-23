using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using gs;
using f3;

namespace gsbody
{
    // [TODO] should take optional selection target...
    public class CreatePlaneFromSurfacePointToolBuilder : IToolBuilder
    {
        public float SphereIndicatorSizeScene = 0.25f;
        public float PlaneIndicatorWidthScene = 0.25f;

        public IndicatorFactory IndicatorBuilder = null;

        // arguments are (TargetSO, CurrentPlaneInScene)
        public Action<SceneObject, Frame3f> OnApplyF = null;

        public bool IsSupported(ToolTargetType type, List<SceneObject> targets)
        {
            return (type == ToolTargetType.SingleObject && targets[0].IsSurface);
        }

        public virtual ITool Build(FScene scene, List<SceneObject> targets)
        {
            CreatePlaneFromSurfacePointTool tool = new_tool(scene, targets[0]);
            tool.SphereIndicatorSizeScene = SphereIndicatorSizeScene;
            tool.PlaneIndicatorWidthScene = PlaneIndicatorWidthScene;
            if (IndicatorBuilder != null)
                tool.IndicatorBuilder = IndicatorBuilder;
            tool.OnApplyF = OnApplyF;
            return tool;
        }

        protected virtual CreatePlaneFromSurfacePointTool new_tool(FScene scene, SceneObject target)
        {
            return new CreatePlaneFromSurfacePointTool(scene, target);
        }
    }





    public class CreatePlaneFromSurfacePointTool : BaseSurfacePointTool
    {
        static readonly public string Identifier = "surface_point_to_plane";
        override public string Name { get { return "SurfacePointToPlaneTool"; } }
        override public string TypeIdentifier { get { return Identifier; } }


        SceneObject TargetSO;
        Frame3f ObjectFrameS;

        ToolIndicatorSet Indicators { get; set; }
        public IndicatorFactory IndicatorBuilder { get; set; }

        SphereIndicator sphereIndicator;
        SectionPlaneIndicator planeIndicator;

        float sphere_indicator_size = 0.2f;
        public float SphereIndicatorSizeScene {
            get { return sphere_indicator_size; }
            set { sphere_indicator_size = MathUtil.Clamp(value, 0.01f, 10000.0f); }
        }


        float plane_indicator_width = 5.0f;
        public float PlaneIndicatorWidthScene {
            get { return plane_indicator_width; }
            set { plane_indicator_width = MathUtil.Clamp(value, 0.01f, 10000.0f); }
        }

        /// <summary>
        /// called with (TargetSO, CurrentPlaneInScene) when Apply() is called
        /// </summary>
        public Action<SceneObject, Frame3f> OnApplyF = null;


        /// <summary>
        /// called with (TargetSO, CurrentPlaneInScene) when CanApply() is called
        /// </summary>
        public Func<SceneObject, Frame3f, bool> CanApplyF = null;

        Frame3f CurrentHitPosS;
        Frame3f CurrentPlaneFrameS;
        bool have_set_plane;

        public CreatePlaneFromSurfacePointTool(FScene scene, SceneObject target) : base(scene)
        {
            TargetSO = target;
            ObjectFrameS = TargetSO.GetLocalFrame(CoordSpace.SceneCoords);

            Indicators = new ToolIndicatorSet(this, scene);
            IndicatorBuilder = new StandardIndicatorFactory();

            have_set_plane = false;
        }


        // override this to limit SOs that can be clicked
        override public bool ObjectFilter(SceneObject so)
        {
            return so == TargetSO;
        }


        override public void PreRender()
        {
            Indicators.PreRender();
            base.PreRender();
        }


        override public void Shutdown()
        {
            Indicators.Disconnect(true);
            base.Shutdown();
        }


        /// <summary>
        /// called on click-down
        /// </summary>
        override public void Begin(SceneObject so, Vector2d downPos, Ray3f downRay)
        {
            SORayHit hit;
            if (TargetSO.FindRayIntersection(downRay, out hit) == false)
                return;

            Vector3d scenePos = SceneTransforms.WorldToSceneP(this.Scene, hit.hitPos);
            CurrentHitPosS = new Frame3f(scenePos);

            float fObjectT = (CurrentHitPosS.Origin - ObjectFrameS.Origin).Dot(ObjectFrameS.Y);
            CurrentPlaneFrameS = ObjectFrameS.Translated(fObjectT, 1);

            if (have_set_plane == false) {
                sphereIndicator = IndicatorBuilder.MakeSphereIndicator(0, "hit_point",
                    fDimension.Scene(sphere_indicator_size * 0.5),
                    () => { return CurrentHitPosS; },
                    () => { return Colorf.Orange; },
                    () => { return true; });
                Indicators.AddIndicator(sphereIndicator);
                sphereIndicator.RootGameObject.SetName("hit_point");

                planeIndicator = IndicatorBuilder.MakeSectionPlaneIndicator(1, "section_plane",
                    fDimension.Scene(plane_indicator_width),
                    () => { return CurrentPlaneFrameS; },
                    () => { return new Colorf(Colorf.LightGreen, 0.5f); },
                    () => { return true; });
                Indicators.AddIndicator(planeIndicator);
                planeIndicator.RootGameObject.SetName("section_plane");

                have_set_plane = true;
            }

        }

        /// <summary>
        /// called each frame as cursor moves
        /// </summary>
        override public void Update(Vector2d downPos, Ray3f downRay)
        {
            SORayHit hit;
            if (TargetSO.FindRayIntersection(downRay, out hit)) {
                Vector3d scenePos = SceneTransforms.WorldToSceneP(this.Scene, hit.hitPos);
                CurrentHitPosS = new Frame3f(scenePos);

                float fObjectT = (CurrentHitPosS.Origin - ObjectFrameS.Origin).Dot(ObjectFrameS.Y);
                CurrentPlaneFrameS = ObjectFrameS.Translated(fObjectT, 1);
            }

        }

        /// <summary>
        /// called after click is released
        /// </summary>
        override public void End()
        {
            have_set_plane = true;
        }




        override public bool HasApply { get { return true; } }
        override public bool CanApply {
            get {
                return have_set_plane && ((CanApplyF == null) ? true : CanApplyF(TargetSO, CurrentPlaneFrameS));
            }
        }
        override public void Apply()
        {
            OnApplyF(TargetSO, CurrentPlaneFrameS);
        }


    }
}
