// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    // Note: This type must have the same layout as the CLR's VARARGS type in CLRVarArgs.h.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe ref partial struct ArgIterator
    {
        private IntPtr _argCookie;              // Cookie from the EE.

        [StructLayout(LayoutKind.Sequential)]
        private struct SigPointer
        {
            internal IntPtr _ptr;
            internal uint _len;
        }
        private SigPointer _sigPtr;             // Pointer to remaining signature.

        private IntPtr _argPtr;                 // Pointer to remaining args.
        private int _remainingArgs;             // # of remaining args.

#if TARGET_WINDOWS // Native Varargs are not supported on Unix
        // ArgIterator is a ref struct. It does not require pinning, therefore Unsafe.AsPointer is safe.
        // This method null checks the this pointer as a side-effect.
        private ArgIterator* ThisPtr => (ArgIterator*)Unsafe.AsPointer(ref _argCookie);

        // create an arg iterator that points at the first argument that
        // is not statically declared (that is the first ... arg)
        // 'arglist' is the value returned by the ARGLIST instruction
        public ArgIterator(RuntimeArgumentHandle arglist)
        {
            IntPtr cookie = arglist.Value;
            if (cookie == 0)
                throw new ArgumentException(SR.InvalidOperation_HandleIsNotInitialized);
            Init(ThisPtr, cookie);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "ArgIterator_Init")]
        private static partial void Init(ArgIterator* thisPtr, IntPtr cookie);

        // create an arg iterator that points just past 'firstArg'.
        // 'arglist' is the value returned by the ARGLIST instruction
        // This is much like the C va_start macro

        [CLSCompliant(false)]
        public ArgIterator(RuntimeArgumentHandle arglist, void* ptr)
        {
            IntPtr cookie = arglist.Value;
            if (cookie == 0)
                throw new ArgumentException(SR.InvalidOperation_HandleIsNotInitialized);
            Init(ThisPtr, cookie, ptr);
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "ArgIterator_Init2")]
        private static partial void Init(ArgIterator* thisPtr, IntPtr cookie, void* ptr);

        // Fetch an argument as a typed referece, advance the iterator.
        // Throws an exception if past end of argument list
        [CLSCompliant(false)]
        public TypedReference GetNextArg()
        {
            if (_argCookie == 0)
            {
                // This ArgIterator was created by marshaling from an unmanaged va_list -
                // can't do this operation
                ThrowHelper.ThrowNotSupportedException();
            }

            // Make sure there are remaining args.
            if (_remainingArgs == 0)
            {
                throw new InvalidOperationException(SR.InvalidOperation_EnumEnded);
            }

            TypedReference result = default;
            GetNextArg(ThisPtr, &result);
            return result;
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "ArgIterator_GetNextArg")]
        private static partial void GetNextArg(ArgIterator* thisPtr, TypedReference* pResult);

        // Alternate version of GetNextArg() intended primarily for IJW code
        // generated by VC's "va_arg()" construct.
        [CLSCompliant(false)]
        public TypedReference GetNextArg(RuntimeTypeHandle rth)
        {
            if (_sigPtr._ptr != IntPtr.Zero)
            {
                // This is an ordinary ArgIterator capable of determining
                // types from a signature. Just do a regular GetNextArg.
                return GetNextArg();
            }

            // Prevent abuse of this API with a default ArgIterator (it
            // doesn't require permission to create a zero-inited value
            // type). Check that _argPtr isn't zero or this API will allow a
            // malicious caller to increment the pointer to an arbitrary
            // location in memory and read the contents.
            if (_argPtr == IntPtr.Zero)
            {
                throw new ArgumentNullException(null);
            }

            if (rth.IsNullHandle())
            {
                throw new ArgumentNullException(nameof(rth), SR.Arg_InvalidHandle);
            }

            TypedReference result = default;
            GetNextArg(ThisPtr, new QCallTypeHandle(ref rth), &result);
            return result;
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "ArgIterator_GetNextArg2")]
        private static partial void GetNextArg(ArgIterator* thisPtr, QCallTypeHandle rth, TypedReference* pResult);

        // This method should invalidate the iterator (va_end). It is not supported yet.
        public void End()
        {
        }

        public int GetRemainingCount()
        {
            if (_argCookie == 0)
            {
                // This ArgIterator was created by marshaling from an unmanaged va_list -
                // can't do this operation
                ThrowHelper.ThrowNotSupportedException();
            }
            return _remainingArgs;
        }

        // Gets the type of the current arg, does NOT advance the iterator
        public unsafe RuntimeTypeHandle GetNextArgType()
        {
            return RuntimeTypeHandle.FromIntPtr(GetNextArgType(ThisPtr));
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "ArgIterator_GetNextArgType")]
        private static partial IntPtr GetNextArgType(ArgIterator* thisPtr);

        public override int GetHashCode()
        {
            return HashCode.Combine(_argCookie);
        }

        // Inherited from object
        public override bool Equals(object? o)
        {
            throw new NotSupportedException(SR.NotSupported_NYI);
        }
#else
        public ArgIterator(RuntimeArgumentHandle arglist)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        [CLSCompliant(false)]
        public unsafe ArgIterator(RuntimeArgumentHandle arglist, void* ptr)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        public void End()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        public override bool Equals(object? o)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        public override int GetHashCode()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        [CLSCompliant(false)]
        public System.TypedReference GetNextArg()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        [CLSCompliant(false)]
        public System.TypedReference GetNextArg(System.RuntimeTypeHandle rth)
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        public unsafe System.RuntimeTypeHandle GetNextArgType()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }

        public int GetRemainingCount()
        {
            throw new PlatformNotSupportedException(SR.PlatformNotSupported_ArgIterator); // https://github.com/dotnet/runtime/issues/7317
        }
#endif // TARGET_WINDOWS
    }
}
