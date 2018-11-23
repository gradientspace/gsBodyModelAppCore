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
    /// SingleMeshShapeModel mesh is a pointer to SocketSO.Mesh  (should be a copy??)
    /// </summary>
    public class SocketModel : SingleMeshShapeModel
    {

        public enum ModelModes
        {
            Socket, AFO
        };
        ModelModes model_mode = ModelModes.Socket;
        public ModelModes Mode {
            get { return model_mode; }
        }


        SocketSO socket;
        public SocketSO Socket
        {
            get { return socket; }
        }

        LegModel leg;
        public LegModel SourceLeg {
            get { return leg; }
        }

        TrimLoopSO trimLine;

        bool enable_update = true;
        public bool EnableUpdate {
            get { return enable_update; }
            set { enable_update = value; }
        }


        float socket_offset = 2.0f;
        public float SocketOffset {
            get { return socket_offset; }
            set { socket_offset = value; DeviceGenOp.InnerWallOffset = value;  } 
        }

        float socket_thickness = 5.0f;
        public float SocketThickness {
            get { return socket_thickness; }
            set { socket_thickness = value; DeviceGenOp.SocketThickness = value; } 
        }

        float connector_cut_height = 25.0f;
        public float ConnectorCutHeight {
            get { return connector_cut_height; }
            set { connector_cut_height = value; DeviceGenOp.ConnectorCutHeight = value; }
        }

        bool flip_trim_side = false;
        public bool FlipTrimSide {
            get { return flip_trim_side; }
            set { flip_trim_side = value; DeviceGenOp.FlipTrimSide = value; }
        }

        // this event is called whenever the socket mesh is updated (ie after the background compute finishes)
        public delegate void SocketUpdatedEventHandler();
        public SocketUpdatedEventHandler OnSocketUpdated;


        public enum SocketStatus
        {
            FullSocket, PreviewSocket, ErrorSocket
        }

        // this event is called when the background compute finishes
        public delegate void SocketUpdateStatusEventHandler(SocketStatus status, string message);
        public SocketUpdateStatusEventHandler OnSocketUpdateStatus;


        ShapeModelOutputMeshSourceOp LegSourceOp;
        PolyCurveSOSourceOp TrimlineSourceOp;
        DeviceGeneratorOp DeviceGenOp;
        ThreadedMeshComputeOp Compute;


        public SocketModel(SocketSO Socket, LegModel leg, TrimLoopSO trimLineIn, ModelModes eMode) : base(Socket.Mesh, false)
        {
            socket = Socket;

            this.leg = leg;
            leg.SO.OnTransformModified += leg_transform_modified;

            this.trimLine = trimLineIn;

            LegSourceOp = new ShapeModelOutputMeshSourceOp(leg);
            TrimlineSourceOp = new PolyCurveSOSourceOp(this.trimLine);

            if (eMode == ModelModes.Socket) {
                DeviceGenOp = new SocketGeneratorOp();
            } else {
                DeviceGenOp = new AFOGeneratorOp();
            }

            DeviceGenOp.MeshSource = LegSourceOp;
            DeviceGenOp.CurveSource = TrimlineSourceOp;
            DeviceGenOp.SocketVertexColor = Colorf.CornflowerBlue;
            DeviceGenOp.PartialSocketVertexColor = ColorMixer.Darken(Colorf.SelectionGold, 0.75f);
            DeviceGenOp.SocketThickness = this.SocketThickness;
            DeviceGenOp.InnerWallOffset = this.SocketOffset;
            DeviceGenOp.ConnectorCutHeight = this.ConnectorCutHeight;

            Compute = new ThreadedMeshComputeOp() {
                MeshSource = DeviceGenOp
            };

            // initialize socket input transform
            leg_transform_modified(leg.SO);
        }

        private void leg_transform_modified(SceneObject so)
        {
            Frame3f legFrame = leg.SO.GetLocalFrame(CoordSpace.ObjectCoords);
            //legFrame.Origin = Vector3f.Zero;
            DeviceGenOp.InputsTransform = legFrame;
        }

        public void RemoveTrimLine()
        {
            EnableUpdate = false;

            trimLine = null;
            TrimlineSourceOp.ReplaceSource(null);
        }

        public void SetNewTrimLine(TrimLoopSO so)
        {
            trimLine = so;
            TrimlineSourceOp.ReplaceSource(so);
        }



        /// <summary>
        /// Direct access to internal DeviceGenerator operator. Kind of dangerous...
        /// </summary>
        public DeviceGeneratorOp DeviceGenerator {
            get { return DeviceGenOp; }
        }
        public bool IsSocket { get { return DeviceGenOp != null && DeviceGenOp is SocketGeneratorOp; } }
        public bool IsAFO { get { return DeviceGenOp != null && DeviceGenOp is AFOGeneratorOp; } }
        public SocketGeneratorOp DeviceSocket { get { return DeviceGenOp as SocketGeneratorOp; } }
        public AFOGeneratorOp DeviceAFO { get { return DeviceGenOp as AFOGeneratorOp; } }

        public void ShowSocket()
        {
            if (Socket != null)
                SceneUtil.SetVisible(Socket, true);
        }
        public void HideSocket()
        {
            if (Socket != null)
                SceneUtil.SetVisible(Socket, false);
        }



        public void Update()
        {
            if (EnableUpdate == false)
                return;

            try {
                DMeshOutputStatus result = Compute.CheckForNewMesh();
                if (result.State == DMeshOutputStatus.States.Ready) {
                    socket.ReplaceMesh(result.Mesh);
                    OnSocketUpdated?.Invoke();
                    OnSocketUpdateStatus?.Invoke(
                        (DeviceGenOp.LastResultStatus == DeviceGeneratorOp.ResultStatus.PreviewResult) ? 
                            SocketStatus.PreviewSocket : SocketStatus.FullSocket, "Ok" );
                }
            } catch (Exception e) {
                DebugUtil.Log(2, "SocketModel.Update: caught exception! " + e.Message);
                DebugUtil.Log(2, e.StackTrace);
                OnSocketUpdateStatus?.Invoke(SocketStatus.ErrorSocket, "[REPLACE_EXCEPTION] " + e.Message);
            }

            if ( Compute.HaveBackgroundException ) {
                Exception e = Compute.ExtractBackgroundException();
                DebugUtil.Log(2, "SocketMode.Update: exception in background compute: " + e.Message);
                DebugUtil.Log(2, e.StackTrace);
                OnSocketUpdateStatus?.Invoke(SocketStatus.ErrorSocket, "[COMPUTE_EXCEPTION] " + e.Message);
            }
        }


        public void Disconnect()
        {
            trimLine = null;
        }



    }
}
