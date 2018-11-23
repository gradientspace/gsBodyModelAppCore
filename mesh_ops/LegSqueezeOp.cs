// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    /// <summary>
    /// LegSqueezeOp applies horizontal scaling around an estimated spine curve, with
    /// the scaling factor being smoothly blended between a set of control points.
    /// 
    /// This operator is kind of ugly and weird, FYI.
    /// 
    /// 
    /// In the most basic mode, there is a top and bottom reduce-percentage in range [0,100].
    /// This gets converted to a scaling factor via 1-percent/100. So a percentage of 5% means
    /// scale by 0.95. We blend via smooth-lerp between the bottom and top percentages.
    /// 
    /// Although the top percent can be set, this produces a discontinuity, and so it should
    /// not be used, obviously.
    /// 
    /// An arbitrary number of "midpoints" can be set to vary the squeeze amount. The scaling
    /// is done around a "spine curve" that we estimate by binning the vertices based on 
    /// y-height-closest endpoint/midpoint (spine vertex is at centroid of each bin).
    /// 
    /// Each midpoint is a 2-tuple (t_value, new_percent), where t is a value in range [0,1].
    /// The bottom point has t=0 and top t=1, and the midpoints need to have sequentially-increasing t values. 
    /// So for example a midpoint set might be:
    ///    (0.25, 3.0),  (0.5, 6.0),  (0.75, 8.0)
    /// This would split the t-range along the axis approximately evenly into 4 segments.
    /// We smoothly blend from one segment to the next.
    /// 
    /// UpdateMidpoints() must be used to set the number of midpoints. SetMidPoint can be
    /// used to change the midpoint values.
    /// 
    /// </summary>
    public class LegSqueezeOp : BaseModelingOperator, IVectorDisplacementSourceOp
    {
        VectorDisplacement Displacement;

        Vector3d upperPoint = new Vector3d(0, 100, 0);
        public Vector3d UpperPoint {
            get { return upperPoint; }
            set { upperPoint = value; on_modified(); }
        }

        Vector3d axis = Vector3d.AxisY;
        public Vector3d Axis {
            get { return axis; }
            set { axis = value; on_modified(); }
        }

        double reduce_percent_top = 0;
        public double ReductionPercentTop {
            get { return reduce_percent_top; }
            set { reduce_percent_top = value; on_modified(); }
        }

        double reduce_percent_bottom = 10;
        public double ReductionPercentBottom {
            get { return reduce_percent_bottom; }
            set { reduce_percent_bottom = value; on_modified(); }
        }


        List<Vector2d> midPoints = new List<Vector2d>();

        public int GetNumMidPoints() {
            return midPoints.Count;
        }
        public void UpdateMidpoints(List<Vector2d> newMidPoints) {
            midPoints = new List<Vector2d>(newMidPoints);
            on_modified();
        }
        public Vector2d GetMidPoint(int i) {
            return midPoints[i];
        }
        public void SetMidPoint(int i, double t, double percent) {
            if (t <= 0 || t >= 1)
                throw new Exception("LegSqueezeOp: invalid midpoint t value!");
            if (i > 0 && midPoints[i-1].x > t )
                throw new Exception("LegSqueezeOp: t value is less than previous t value!");
            if (i < midPoints.Count-1 && midPoints[i+1].x < t)
                throw new Exception("LegSqueezeOp: t value is greater than next t value!");
            midPoints[i] = new Vector2d(t, percent);
            on_modified();
        }


        IMeshSourceOp mesh_source;
        public IMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_source_modified;
                mesh_source = value;
                if ( mesh_source != null )
                    mesh_source.OperatorModified += on_source_modified;
                on_modified();
            }
        }


        public LegSqueezeOp(IMeshSourceOp meshSource = null)
        {
            Displacement = new VectorDisplacement();
            if ( meshSource != null )
                MeshSource = meshSource;
        }


        bool result_valid = false;

        protected virtual void on_modified()
        {
            result_valid = false;
            PostOnOperatorModified();
        }
        protected virtual void on_source_modified(ModelingOperator op)
        {
            on_modified();
        }



        public virtual void Update()
        {
            if ( MeshSource == null )
                throw new Exception("LegSqueezeOp: must set valid MeshSource to compute!");

            IMesh mesh = MeshSource.GetIMesh();
            if (mesh.HasVertexNormals == false)
                throw new Exception("LegSqueezeOp: input mesh does not have surface normals...");

            Displacement.Resize(mesh.MaxVertexID);

            // compute extents along axis
            double upper_t = UpperPoint.Dot(Axis);
            Interval1d axis_extents = MeshMeasurements.ExtentsOnAxis(mesh, axis);
            double lower_t = axis_extents.a;


            // compute approximate skeleton
            int nVertices = midPoints.Count + 2;
            Vector3d[] centers = new Vector3d[nVertices];
            int[] counts = new int[nVertices];
            foreach (int vid in mesh.VertexIndices()) {
                Vector3d v = mesh.GetVertex(vid);
                double t = v.Dot(ref axis);
                int iBin = 0;
                if (t > upper_t) {
                    iBin = nVertices-1;
                } else if (t > lower_t) {
                    double unit_t = (t - lower_t) / (upper_t - lower_t);
                    iBin = 1;
                    for ( int k = 0; k < midPoints.Count; ++k ) {
                        if ( unit_t > midPoints[k].x ) 
                            iBin++;
                    }
                }   // else iBin = 0, as initialized
                centers[iBin] += v;
                counts[iBin]++;
            }
            for (int k = 0; k < centers.Length; ++k)
                centers[k] /= counts[k];


            // todo: can do this in parallel
            foreach (int vid in mesh.VertexIndices()) {
                Vector3d v = mesh.GetVertex(vid);
                double t = v.Dot(axis);
                if (t > upper_t)
                    continue;

                Vector3d center = centers[0];
                double percent = 0;

                if (t >= upper_t) {
                    percent = reduce_percent_top;
                    center = centers[nVertices - 1];
                } else if (t <= lower_t) {
                    percent = reduce_percent_bottom;
                    center = centers[0];

                } else if (midPoints.Count == 0) {
                    double unit_t = (t - lower_t) / (upper_t - lower_t);
                    unit_t = MathUtil.WyvillRise01(unit_t);
                    percent = MathUtil.Lerp(reduce_percent_bottom, reduce_percent_top, unit_t);
                } else {
                    double unit_t = (t - lower_t) / (upper_t - lower_t);
                    double low_percent = reduce_percent_bottom;
                    double high_percent = reduce_percent_top;
                    Vector3d low_center = centers[0];
                    Vector3d high_center = centers[0];
                    double low_t = 0.0, high_t = 0;
                    for (int i = 0; i < midPoints.Count; ++i) {
                        if (unit_t < midPoints[i].x) {
                            high_t = midPoints[i].x;
                            high_percent = midPoints[i].y;
                            high_center = centers[i + 1];
                            break;
                        }
                        low_t = midPoints[i].x;
                        low_percent = midPoints[i].y;
                        low_center = centers[i + 1];
                    }
                    if (high_t == 0) {
                        high_t = 1.0;
                        high_percent = reduce_percent_top;
                        high_center = centers[nVertices-1];
                    }
                    double a = (unit_t - low_t) / (high_t - low_t);
                    a = MathUtil.WyvillRise01(a);
                    percent = MathUtil.Lerp(low_percent, high_percent, a);
                    center = Vector3d.Lerp(low_center, high_center, a);
                }

                percent = percent / 100;

                double scale = 1.0 - percent;
                Vector3d v_scaled = (v - center) * new Vector3d(scale, 1, scale) + center;
                Displacement[vid] = v_scaled - v;
            }

            result_valid = true;
        }


        public IVectorDisplacement GetDisplacement()
        {
            if (result_valid == false)
                Update();

            return Displacement;
        }
    }
}
