using System;
#if USE_GENERICS
using System.Collections.Generic;
#else
using System.Collections;
#endif

namespace Perst
{
    ///<summary>
    /// Interface for persistent dictionary.
    /// </summary>
#if USE_GENERICS
    public interface IPersistentMap<K,V> : IPersistent, IResource, IDictionary<K,V> where V:class
#else
    public interface IPersistentMap : IPersistent, IResource, IDictionary
#endif
    {  
    }
}