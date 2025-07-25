// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Linq
{
    // NOTE: DO NOT DELETE THE FOLLOWING DEBUG VIEW TYPES.
    // Although it might be tempting due to them not be referenced anywhere in this library,
    // Visual Studio (and potentially other debuggers) currently depends on their existence to
    // enable the "Results" view in watch windows.  Specifically:
    // - It creates the debug view by name (the view then has the same requirements as other views).
    // - It looks for the empty exception by name.
    // - It uses the exception's Empty property by name.

    /// <summary>
    /// This class provides the items view for the Enumerable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class SystemCore_EnumerableDebugView<T>
    {
        public SystemCore_EnumerableDebugView(IEnumerable<T> enumerable)
        {
            if (enumerable is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.enumerable);
            }

            _enumerable = enumerable;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get
            {
                T[] array = _enumerable.ToArray();
                if (array.Length == 0)
                {
                    throw new SystemCore_EnumerableDebugViewEmptyException();
                }

                return array;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly IEnumerable<T> _enumerable;
    }

    internal sealed class SystemCore_EnumerableDebugViewEmptyException : Exception
    {
#pragma warning disable CA1822
        // This property value is used by the debugger EE as the message
        // displayed when a dynamic object has no members.
        public string Empty => SR.EmptyEnumerable;
#pragma warning restore CA1822
    }

    internal sealed class SystemCore_EnumerableDebugView
    {
        public SystemCore_EnumerableDebugView(IEnumerable enumerable)
        {
            if (enumerable is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.enumerable);
            }

            _enumerable = enumerable;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public object?[] Items
        {
            get
            {
                List<object?> tempList = [.. _enumerable];

                if (tempList.Count == 0)
                {
                    throw new SystemCore_EnumerableDebugViewEmptyException();
                }

                return tempList.ToArray();
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly IEnumerable _enumerable;
    }

    internal sealed class SystemLinq_GroupingDebugView<TKey, TElement>
    {
        private readonly Grouping<TKey, TElement> _grouping;

        public SystemLinq_GroupingDebugView(Grouping<TKey, TElement> grouping)
        {
            _grouping = grouping;
        }

        public TKey Key => _grouping.Key;

        // The name of this property must alphabetically follow `Key` so the elements appear last in the display.
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public TElement[] Values => field ??= _grouping.ToArray();
    }

    internal sealed class SystemLinq_LookupDebugView<TKey, TElement>
    {
        private readonly ILookup<TKey, TElement> _lookup;

        public SystemLinq_LookupDebugView(ILookup<TKey, TElement> lookup)
        {
            _lookup = lookup;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IGrouping<TKey, TElement>[] Groupings => field ??= _lookup.ToArray();
    }
}
