using System.Collections.Generic;

namespace Dissonance.Networking
{
    /// <summary>
    /// All the state to do with a remote player we are receiving audio from
    /// </summary>
    internal class ReceivingState
    {
        public int ExpectedChannelSession;
        public ushort PlayerId;
        public ushort BaseSequenceNumber;
        public long LastReceiptTicks;
        public uint LocalSequenceNumber;
        public bool Open;

        public readonly Dictionary<int, int> ExpectedPerChannelSessions = new Dictionary<int, int>();

        /// <summary>
        /// Remove all items from ExpectedPerChannelSessions which is not in the given list
        /// </summary>
        /// <param name="keysToKeep"></param>
        public void ClearChannels(List<int> keysToKeep)
        {
            var count = keysToKeep.Count;
            keysToKeep.Sort();

            using (var e = ExpectedPerChannelSessions.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    var item = e.Current;

                    var ti = keysToKeep.BinarySearch(0, count, item.Key, Comparer<int>.Default);

                    //We didn't find this item in the list, add it
                    if (ti < 0)
                        keysToKeep.Add(item.Key);
                }
            }

            //Everything above count is an item we want to remove
            for (var i = count; i < keysToKeep.Count; i++)
                ExpectedPerChannelSessions.Remove(keysToKeep[i]);
        }
    }
}
