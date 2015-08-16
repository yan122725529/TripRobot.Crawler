using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst
{
    ///<summary>
    /// Enumerator for moving in both directions
    /// </summary>
#if USE_GENERICS
    public interface IBidirectionalEnumerator<T> : IEnumerator<T> 
#else
    public interface IBidirectionalEnumerator : IEnumerator
#endif
    {
        /// <summary>
        /// Move curent position to the previous element
        /// </summary>
        /// <returns>true if previous element exists, false otherwise
        /// </returns>returns>
        bool MovePrevious();
    }
}
      