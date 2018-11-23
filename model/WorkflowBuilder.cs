using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using gs;

namespace gsbody
{
    public class WorkflowBuilder
    {
        public WorkflowGraph Graph;
        
        public WorkflowBuilder(WorkflowGraph g)
        {
            Graph = g;
        }


        public WorkflowState AddState(string identifier, Action activatedF = null, Action deactivatedF = null)
        {
            AnonWorkflowState state = new AnonWorkflowState() { Name = identifier };
            if (activatedF != null)
                state.OnActivatedF.Add(activatedF);
            if (deactivatedF != null)
                state.OnDeactivatedF.Add(deactivatedF);
            Graph.AddState(state);
            return state;
        }


        public void AddTransition(WorkflowState fromState, WorkflowState toState, string identifier, 
            Func<bool> precondition, Action beforeF, Action afterF)
        {
            WorkflowTransition transition = Graph.AddTransition(fromState, toState, identifier);
            if ( precondition != null )
                transition.Preconditions.Add(precondition);
            if (beforeF != null)
                transition.BeforeTransition.Add(beforeF);
            if (afterF != null)
                transition.AfterTransition.Add(afterF);
        }


    }






}
