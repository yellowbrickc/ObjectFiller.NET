using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tynamix.ObjectFiller
{
    using System.Threading;

    internal static class ParallelExtension
    {
        /// <summary>
        /// Enumerates through each item in a list in parallel
        /// </summary>
        internal static void ParallelForEach<T>(this IEnumerable<T> list, Action<T> action)
        {
            // enumerate the list so it can't change during execution
            // TODO: why is this happening?
            list = list.ToArray();
            var count = list.Count();

            if (count == 0)
            {
                return;
            }
            else if (count == 1)
            {
                // if there's only one element, just execute it
                action(list.First());
            }
            else
            {
                // Launch each method in it's own thread
                const int MaxHandles = 64;
                for (var offset = 0; offset <= count / MaxHandles; offset++)
                {
                    // break up the list into 64-item chunks because of a limitiation in WaitHandle
                    var chunk = list.Skip(offset * MaxHandles).Take(MaxHandles);

                    // Initialize the reset events to keep track of completed threads
                    var resetEvents = new ManualResetEvent[chunk.Count()];

                    // spawn a thread for each item in the chunk
                    int i = 0;
                    foreach (var item in chunk)
                    {
                        resetEvents[i] = new ManualResetEvent(false);
                        ThreadPool.QueueUserWorkItem(new WaitCallback((object data) =>
                        {
                            int methodIndex = (int)((object[])data)[0];

                            // Execute the method and pass in the enumerated item
                            action((T)((object[])data)[1]);

                            // Tell the calling thread that we're done
                            resetEvents[methodIndex].Set();
                        }), new object[] { i, item });
                        i++;
                    }

                    // Wait for all threads to execute
                    WaitHandle.WaitAll(resetEvents);
                }
            }
        }

        internal static void ParallelFor(Action action, int count)
        {
            var resetEvents = new ManualResetEvent[count];

            for (int i = 0; i < count; i++)
            {
                resetEvents[i] = new ManualResetEvent(false);
                ThreadPool.QueueUserWorkItem(
                                            (data) =>
                                            {
                                                int methodIndex = (int)data;
                                                action();
                                                resetEvents[methodIndex].Set();
                                            }, i);
            }

            // Wait for all threads to execute
            foreach (ManualResetEvent manualResetEvent in resetEvents)
            {
                manualResetEvent.WaitOne();
            }
        }
    }
}
