using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;
using g3;
using gs;

namespace gsbody
{
    public class SocketSO : DMeshSO
    {
        public bool EnableRayIntersection = false;
        public bool EnableSelection = false;


        override public SOType Type { get { return BodyModelSOTypes.Socket; } }


        override public bool IsSelectable {
            get { return EnableSelection; }
        }



        override public bool FindRayIntersection(Ray3f ray, out SORayHit hit)
        {
            if (EnableRayIntersection == false) {
                hit = null;
                return false;
            } else {
                return base.FindRayIntersection(ray, out hit);
            }
        }

    }
}
