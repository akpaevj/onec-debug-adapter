namespace Onec.DebugAdapter.Services
{
    class References<T>
    {
        private readonly object _locker = new();
        private readonly List<T> _items = new();

        public int Add(T item)
        {
            lock (_locker)
            {
                _items.Add(item);
                return _items.Count;
            }
        }

        public T Get(int reference)
        {
            lock (_locker)
                return _items[reference - 1];
        }

        public void Clear(Predicate<T> predicate)
        {
            lock (_locker)
                _items.RemoveAll(predicate);
        }
    }
}
