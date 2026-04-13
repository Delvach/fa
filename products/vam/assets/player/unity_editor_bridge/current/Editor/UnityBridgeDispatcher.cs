using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;

namespace FrameAngel.UnityEditorBridge
{
    [InitializeOnLoad]
    internal static class UnityBridgeDispatcher
    {
        private sealed class WorkItem
        {
            public Func<UnityBridgeResponse> Work;
            public TaskCompletionSource<UnityBridgeResponse> CompletionSource;
        }

        private static readonly Queue<WorkItem> Pending = new Queue<WorkItem>();
        private static readonly int MainThreadId;

        static UnityBridgeDispatcher()
        {
            MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += Pump;
        }

        public static UnityBridgeResponse Invoke(Func<UnityBridgeResponse> work, TimeSpan timeout)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                return work();
            }

            TaskCompletionSource<UnityBridgeResponse> completionSource = new TaskCompletionSource<UnityBridgeResponse>();
            lock (Pending)
            {
                Pending.Enqueue(new WorkItem
                {
                    Work = work,
                    CompletionSource = completionSource
                });
            }

            if (completionSource.Task.Wait(timeout))
            {
                return completionSource.Task.Result;
            }

            return UnityBridgeResponse.Error("MAIN_THREAD_TIMEOUT", "Unity Editor bridge timed out waiting for the main thread.", "");
        }

        private static void Pump()
        {
            while (true)
            {
                WorkItem item = null;
                lock (Pending)
                {
                    if (Pending.Count > 0)
                    {
                        item = Pending.Dequeue();
                    }
                }

                if (item == null)
                {
                    break;
                }

                try
                {
                    item.CompletionSource.SetResult(item.Work());
                }
                catch (Exception ex)
                {
                    item.CompletionSource.SetResult(UnityBridgeResponse.Error("MAIN_THREAD_FAILURE", ex.Message, ""));
                }
            }
        }
    }
}
