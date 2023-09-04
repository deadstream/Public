using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Caspar
{
    public class Dice
    {


        //static ThreadLocal<System.Random> random = new ThreadLocal<System.Random>(() => { return MersenneTwister.DsfmtRandom.Create(MersenneTwister.DsfmtEdition.Original_19937); });
        static ThreadLocal<System.Random> random = new ThreadLocal<System.Random>(() => { return MersenneTwister.MTRandom.Create(MersenneTwister.MTEdition.Original_19937); });
        //static System.Random random = MersenneTwister.MTRandom.Create(MersenneTwister.MTEdition.Original_19937);
        static public int Roll(int from, int to)
        {
            if (from == to) { return roll(); }
            return random.Value.Next(from, to);

        }

        static public double Roll(double from, double to)
        {
            return random.Value.NextDouble() * (to - from) + from;
        }

        static protected int roll()
        {
            return random.Value.Next();
        }

        static public int Roll(int max)
        {
            return random.Value.Next(max);
        }

        public static double Roll()
        {
            return random.Value.NextDouble();
        }
        public interface IBucket<T>
        {
            void Insert(T value, int per);
            void Shuffle();
            void Clear();
            int Count { get; }
            T Pick();
        }

        public sealed class PopBucket<T> : IBucket<T>
        {

            private List<Tuple<T, int>> originals = new List<Tuple<T, int>>();
            private ThreadLocal<HashSet<Tuple<T, int>>> picked =
                new ThreadLocal<HashSet<Tuple<T, int>>>(() => { return new HashSet<Tuple<T, int>>(); });
            private ThreadLocal<SortedDictionary<int, Tuple<T, int>>> candidates = new ThreadLocal<SortedDictionary<int, Tuple<T, int>>>(() => { return new SortedDictionary<int, Tuple<T, int>>(); });
            private ThreadLocal<int> MaxPER = new ThreadLocal<int>();
            public int Count { get { return originals.Count; } }

            public void Shuffle()
            {
                picked.Value.Clear();
                candidates.Value.Clear();

                MaxPER.Value = 0;

                var array = originals.ToArray();

                array.Shuffle();

                foreach (var e in array)
                {
                    if (e.Item2 == 0) { return; }
                    MaxPER.Value += e.Item2;
                    candidates.Value.Add(MaxPER.Value, e);

                }

            }
            public void Insert(T value, int per)
            {

                if (per == 0) { return; }
                originals.Add(new Tuple<T, int>(value, per));
            }

            public void Clear()
            {
                originals.Clear();
                picked.Value.Clear();
                candidates.Value.Clear();
            }

            public T Pick()
            {

                if (candidates.Value.Count == 0) { return default(T); }
                var dice = global::Framework.Caspar.Dice.Roll(0, MaxPER.Value);
                var pick = candidates.Value.First(e => e.Key >= dice).Value;

                picked.Value.Add(pick);

                candidates.Value.Clear();
                MaxPER.Value = 0;

                foreach (var e in originals)
                {
                    if (picked.Value.Contains(e) == true) { continue; }
                    MaxPER.Value += e.Item2;
                    candidates.Value.Add(MaxPER.Value, e);
                }

                return pick.Item1;


            }

        }
        public sealed class Bucket<T> : IBucket<T>
        {
            public class Slot
            {
                public T Value;
                public int PER;
                public double Rate(int Max)
                {
                    return (double)PER / (double)Max;
                }
            }

            private List<Slot> origin = new();
            private SortedDictionary<double, T> shuffled = new();
            public int MaxPER { get; set; }

            public int Count { get { return origin.Count; } }
            public void Insert(T value, int per)
            {
                if (per == 0) { return; }
                MaxPER += per;
                origin.Add(new Slot()
                {
                    Value = value,
                    PER = MaxPER,
                });

            }
            public T Pick()
            {
                if (origin.Count == 0) { return default(T); }
                var dice = global::Framework.Caspar.Dice.Roll();

                var temp = shuffled;
                var picked = temp.First(e => e.Key >= dice).Value;
                return picked;
            }

            public void Shuffle()
            {
                SortedDictionary<double, T> other = new();
                foreach (var e in origin)
                {
                    other.Add(e.Rate(MaxPER), e.Value);
                }
                shuffled = other;

            }
            public void Clear()
            {
                origin.Clear();
            }

        }

        public sealed class Fixed<T> : IBucket<T>
        {
            private ThreadLocal<int> Index = new ThreadLocal<int>();
            private List<T> origins = new List<T>();
            public void Shuffle()
            {
                Index.Value = 0;
            }
            public int Count { get { return origins.Count; } }

            public void Insert(T value, int per)
            {
                origins.Add(value);
            }
            public void Clear()
            {
                origins.Clear();
            }

            public T Pick()
            {
                if (Index.Value >= origins.Count) { return default(T); }
                return origins[Index.Value++];
            }

        }
    }
}
