using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using f3;
using g3;

namespace gsbody
{
    public static class BodyModelSOTypes
    {

        static readonly public SOType Scan =
            new SOType("ScanSO", Type.GetType("gsbody.ScanSO"), null);

        static readonly public SOType Leg =
            new SOType("LegSO", Type.GetType("gsbody.LegSO"), null);
        static readonly public SOType RectifiedLeg =
            new SOType("RectifiedLegSO", Type.GetType("gsbody.RectifiedLegSO"), null);
        static readonly public SOType Socket =
            new SOType("SocketSO", Type.GetType("gsbody.SocketSO"), null);

        static readonly public SOType TrimLoop =
                    new SOType("TrimLoopSO", Type.GetType("gsbody.TrimLoopSO"), null);

        static readonly public SOType EnclosedPatch =
            new SOType("EnclosedPatchSO", Type.GetType("gsbody.EnclosedPatchSO"), null);

        static readonly public SOType PlaneIntersectionCurve =
                    new SOType("PlaneIntersectionCurveSO", Type.GetType("gsbody.PlaneIntersectionCurveSO"), null);

        // used for the handle at the bottom of a socket, to control Lengthen deform op
        static readonly public SOType LengthenPivot =
            new SOType("LengthenPivotSO", Type.GetType("gsbody.LengthenPivotSO"), null);



        public static void RegisterSocketGenTypes(SORegistry registry)
        {
            registry.RegisterType(Scan, EmitScanSO, BuildScanSO);
            registry.RegisterType(EnclosedPatch, EmitEnclosedPatchSO, BuildEnclosedPatchSO);
            registry.RegisterType(PlaneIntersectionCurve, EmitPlaneIntersectionCurveSO, BuildPlaneIntersectionCurveSO);
            registry.RegisterType(TrimLoop, EmitTrimLoopSO, BuildTrimLoopSO);
            registry.RegisterType(LengthenPivot, EmitLengthenPivotSO, BuildLengthenPivotSO);
        }



        public static bool EmitScanSO(SceneSerializer s, IOutputStream o, SceneObject gso)
        {
            ScanSO so = gso as ScanSO;
            o.AddAttribute(IOStrings.ASOType, so.Type.identifier);
            SceneSerializerEmitTypesExt.EmitDMeshSO(s, o, so as DMeshSO);
            return true;
        }
        public static SceneObject BuildScanSO(SOFactory factory, FScene scene, TypedAttribSet attributes)
        {
            ScanSO so = new ScanSO();
            factory.RestoreDMeshSO(scene, attributes, so);
            return so;
        }




        public static bool EmitEnclosedPatchSO(SceneSerializer s, IOutputStream o, SceneObject gso)
        {
            EnclosedPatchSO so = gso as EnclosedPatchSO;
            if (so == null)
                throw new Exception("EmitEnclosedPatchSO: input so is not an EnclosedPatchSO!");

            o.AddAttribute(IOStrings.ASOType, so.Type.identifier);
            SceneSerializerEmitTypesExt.EmitPolyCurveSO(s, o, so as PolyCurveSO);
            return true;
        }
        public static SceneObject BuildEnclosedPatchSO(SOFactory factory, FScene scene, TypedAttribSet attributes)
        {
            EnclosedPatchSO so = new EnclosedPatchSO();
            so.Create(scene.DefaultCurveSOMaterial);
            factory.RestorePolyCurveSOType(scene, attributes, so);
            return so;
        }




        public static bool EmitPlaneIntersectionCurveSO(SceneSerializer s, IOutputStream o, SceneObject gso)
        {
            PlaneIntersectionCurveSO so = gso as PlaneIntersectionCurveSO;
            if (so == null)
                throw new Exception("EmitPlaneIntersectionCurveSO: input so is not an PlaneIntersectionCurveSO!");
            o.AddAttribute(IOStrings.ASOType, so.Type.identifier);
            SceneSerializerEmitTypesExt.EmitPolyCurveSO(s, o, so as PolyCurveSO);
            return true;
        }
        public static SceneObject BuildPlaneIntersectionCurveSO(SOFactory factory, FScene scene, TypedAttribSet attributes)
        {
            PlaneIntersectionCurveSO so = new PlaneIntersectionCurveSO();
            so.Create(scene.DefaultCurveSOMaterial);
            factory.RestorePolyCurveSOType(scene, attributes, so);
            return so;
        }





        public static bool EmitTrimLoopSO(SceneSerializer s, IOutputStream o, SceneObject gso)
        {
            TrimLoopSO so = gso as TrimLoopSO;
            if (so == null)
                throw new Exception("EmitTrimLoopSO: input so is not an TrimLoopSO!");
            o.AddAttribute(IOStrings.ASOType, so.Type.identifier);
            SceneSerializerEmitTypesExt.EmitPolyCurveSO(s, o, so as PolyCurveSO);
            return true;
        }
        public static SceneObject BuildTrimLoopSO(SOFactory factory, FScene scene, TypedAttribSet attributes)
        {
            TrimLoopSO so = new TrimLoopSO();
            so.Create(scene.DefaultCurveSOMaterial);
            factory.RestorePolyCurveSOType(scene, attributes, so);
            return so;
        }



        public static readonly string PivotOriginalFrameStruct = "OriginalFrameS";  // this is deprecated
        public static readonly string PivotOriginalLegPoint = "InitialLegPtL";

        public static bool EmitLengthenPivotSO(SceneSerializer s, IOutputStream o, SceneObject gso)
        {
            LengthenPivotSO so = gso as LengthenPivotSO;
            if (so == null)
                throw new Exception("EmitLengthenPivotSO: input so is not an LengthenPivotSO!");
            o.AddAttribute(IOStrings.ASOType, so.Type.identifier);
            s.EmitFrame(o, PivotOriginalLegPoint, new Frame3f(so.InitialLegPtL));
            SceneSerializerEmitTypesExt.EmitPivotSO(s, o, so as PivotSO);
            return true;
        }
        public static SceneObject BuildLengthenPivotSO(SOFactory factory, FScene scene, TypedAttribSet attributes)
        {
            LengthenPivotSO so = new LengthenPivotSO();
            so.Create(scene.PivotSOMaterial);
            factory.RestorePivotSOType(scene, attributes, so);
            if (factory.find_struct(attributes, PivotOriginalLegPoint) != null) {
                Frame3f frameL = factory.RestoreFrame(attributes, PivotOriginalLegPoint);
                so.InitialLegPtL = frameL.Origin;
            } else {
                so.InitialLegPtL = Vector3d.Zero;
            }
            return so;
        }



    }

}
