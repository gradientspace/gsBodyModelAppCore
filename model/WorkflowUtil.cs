using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using gs;

namespace gsbody
{
    public class WorkflowRouter
    {
        public struct Entry
        {
            public string State;
            public string Transition;
        }

        public Action UnknownAction;

        public List<Entry> Table = new List<Entry>();



        public void AddTransition(string state, string transition)
        {
            Entry e = new Entry() { State = state, Transition = transition };
            Table.Add(e);
        }



        public bool Apply(WorkflowGraph graph)
        {
            for ( int k = 0; k < Table.Count; ++k ) {
                if ( graph.IsInState(Table[k].State) ) {
                    graph.Transition(Table[k].Transition);
                    return true;
                }
            }
            if (UnknownAction != null)
                UnknownAction();
            return false;
        }


        public bool CanApply(WorkflowGraph graph)
        {
            for (int k = 0; k < Table.Count; ++k) {
                if (graph.CanTransition(Table[k].Transition))
                    return true;
            }
            if (UnknownAction != null)
                UnknownAction();
            return false;
        }



        public static WorkflowRouter Build(IEnumerable<string> states, IEnumerable<string> transitions)
        {
            WorkflowRouter r = new WorkflowRouter();

            string[] s = states.ToArray();
            string[] t = transitions.ToArray();
            for ( int k = 0; k < s.Length; ++k ) {
                Entry e = new Entry() { State = s[k], Transition = t[k] };
                r.Table.Add(e);
            }

            return r;
        }


        public static WorkflowRouter Build(IEnumerable<string> states_transition_pairs)
        {
            WorkflowRouter r = new WorkflowRouter();

            string[] n = states_transition_pairs.ToArray();
            for (int k = 0; k < n.Length; k += 2) {
                Entry e = new Entry() { State = n[k], Transition = n[k+1] };
                r.Table.Add(e);
            }

            return r;
        }

    }
}
