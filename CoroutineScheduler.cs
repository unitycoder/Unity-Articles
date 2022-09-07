namespace B83.CustomCoroutines
{
    using System.Collections;
    using System.Collections.Generic;

    public class WaitForSeconds
    {
        public double delay;
        public WaitForSeconds(double aDelay) => delay = aDelay;
    }

    public abstract class CoroutineSchedulerQueue
    {
        public abstract void ScheduleCoroutine(Coroutine aCoroutine, object aYieldValue);
        public abstract void Tick(List<Coroutine> aList);
    }

    public class CoroutineSchedulerWaitQueue : CoroutineSchedulerQueue
    {
        public class Node
        {
            public Coroutine co;
            public double timeout;
            public Node next = null;
        }

        public double currentTime;
        Node first = null;
        
        public override void ScheduleCoroutine(Coroutine aCoroutine, object aYieldValue)
        {
            if (aYieldValue is WaitForSeconds wfs)
            {
                // time sorted linked list
                var n = new Node { co = aCoroutine, timeout = currentTime + wfs.delay };
                if (first == null || first.timeout > n.timeout)
                {
                    n.next = first;
                    first = n;
                    return;
                }
                // make sure the coroutine is placed in the right order
                var a = first;
                while (a.next != null && a.next.timeout < n.timeout)
                    a = a.next;
                n.next = a.next;
                a.next = n;
            }
        }

        public override void Tick(List<Coroutine> aList)
        {
            // since the routines are ordered, they always complete in the right order
            while (first != null && currentTime >= first.timeout)
            {
                aList.Add(first.co);
                first = first.next;
            }
        }
    }

    public class CoroutineSchedulerSimpleQueue : CoroutineSchedulerQueue
    {
        List<Coroutine> list = new List<Coroutine>();
        public System.Type type;
        public override void ScheduleCoroutine(Coroutine aCoroutine, object aYieldValue)
        {
            list.Add(aCoroutine);
        }

        public override void Tick(List<Coroutine> aList)
        {
            aList.AddRange(list);
            list.Clear();
        }
    }

    public class Coroutine
    {
        internal Stack<IEnumerator> m_Routine = new Stack<IEnumerator>();
        Coroutine m_Child = null;
        public bool HasFinished => m_Routine.Count == 0;
        public Coroutine(IEnumerator aRoutine)
        {
            m_Routine.Push(aRoutine);
        }

        internal bool Tick(out object aYieldVal)
        {
            aYieldVal = null;
            if (m_Routine.Count == 0)
                return false;

            // This coroutine is waiting for a child coroutine to finish
            if (m_Child != null)
            {
                if (m_Child.HasFinished)
                    m_Child = null;
                else
                    return true;
            }
            var it = m_Routine.Peek();
            if (it.MoveNext())
            {
                if (it.Current is IEnumerator subIterator)
                {
                    m_Routine.Push(subIterator);
                    return Tick(out aYieldVal);
                }
                else if (it.Current is Coroutine subCoroutine)
                {
                    return subCoroutine.HasFinished;
                }
                aYieldVal = it.Current;
                return true;
            }
            else
            {
                m_Routine.Pop();
                return Tick(out aYieldVal);
            }
        }
        public void Stop() => m_Routine.Clear();
    }

    public class CoroutineScheduler
    {
        Dictionary<System.Type, CoroutineSchedulerQueue> queues = new Dictionary<System.Type, CoroutineSchedulerQueue>();
        CoroutineSchedulerSimpleQueue m_DefaultQueue = new CoroutineSchedulerSimpleQueue();
        List<Coroutine> m_ProcessList = new List<Coroutine>();

        public void AddQueue<T>( CoroutineSchedulerQueue aQueue)
        {
            AddQueue(typeof(T), aQueue);
        }
        public void AddQueue(System.Type aType, CoroutineSchedulerQueue aQueue)
        {
            queues.Add(aType, aQueue);
        }

        public void TickQueue<T>()
        {
            TickQueue(typeof(T));
        }
        public void TickQueue(System.Type aType)
        {
            if (queues.TryGetValue(aType, out CoroutineSchedulerQueue queue) && queue != null)
                TickQueue(queue);
        }

        private void TickQueue(CoroutineSchedulerQueue aQueue)
        {
            aQueue.Tick(m_ProcessList);
            foreach (var co in m_ProcessList)
            {
                try
                {
                    TickCoroutine(co);
                }
                catch (System.Exception e)
                {
                    // Optional: log error here.
                    // if there's an exception in the coroutine, it would simply terminate the innermost IEnumerator
                    co.m_Routine.Pop();
                    if (co.m_Routine.Count > 0)
                        m_DefaultQueue.ScheduleCoroutine(co, null);
                }
            }
            m_ProcessList.Clear();
        }

        internal void TickCoroutine(Coroutine aRoutine)
        {
            if (aRoutine.Tick(out object obj))
            {
                if (obj == null)
                    m_DefaultQueue.ScheduleCoroutine(aRoutine, obj);
                else if (queues.TryGetValue(obj.GetType(), out CoroutineSchedulerQueue queue))
                    queue.ScheduleCoroutine(aRoutine, obj);
            }
        }

        public void TickDefault() => TickQueue(m_DefaultQueue);

        public Coroutine StartCoroutine(IEnumerator aRoutine, bool aStartImmediately = true)
        {
            var co = new Coroutine(aRoutine);
            if (aStartImmediately)
                TickCoroutine(co);
            else
                m_DefaultQueue.ScheduleCoroutine(co, null);
            return co;
        }
    }
}
