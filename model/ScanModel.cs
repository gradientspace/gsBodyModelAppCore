using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using gs;
using f3;

namespace gsbody
{
    /// <summary>
    /// Shape Model for a scan (no parametrics?)
    /// </summary>
    public class ScanModel : SingleMeshShapeModel
    {
        ScanSO scan;
        public ScanSO SO
        {
            get { return scan; }
        }


        Vector3d user_base_point;
        bool base_point_set = false;
        public bool HasUserBasePoint {
            get { return base_point_set; }
        }
        public Vector3d UserBasePoint {
            get { return user_base_point; }
            set { user_base_point = value; base_point_set = true; }
        }



        public ScanModel(ScanSO scanIn) : base(scanIn.Mesh, false, scanIn.Spatial)
        {
            scan = scanIn;
            scan.Name = "Scan";

            scanIn.OnMeshModified += on_scan_modified;
            ReplaceOutputMesh(scan.Mesh, true);
        }

        private void on_scan_modified(DMeshSO so)
        {
            ReplaceOutputMesh(scan.Mesh, true);
        }


        public void Hide() {
            SceneUtil.Hide(scan);
        }


        public void Update()
        {
        }


        public void Disconnect()
        {
            scan.OnMeshModified -= on_scan_modified;
        }

    }
}
