using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;
using g3;

namespace gsbody
{
    public class LegSO : DMeshSO
    {

        override public SOType Type { get { return BodyModelSOTypes.Leg; } }

        override public bool SupportsScaling {
            get { return false; }
        }
    }




    public class RectifiedLegSO : DMeshSO
    {
        override public SOType Type { get { return BodyModelSOTypes.RectifiedLeg; } }

        override public bool SupportsScaling {
            get { return false; }
        }
    }
}
