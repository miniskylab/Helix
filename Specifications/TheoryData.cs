using System.Collections;
using System.Collections.Generic;

namespace Helix.Specifications.Core
{
    public abstract class AbstractTheoryData : IEnumerable<object[]>
    {
        readonly List<object[]> _theoryData = new List<object[]>();

        public IEnumerator<object[]> GetEnumerator() => _theoryData.GetEnumerator();

        protected void AddRow(params object[] values) { _theoryData.Add(values); }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class TheoryData<T1> : AbstractTheoryData
    {
        protected void Add(T1 p1 = default) { AddRow(p1); }
    }

    public class TheoryData<T1, T2> : AbstractTheoryData
    {
        protected void Add(T1 p1 = default, T2 p2 = default) { AddRow(p1, p2); }
    }

    public class TheoryData<T1, T2, T3> : AbstractTheoryData
    {
        protected void Add(T1 p1 = default, T2 p2 = default, T3 p3 = default) { AddRow(p1, p2, p3); }
    }

    public class TheoryData<T1, T2, T3, T4> : AbstractTheoryData
    {
        protected void Add(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default) { AddRow(p1, p2, p3, p4); }
    }

    public class TheoryData<T1, T2, T3, T4, T5> : AbstractTheoryData
    {
        protected void Add(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default, T5 p5 = default)
        {
            AddRow(p1, p2, p3, p4, p5);
        }
    }

    public class TheoryData<T1, T2, T3, T4, T5, T6> : AbstractTheoryData
    {
        protected void Add(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default, T5 p5 = default, T6 p6 = default)
        {
            AddRow(p1, p2, p3, p4, p5, p6);
        }
    }

    public class TheoryData<T1, T2, T3, T4, T5, T6, T7> : AbstractTheoryData
    {
        protected void Add(T1 p1 = default, T2 p2 = default, T3 p3 = default, T4 p4 = default, T5 p5 = default, T6 p6 = default,
            T7 p7 = default)
        {
            AddRow(p1, p2, p3, p4, p5, p6, p7);
        }
    }
}