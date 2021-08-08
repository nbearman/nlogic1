using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace nlogic_sim
{
    /// <summary>
    /// An associative mapping of intervals of type T to values of type U
    /// </summary>
    public class IntervalTree<T, U> where T : IComparable
    {
        private SortedList<IntervalTreeValue, U> tree;

        public IntervalTree()
        {
            tree = new SortedList<IntervalTreeValue, U>(new IntervalTreeValue.IntervalTreeValueComparer());
        }

        /// <summary>
        /// Insert a new interval [lower_inclusive, upper_exclusive) associated with data
        /// </summary>
        public void insert(T lower_inclusive, T upper_exclusive, U data)
        {
            //invariants: no duplicate intervals, no overlapping intervals, no "value" intervals

            //create an "interval" interval, not a "value" interval
            IntervalTreeValue itv = new IntervalTreeValue(lower_inclusive, upper_exclusive);

            //duplicate intervals cannot exist in tree
            Debug.Assert(!tree.ContainsKey(itv));

            //overlapping intervals will be caught by comparator during insertion
            tree.Add(itv, data);
        }

        public U get_data(T value)
        {
            //invariants: only "value" intervals may be searched for

            //create a "value" interval
            IntervalTreeValue itv = new IntervalTreeValue(value);

            //return the data associated with the interval this value belongs to
            return tree[itv];
        }


        private class IntervalTreeValue
        {
            //true if holds a single value (instead of an interval)
            bool value;

            //lower and upper values of the interval
            //if a this.value == true, the lower_inclusive value will hold its value
            T lower_inlcusive;
            T upper_exclusive;


            /// <summary>
            /// Interval constructor
            /// </summary>
            public IntervalTreeValue(T lower_bound_inclusive, T upper_bound_exclusive)
            {
                //interval must have lower <= upper
                Debug.Assert(lower_bound_inclusive.CompareTo(upper_bound_exclusive) <= 0);

                lower_inlcusive = lower_bound_inclusive;
                upper_exclusive = upper_bound_exclusive;
                value = false;
            }

            /// <summary>
            /// Value constructor
            /// </summary>
            public IntervalTreeValue(T value)
            {
                lower_inlcusive = value;
                upper_exclusive = default(T);
                this.value = true;
            }

            public class IntervalTreeValueComparer : Comparer<IntervalTreeValue>
            {
                public override int Compare(IntervalTree<T, U>.IntervalTreeValue x, IntervalTree<T, U>.IntervalTreeValue y)
                {
                    //should not compare two values
                    Debug.Assert(!(x.value && y.value));

                    //both arguments are ranges
                    if (!x.value && !y.value)
                    {
                        //TODO removed asserting that the ranges do not overlap
                        //(the check didnt work because it assumed the ranges were given in sorted order)
                        //should this assertion be restored?

                        return x.lower_inlcusive.CompareTo(y.lower_inlcusive);
                    }

                    //one argument is a value
                    if (x.value && !y.value)
                    {
                        //return lower than if value is less than lower bound
                        int compare_lower = x.lower_inlcusive.CompareTo(y.lower_inlcusive);
                        if (compare_lower < 0)
                            return -1;

                        //return greater than if value is greater than or equal to upper bound
                        int compare_upper = x.lower_inlcusive.CompareTo(y.upper_exclusive);
                        if (compare_upper >= 0)
                            return 1;

                        //else value is within range, return equal
                        return 0;
                    }

                    //other argument is a value
                    if (!x.value && y.value)
                    {
                        //interval is less than value if upperbound is <= value 
                        int compare_upper = x.upper_exclusive.CompareTo(y.lower_inlcusive);
                        if (compare_upper <= 0)
                            return -1;

                        //interval is greater than value if lower bound > value
                        int compare_lower = x.lower_inlcusive.CompareTo(y.lower_inlcusive);
                        if (compare_lower > 0)
                            return 1;

                        //else value is within interval, return equal
                        return 0;
                    }

                    //should not be reached
                    Debug.Assert(false);
                    return 0;
                }

            }


        }
    }

}