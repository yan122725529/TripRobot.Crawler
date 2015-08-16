namespace Perst
{
    using System;

    /// <summary>
    /// Interface of the classes sleft responsible for their serialization
    /// </summary>
    public interface ISelfSerializable
    {
        /// <summary>
        /// Serialize object
        /// </summary>
        /// <param name="writer">writer to be used for object serialization</param>
        ///        
        void Pack(ObjectWriter writer);

        /// <summary>
        /// Deserialize object
        /// </summary>
        /// <param name="reader">reader to be used for objet deserialization</param>
        ///
        void Unpack(ObjectReader reader);
    }
}