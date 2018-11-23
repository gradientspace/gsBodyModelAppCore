using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;
using f3;
using gs;

namespace gsbody
{
    public class AppDataModel
    {
        public FContext Context;
        public FScene Scene { get { return Context.Scene; } }

        public WorkflowGraph Workflow;

        public virtual void Reinitialize(FContext context)
        {
            if ( Context != null )
                disconnect();

            Context = context;

            initialize_internals();
            connect(Context);
        }


        public virtual void Disconnect()
        {
            if (Context != null)
                disconnect();
        }


        public virtual void InitializeWorkflow(WorkflowGraph graph)
        {
            this.Workflow = graph;
        }



        public virtual void Update()
        {
        }




        /*
         * Event handling stuff
         */

        public void RegisterDeleteSOAction(SceneObject so, Action deleteF)
        {
            if (SODeletedActionMap.ContainsKey(so) == false)
                SODeletedActionMap[so] = new List<Action>();

            SODeletedActionMap[so].Add(deleteF);
        }


        public void RegisterSelectSOAction(SceneObject so, Action selectF)
        {
            if (SOSelectedActionMap.ContainsKey(so) == false)
                SOSelectedActionMap[so] = new List<Action>();

            SOSelectedActionMap[so].Add(selectF);
        }


        public void RegisterDeselectSOAction(SceneObject so, Action deselectF)
        {
            if (SODeselectedActionMap.ContainsKey(so) == false)
                SODeselectedActionMap[so] = new List<Action>();

            SODeselectedActionMap[so].Add(deselectF);
        }


        Dictionary<SceneObject, List<Action>> SODeletedActionMap;
        Dictionary<SceneObject, List<Action>> SOSelectedActionMap;
        Dictionary<SceneObject, List<Action>> SODeselectedActionMap;

        protected void initialize_internals()
        {
            SODeletedActionMap = new Dictionary<SceneObject, List<Action>>();
            SOSelectedActionMap = new Dictionary<SceneObject, List<Action>>();
            SODeselectedActionMap = new Dictionary<SceneObject, List<Action>>();
        }


        protected void connect(FContext context)
        {
            Context = context;
            Scene.ChangedEvent += on_scene_changed;
            Scene.SelectedEvent += on_selected;
            Scene.DeselectedEvent += on_deselected;

            Context.RegisterEveryFrameAction("AppDataModel_update", () => {
                Update();
            });
        }
        protected void disconnect()
        {
            Context.DeregisterEveryFrameAction("AppDataModel_update");

            Scene.ChangedEvent -= on_scene_changed;
            Scene.SelectedEvent -= on_selected;
            Scene.DeselectedEvent -= on_deselected;

            SODeletedActionMap.Clear();
            SOSelectedActionMap.Clear();
            SODeselectedActionMap.Clear();

            Context = null;
            Workflow = null;
        }


        protected void on_scene_changed(object sender, SceneObject so, SceneChangeType type)
        {
            if (type == SceneChangeType.Removed) {
                List<Action> deleteActions;
                bool found = SODeletedActionMap.TryGetValue(so, out deleteActions);
                if (found) {
                    foreach (var action in deleteActions)
                        action();
                }
            }
        }


        protected void on_selected(SceneObject so)
        {
            if (Scene.Selected.Count == 1) {
                List<Action> selectActions;
                bool found = SOSelectedActionMap.TryGetValue(so, out selectActions);
                if (found) {
                    foreach (var action in selectActions)
                        action();
                }
            }
        }



        protected void on_deselected(SceneObject so)
        {
            List<Action> deselectActions;
            bool found = SODeselectedActionMap.TryGetValue(so, out deselectActions);
            if (found) {
                foreach (var action in deselectActions)
                    action();
            }
        }


    }



}
