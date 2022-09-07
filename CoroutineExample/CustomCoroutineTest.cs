using System.Collections;
using UnityEngine;

namespace B83.UnityTestComponent
{
    using B83.CustomCoroutines;

    public class CustomCoroutineTest : UnityEngine.MonoBehaviour
    {
        public CoroutineScheduler m_Scheduler = new CoroutineScheduler();
        public CoroutineSchedulerWaitQueue m_WaitQueue;
        void Awake()
        {
            m_Scheduler.AddQueue<WaitForSeconds>(m_WaitQueue = new CoroutineSchedulerWaitQueue());
            m_Scheduler.AddQueue<WaitForFixedUpdate>(new CoroutineSchedulerSimpleQueue());
            m_Scheduler.StartCoroutine(TestCoroutine());
        }

        void Update()
        {
            // handles yield return null;
            m_Scheduler.TickDefault();

            // handles yield return new WaitForSeconds();
            m_WaitQueue.currentTime += Time.deltaTime;
            m_Scheduler.TickQueue<WaitForSeconds>();
        }
        void FixedUpdate()
        {
            // handles yield return new WaitForFixedUpdate();
            m_Scheduler.TickQueue<WaitForFixedUpdate>();
        }

        IEnumerator TestCoroutine()
        {
            Debug.Log("TestCoroutine started");
            yield return new WaitForSeconds(2);
            Debug.Log("2 seconds later");
            for (float f = 0; f < 1f; f += Time.deltaTime)
            {
                Debug.Log("Lerp up to 1: " + f);
                yield return null;
            }

            yield return SubRoutine();

            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                Debug.Log("This executes during FixedUpdate, dt: " + Time.deltaTime);
            }
            yield return null;
            Debug.Log("back to update");
        }

        IEnumerator SubRoutine()
        {
            Debug.Log("Sub routine");
            yield return new WaitForSeconds(2);
            Debug.Log("counting down");
            for (int i = 10; i >= 0; i--)
            {
                Debug.Log(">> " + i);
                yield return new WaitForSeconds(1);
            }
        }
    }
}
