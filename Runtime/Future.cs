using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Silk
{
    /// <summary>
    /// Interface implemented by all Futures.
    /// </summary>
    public interface IFuture
    {
        /// <summary>
        /// True if the future has been fulfilled and has a value.
        /// </summary>
        bool Fulfilled { get; }

        /// <summary>
        /// The type of the future's value.
        /// </summary>
        Type ValueType { get; }
    }

    /// <summary>
    /// A concrete future.
    /// Unfulfilled by default, and prevents accessing unfulfilled values.
    /// </summary>
    /// <remarks>
    /// When using this with Unity serialization,
    /// because Unity does not support serialization for generic types used in conjunction with [SerializeReference],
    /// you probably want to implement a concrete type derived from this class. See <see cref="FutureInt"/> for example.
    /// </remarks>
    /// <typeparam name="T">The type of the value.</typeparam>
    [Serializable]
    public class Future<T> : IFuture
    {
        [SerializeField]
        private bool fulfilled;

        [SerializeField]
        private T value;

        public bool Fulfilled => fulfilled;

        public Type ValueType => typeof(T);

        /// <summary>
        /// When set, marks the future as fulfilled.
        /// When accessed, returns the fulfilled value.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when trying to access an unfulfilled value.</exception>
        public T Value
        {
            get => fulfilled ? value : throw new System.InvalidOperationException("Future has no result yet");
            set
            {
                fulfilled = true;
                this.value = value;
            }
        }
    }

    [Serializable]
    public class FutureBool : Future<bool> { }

    [Serializable]
    public class FutureInt : Future<int> { }

    [Serializable]
    public class FutureFloat : Future<float> { }

    [Serializable]
    public class FutureString : Future<string> { }
}
