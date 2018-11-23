using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;
using g3;

namespace gsbody
{
    public class ScanSO : DMeshSO
    {

        override public SOType Type { get { return BodyModelSOTypes.Scan; } }


        override public bool SupportsScaling {
            get { return false; }
        }
    }




}
