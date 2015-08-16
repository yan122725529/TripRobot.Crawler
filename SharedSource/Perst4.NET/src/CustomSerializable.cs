using System;

namespace Perst
{
    /// <summary>
    /// Interface used to mark objects serialized using custom serializer
    /// </summary>
    public interface CustomSerializable
    {
        ///
        /// <summary>
        /// Get string representation of object. This string representation may be used
        /// by CustomSerailize.parse method to create new instance of this object
        /// </summary>
        /// <returns>string representation of object</returns>
        ///
        String ToString();
    }
}
