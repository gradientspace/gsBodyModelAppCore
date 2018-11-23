using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using gs;

namespace gsbody
{


    /// <summary>
    /// [RMS] simple connector with a flat base, no holes
    /// </summary>
    public class VariableSizeFlatBaseConnector : SocketConnector
    {
        double base_diameter = 75;
        public double BaseDiameter {
            get { return base_diameter; }
            set { base_diameter = value; geometry_valid = false; post_modified_event(); }
        }

        double wall_thickness = 5;
        public double WallThickness {
            get { return wall_thickness; }
            set { wall_thickness = value; geometry_valid = false; post_modified_event(); }
        }

        double base_thickness = 5;
        public double BaseThickness {
            get { return base_thickness; }
            set { base_thickness = value; geometry_valid = false; post_modified_event(); }
        }

        double inner_vertical_space = 5;
        public double InnerVerticalSpace {
            get { return inner_vertical_space; }
            set { inner_vertical_space = value; geometry_valid = false; post_modified_event(); }
        }

        public VariableSizeFlatBaseConnector() : base()
        {
        }


        bool geometry_valid = false;


        protected override void validate_geometry()
        {
            if (geometry_valid)
                return;

            // cache parameters
            float outer_diam = (float)base_diameter;
            float wall_thick = (float)wall_thickness;
            float base_thick = (float)base_thickness;
            float inner_height = (float)inner_vertical_space;
            float inner_diam = outer_diam - wall_thick;
            generate(outer_diam, inner_height + base_thick, wall_thick, base_thick);

            geometry_valid = true;
        }


        protected void generate(float fDiameter, float fHeight, float fWallThickness, float fBaseThickness)
        {
            base.reset_holes();

            CappedCylinderGenerator outer_cylgen = new CappedCylinderGenerator() {
                BaseRadius = fDiameter / 2, TopRadius = fDiameter / 2,
                Height = fHeight + 10,
                Slices = 60,
                Clockwise = true
            };
            DMesh3 outer_mesh = outer_cylgen.Generate().MakeDMesh();

            float fInnerDiam = fDiameter - 2 * fWallThickness;
            CappedCylinderGenerator inner_cylgen = new CappedCylinderGenerator() {
                BaseRadius = fInnerDiam / 2, TopRadius = fInnerDiam / 2,
                Height = fHeight + 10,
                Slices = 60,
                Clockwise = false
            };
            DMesh3 inner_mesh = inner_cylgen.Generate().MakeDMesh();
            MeshTransforms.Translate(inner_mesh, fBaseThickness * Vector3d.AxisY);

            DMesh3[] meshes = new DMesh3[2] { outer_mesh, inner_mesh };

            foreach (DMesh3 mesh in meshes) {
                Remesher r = new Remesher(mesh);
                r.SetTargetEdgeLength(TargetEdgeLength);
                r.SmoothSpeedT = 0.5f;
                r.SetExternalConstraints(new MeshConstraints());
                MeshConstraintUtil.FixAllGroupBoundaryEdges(r.Constraints, mesh, true);
                r.SetProjectionTarget(MeshProjectionTarget.Auto(mesh));
                for (int k = 0; k < 10; ++k)
                    r.BasicRemeshPass();
            }

            Vector3d vCutPos = new Vector3d(0, fHeight, 0);
            Vector3d vCutNormal = Vector3d.AxisY;

            foreach (DMesh3 mesh in meshes) {
                MeshPlaneCut cut = new MeshPlaneCut(mesh, new Vector3d(0, fHeight, 0), Vector3d.AxisY);
                cut.Cut();
            }

            base.set_output_meshes(inner_mesh, outer_mesh);
        }
    }














}
