using System.Collections;

namespace SOHModel.Bus.Route;

public class BusRoute : IEnumerable<BusRouteEntry>
{
    public List<BusRouteEntry> Entries { get; set; } = [];

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<BusRouteEntry> GetEnumerator()
    {
        return new BusRouteEnumerator(Entries);
    }

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>
    ///     An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the
    ///     collection.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Generates a <code>BusRoute</code> that is reversed.
    /// </summary>
    /// <returns>
    ///     A <code>BusRoute</code> that defines a route that starts at the original end station and finishes
    ///     at the original start station.
    /// </returns>
    public BusRoute Reversed()
    {
        var reversed = new BusRoute();
        for (var index = Entries.Count - 1; index >= 0; index--)
        {
            var entry = Entries[index];
            reversed.Entries.Add(new BusRouteEntry(entry.To, entry.From, entry.Minutes));
        }

        return reversed;
    }

    public class BusRouteEnumerator : IEnumerator<BusRouteEntry>
    {
        public BusRouteEnumerator(List<BusRouteEntry> entries)
        {
            Entries = entries;
            CurrentIndex = -1;
        }

        public List<BusRouteEntry> Entries { get; set; }

        public int CurrentIndex { get; set; }

        /// <summary>Advances the enumerator to the next element of the collection.</summary>
        /// <returns>
        ///     true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the
        ///     end of the collection.
        /// </returns>
        /// <exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (CurrentIndex >= Entries.Count - 1) return false;

            CurrentIndex++;
            return true;
        }

        /// <summary>Sets the enumerator to its initial position, which is before the first element in the collection.</summary>
        /// <exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created.</exception>
        public void Reset()
        {
            CurrentIndex = -1;
        }

        /// <summary>Gets the element in the collection at the current position of the enumerator.</summary>
        /// <returns>The element in the collection at the current position of the enumerator.</returns>
        public BusRouteEntry Current
        {
            get
            {
                if (CurrentIndex < 0 || CurrentIndex >= Entries.Count) return null;
                return Entries[CurrentIndex];
            }
        }

        /// <summary>Gets the element in the collection at the current position of the enumerator.</summary>
        /// <returns>The element in the collection at the current position of the enumerator.</returns>
        object IEnumerator.Current => Current;

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            Entries = null;
            CurrentIndex = -1;
        }
    }
}