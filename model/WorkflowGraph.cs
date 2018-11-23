using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gsbody
{
    public class WorkflowState
    {
        public string Name;

        public virtual void OnActivated()
        {
        }

        public virtual void OnDeactivated()
        {
        }
    }


    public class AnonWorkflowState : WorkflowState
    {
        public List<Action> OnActivatedF = new List<Action>();
        public List<Action> OnDeactivatedF = new List<Action>();

        public override void OnActivated() {
            foreach (Action a in OnActivatedF)
                a();
        }

        public override void OnDeactivated() {
            foreach (Action a in OnDeactivatedF)
                a();
        }

    }





    public class WorkflowTransition
    {
        public string Name;
        public WorkflowState From;
        public WorkflowState To;

        public List<Func<bool>> Preconditions = new List<Func<bool>>();

        public List<Action> BeforeTransition = new List<Action>();
        public List<Action> AfterTransition = new List<Action>();
      

    }
    public enum TransitionResult
    {
        Ok = 0,
        Failed_NoTransition = 5,
        Failed_NotInFromState = 6,
        Failed_InvalidPrecondition = 10,
    }


    public delegate void WorkflowGraphTransitionEvent(WorkflowState from, WorkflowState to);


    public class WorkflowGraph
    {
        // set this to your own function for debug output (second variable is args...)
        public delegate void LogFunc(string fmt, params object[] args);
        public LogFunc LogF;


        protected Dictionary<string, WorkflowState> States;
        protected Dictionary<string, WorkflowTransition> Transitions;
        protected WorkflowState ActiveState;

        public WorkflowGraph()
        {
            States = new Dictionary<string, WorkflowState>();
            Transitions = new Dictionary<string, WorkflowTransition>();
        }


        /// <summary>
        /// Called with before/after states, *after* transition occurs
        /// </summary>
        public event WorkflowGraphTransitionEvent OnStateTransition;



        public void AddState(WorkflowState state)
        {
            if (FindStateByName(state.Name) != null)
                throw new ArgumentException("WorkflowGraph.AddState: state with name " + state.Name + " already exists!");
            States[state.Name] = state;
        }

        public WorkflowState FindStateByName(string name)
        {
            WorkflowState state;
            if (States.TryGetValue(name, out state) == false)
                return null;
            return state;
        }


        public WorkflowTransition AddTransition(WorkflowState from, WorkflowState to)
        {
            string name = from.Name + "//" + to.Name;
            return AddTransition(from, to, name);
        }
        public WorkflowTransition AddTransition(WorkflowState from, WorkflowState to, string name)
        {
            if (FindTransitionByName(name) != null)
                throw new InvalidOperationException("WorkflowGraph.AddTransition: transition " + name + " from " + from.Name + " to " + to.Name + " already exists!");

            WorkflowTransition t = new WorkflowTransition() { From = from, To = to, Name = name };
            Transitions[name] = t;
            return t;
        }



        public WorkflowTransition FindTransitionByName(string name)
        {
            WorkflowTransition trans;
            if (Transitions.TryGetValue(name, out trans) == false)
                return null;
            return trans;
        }


        public WorkflowTransition FindTransition(WorkflowState from, WorkflowState to)
        {
            string name = from.Name + "//" + to.Name;
            return FindTransitionByName(name);
        }




        public bool IsInitialized { get { return ActiveState != null; } }

        public void SetInitialState(WorkflowState initial)
        {
            if (IsInitialized)
                throw new InvalidOperationException("WorkflowGraph.SetInitialState: already initialized to state " + ActiveState.Name);

            ActiveState = initial;
            OnStateTransition?.Invoke(null, ActiveState);
        }
        public void SetInitialState(string name)
        {
            var state = FindStateByName(name);
            if ( state == null )
                throw new ArgumentException("WorkflowGraph.FindStateByName: no existing state with name " + state.Name);
            SetInitialState(state);
        }



        public bool IsInState(WorkflowState state)
        {
            return ActiveState == state;
        }

        public bool IsInState(string name)
        {
            return ActiveState != null && ActiveState.Name == name;
        }



        public TransitionResult Transition(string name)
        {
            WorkflowTransition t = FindTransitionByName(name);
            if (t == null)
                throw new ArgumentException("WorkflowGraph.Transition: no transition with name " + name);
            return Transition(t);
        }


        public TransitionResult TransitionToState(string name)
        {
            var state = FindStateByName(name);
            if (state == null)
                throw new ArgumentException("WorkflowGraph.TransitionToState: no existing state with name " + state.Name);
            return TransitionToState(state);
        }
        public TransitionResult TransitionToState(WorkflowState newState)
        {
            WorkflowTransition t = FindTransition(ActiveState, newState);
            if (t == null) {
                if (LogF != null)
                    LogF("[WorfklowGraph.TransitionToState] ANONYMOUS transition from {0} to {1} does not exist!", ActiveState.Name, newState.Name );
                return TransitionResult.Failed_NoTransition;
            }
            return Transition(t);
        }



        public TransitionResult Transition(WorkflowTransition t)
        {
            if ( ActiveState != t.From ) {
                if (LogF != null)
                    LogF("[WorfklowGraph.Transition] cannot from {0} to {1} along {2} because in state {3}!", t.From.Name, t.To.Name, t.Name, ActiveState.Name);
                return TransitionResult.Failed_InvalidPrecondition;
            }

            foreach (var p in t.Preconditions) {
                bool r = p();
                if (r == false) {
                    if (LogF != null)
                        LogF("[WorfklowGraph.Transition] precondition failed on transition {0} from {1} to {2}!", t.Name, ActiveState.Name, t.To.Name);
                    return TransitionResult.Failed_InvalidPrecondition;
                }
            }

            foreach (Action b in t.BeforeTransition)
                b();

            var prevState = ActiveState;
            ActiveState.OnDeactivated();
            ActiveState = t.To;
            ActiveState.OnActivated();


            foreach (Action a in t.AfterTransition)
                a();

            if (LogF != null)
                LogF("[WorfklowGraph.Transition] transitioned from {0} to {1} along {2}!", t.From.Name, t.To.Name, t.Name);

            OnStateTransition?.Invoke(prevState, ActiveState);

            return TransitionResult.Ok;
        }


        public bool CanTransitionToState(string stateName)
        {
            foreach ( var t in Transitions.Values ) {
                if (t.From == ActiveState && t.To.Name == stateName && CanTransition(t))
                    return true;
            }
            return false;
        }
        public bool CanTransition(string transitionName)
        {
            WorkflowTransition t = FindTransitionByName(transitionName);
            return (t == null) ? false : CanTransition(t);
        }
        public bool CanTransition(WorkflowTransition t)
        {
            if (ActiveState != t.From)
                return false;
            foreach (var p in t.Preconditions) {
                bool r = p();
                if (r == false)
                    return false;
            }
            return true;
        }


    }




}
