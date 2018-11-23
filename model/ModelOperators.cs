using System;
using System.Collections.Generic;
using System.Linq;
using g3;
using gs;
using f3;

namespace gsbody
{

    /// <summary>
    /// Returns the DCurve from an ThreadSafePolyCurveSO in a thread-safe way.
    /// You can only use this in a background model compute, otherwise it will (currently) throw an exception!
    /// 
    /// [TODO] provide non-threaded option?
    /// </summary>
    public class PolyCurveSOSourceOp : BaseModelingOperator, DCurve3SourceOp
    {
        ThreadSafePolyCurveSO Source;

        DCurve3 Curve;
        bool curve_valid = false;

        public PolyCurveSOSourceOp(ThreadSafePolyCurveSO source)
        {
            Source = source;
            source.OnCurveModified += on_curve_modified;
        }

        public void ReplaceSource(ThreadSafePolyCurveSO source)
        {
            if (Source != null)
                Source.OnCurveModified -= on_curve_modified;
            Source = source;
            if (Source != null) {
                Source.OnCurveModified += on_curve_modified;
                curve_valid = false;
                PostOnOperatorModified();
            }
        }


        void on_curve_modified(PolyCurveSO so)
        {
            curve_valid = false;
            PostOnOperatorModified();
        }


        public void Update()
        {
            Curve = Source.RequestCurveCopyFromBGThread();
            curve_valid = true;
        }


        public ISampledCurve3d GetICurve()
        {
            if (!curve_valid)
                Update();
            return Curve;
        }

        public DCurve3 ExtractDCurve()
        {
            if (!curve_valid)
                Update();

            var result = Curve;
            Curve = null;
            curve_valid = false;
            return result;
        }

    }


}

