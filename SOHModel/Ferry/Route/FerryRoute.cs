using System.Collections;

namespace SOHModel.Ferry.Route;

public class FerryRoute : IEnumerable<FerryRouteEntry>
{
    public FerryRoute()
    {
        Entries = [];
    }

    public List<FerryRouteEntry> Entries { get; set; }

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<FerryRouteEntry> GetEnumerator()
    {
        return new FerryRouteEnumerator(Entries);
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

    public class FerryRouteEnumerator : IEnumerator<FerryRouteEntry>
    {
        public FerryRouteEnumerator(List<FerryRouteEntry> entries)
        {
            Entries = entries;
            CurrentIndex = -1;
        }

        public List<FerryRouteEntry> Entries { get; set; }

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
        public FerryRouteEntry Current
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