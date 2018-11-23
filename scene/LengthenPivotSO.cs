using System;
using g3;
using f3;

namespace gsbody
{
    public class LengthenPivotSO : PivotSO
    {
        /// <summary>
        /// Initial point we computed for Lengthen pivot, at bottom of leg
        /// Lengthening is relative to this location
        /// </summary>
        public Vector3d InitialLegPtL;

        override public SOType Type {
            get { return BodyModelSOTypes.LengthenPivot; }
        }
    }
}
