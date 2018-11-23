using System;
using g3;
using f3;
using gs;

namespace gsbody
{

    public class DrawMultiClickLoopToolBuilder : DrawSurfaceCurveToolBuilder
    {
        public fDimension IndicatorSize = fDimension.World(0.2f);

        protected override DrawSurfaceCurveTool new_tool(FScene scene, SceneObject target)
        {
            InputMode = DrawSurfaceCurveTool.DrawMode.OnClick;
            // [RMS] by default we set this to false, so we are drawing an open curve
            Closed = false;
            IsOverlayCurve = true;
            var result = new DrawMultiClickLoopTool(scene, target);
            result.IndicatorSize = IndicatorSize;
            return result;
        }
    }



    /// <summary>
    /// DrawSurfaceCurveTool specialization for drawing closed loops with multiple clicks. Basically is just
    /// adding indicator dots for click points, right now...
    /// </summary>
    public class DrawMultiClickLoopTool : DrawSurfaceCurveTool
    {
        ToolIndicatorSet indicators;


        fDimension indicator_size = fDimension.World(0.25f);
        virtual public fDimension IndicatorSize {
            get { return indicator_size; }
            set { indicator_size = value; }
        }


        public DrawMultiClickLoopTool(FScene scene, SceneObject target) : base(scene, target)
        {
            indicators = new ToolIndicatorSet(this, scene);
        }



        override public void PreRender()
        {
            indicators.PreRender();
            base.PreRender();
        }


        override public void Shutdown()
        {
            indicators.Disconnect(true);
            base.Shutdown();
        }


        public override void EndDraw()
        {
            indicators.ClearAllIndicators();

            // before we generate result, we set curve to closed (ie Loop)
            this.Closed = true;

            base.EndDraw();
        }


        protected override void OnAddedClickPoint(Vector3d vNew, bool bFirst)
        {
            Vector3f pos = (Vector3f)preview[preview.VertexCount - 1];
            Vector3f color = (bFirst) ? Colorf.VideoRed : Colorf.ForestGreen;
            SphereIndicator dot = new SphereIndicator() {
                SceneFrameF = () => { return new Frame3f(pos); },
                Radius = indicator_size,
                ColorF = () => { return color; }
            };
            indicators.AddIndicator(dot);
        }





        override public bool HasApply { get { return false; } }
        override public bool CanApply { get { return false; } }
        override public void Apply() {
            EndDraw();
        }



    }
}
