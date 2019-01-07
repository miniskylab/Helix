using System.Collections;
using System.Collections.Generic;

namespace Helix.Specifications
{
    public class TheoryDescription<T1> : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryDescription = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryDescription.GetEnumerator();

        protected void AddTheoryDescription(T1 p1 = default) { _theoryDescription.Add(new object[] { p1 }); }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TheoryDescription<T1, T2> : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryDescription = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryDescription.GetEnumerator();

        protected void AddTheoryDescription(T1 p1 = default, T2 p2 = default) { _theoryDescription.Add(new object[] { p1, p2 }); }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TheoryDescription<T1, T2, T3> : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryDescription = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryDescription.GetEnumerator();

        protected void AddTheoryDescription(T1 p1 = default, T2 p2 = default, T3 p3 = default)
        {
            _theoryDescription.Add(new object[] { p1, p2, p3 });
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TheoryDescription<T1, T2, T3, T4> : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryDescription = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryDescription.GetEnumerator();

        protected void AddTheoryDescription(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default)
        {
            _theoryDescription.Add(new object[] { p1, p2, p3, p4 });
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TheoryDescription<T1, T2, T3, T4, T5> : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryDescription = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryDescription.GetEnumerator();

        protected void AddTheoryDescription(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default, T5 p5 = default)
        {
            _theoryDescription.Add(new object[] { p1, p2, p3, p4, p5 });
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TheoryDescription<T1, T2, T3, T4, T5, T6> : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryDescription = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryDescription.GetEnumerator();

        protected void AddTheoryDescription(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default, T5 p5 = default,
            T6 p6 = default)
        {
            _theoryDescription.Add(new object[] { p1, p2, p3, p4, p5, p6 });
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TheoryDescription<T1, T2, T3, T4, T5, T6, T7> : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryDescription = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryDescription.GetEnumerator();

        protected void AddTheoryDescription(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default, T5 p5 = default,
            T6 p6 = default, T7 p7 = default)
        {
            _theoryDescription.Add(new object[] { p1, p2, p3, p4, p5, p6, p7 });
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}