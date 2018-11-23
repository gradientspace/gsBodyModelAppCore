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
    public class ThreadSafePolyCurveSO : PolyCurveSO
    {
        public enum OutputCurveTransform
        {
            NoTransform, ToScene, ToTargetSO
        }
        public OutputCurveTransform TransformMode = OutputCurveTransform.NoTransform;



        protected SingleMeshShapeModel TargetModel;
        protected SceneObject TargetModelSO;            // only used for ToTargetSO mode



        /// <summary>
        /// Connect this curve to a target ShapeModel and associated SO (the SO is optional, if you don't need it)
        /// </summary>
        public void ConnectToTarget(SingleMeshShapeModel target, SceneObject targetSO, bool bSetTargetMode)
        {
            if (TargetModel != null)
                TargetModel.OnSourceMeshModified -= on_target_model_modified;

            TargetModel = target;
            TargetModelSO = targetSO;
            if (bSetTargetMode)
                TransformMode = OutputCurveTransform.ToTargetSO;

            TargetModel.OnSourceMeshModified += on_target_model_modified;
        }
        void on_target_model_modified(IShapeModelMeshSource src)
        {
            target_model_modified = true;
        }


        // dirty flags
        int curve_timestamp = -1;
        bool target_model_modified = false;

        // curve stuff
        DCurve3 curve_copy = null;
        object curve_copy_lock = new object();
        EventWaitHandle waiter = new EventWaitHandle(false, EventResetMode.AutoReset);

        /// <summary>
        /// Request a copy of the internal curve from a background thread. 
        /// If curve is dirty, this will block until it is computed in PreRender (which will then signal this thread).
        /// </summary>
        public DCurve3 RequestCurveCopyFromBGThread()
        {
            if (FPlatform.InMainThread())
                throw new Exception("ThreadSafePolyCurveSO.RequestCurveCopyFromBGThread: called from main thread!");

            bool wait_for_compute = false;
            lock (curve_copy_lock) {
                if (curve_copy == null || this.curve.Timestamp != curve_timestamp || target_model_modified)
                    wait_for_compute = true;
            }
            if (wait_for_compute)
                waiter.WaitOne();

            DCurve3 result;
            lock (curve_copy_lock) {
                result = new DCurve3(curve_copy);
                result.Timestamp = curve_copy.Timestamp;
            }
            return result;
        }


        public DCurve3 RequestCurveCopyFromMainThread()
        {
            DCurve3 result;
            lock (curve_copy_lock) {
                result = new DCurve3(curve_copy);
            }
            return result;
        }



        /// <summary>
        /// Called after curve-copy is updated in PreRender(). 
        /// You can use this in subclasses to implement additional behavior.
        /// </summary>
        protected virtual void on_curve_validated()
        {
        }


        public override void PreRender()
        {
            base.PreRender();

            if (this.curve.Timestamp != curve_timestamp || target_model_modified) {

                // lock copy-curve while we rebuild it
                lock (curve_copy_lock) {
                    curve_copy = new DCurve3(this.curve);

                    if (TransformMode == OutputCurveTransform.ToTargetSO && TargetModelSO == null)
                        throw new InvalidOperationException("ThreadSafePolyCurveSO.PreRender: no TargetSO but target mode requested!");

                    for (int k = 0; k < curve_copy.VertexCount; ++k) {
                        Vector3f f = (Vector3f)curve_copy[k];
                        f = SceneTransforms.ObjectToSceneP(this, f);
                        if ( TransformMode == OutputCurveTransform.ToTargetSO )
                            f = SceneTransforms.SceneToObjectP(TargetModelSO, f);
                        curve_copy[k] = f;
                    }
                    curve_copy.Timestamp = this.curve.Timestamp;
                }
                waiter.Set();  // signal any threads waiting on this update

                curve_timestamp = this.curve.Timestamp;
                target_model_modified = false;

                on_curve_validated();
            }
        }

    }
}
