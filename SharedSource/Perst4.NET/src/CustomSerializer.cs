using System;
using System.IO;

namespace Perst
{
    /// <summary>
    /// Writer allowing to serialize persistent object components
    /// </summary>
    public abstract class ObjectWriter : BinaryWriter
    {      
        /// <summary>
        /// Serialize reference to persistent object
        /// </summary>
        public abstract void WriteObject(object obj);

        public ObjectWriter(Stream stream) : base(stream) {}
    }

    /// <summary>
    /// Reader allowing to deserialize persistent object components
    /// </summary>
    public abstract class ObjectReader : BinaryReader
    {      
        /// <summary>
        /// Deserialize reference to persistent object
        /// </summary>
        public abstract object ReadObject();

        public ObjectReader(Stream stream) : base(stream) {}
    }

    /// <summary>
    /// Interface of custome serializer
    /// </summary>
    public interface CustomSerializer
    {
        /// <summary>
        /// Serialize object
        /// </summary>
        /// <param name="obj">object to be packed</param>
        /// <param name="writer">writer to be used for object serialization</param>
        ///
        void Pack(object obj, ObjectWriter writer);

        /// <summary>
        /// Create and deserialize object
        /// </summary>
        /// <param name="reader">reader to be used for object deserialization</param>
        /// <returns>created and unpacked object</returns>
        ///
        object Unpack(ObjectReader reader);

        /// <summary>
        /// Deserialize object
        /// </summary>
        /// <param name="obj">unpacked object</param>
        /// <param name="reader">reader to be used for object deserialization</param>
        ///
        void Unpack(object obj, ObjectReader reader);


        /// <summary>
        /// Create instance of specified type
        /// </summary>    
        /// <param name="type">type of the created object</param>        
       /// <returns>created object</returns>
        object Create(Type type);

        /// <summary>
        /// Create object from its string representation
        /// </summary>
        /// <param name="str">string representation of object (created by ToString() method)</param>
        ///
        object Parse(string str);

        /// <summary>
        /// Get string representation of the object
        /// </summary>
        /// <param name="obj">object which string representation is taken</param>
        ///
        string Print(object obj);

        /// <summary>
        /// Check if serializer can pack objects of this type
        /// </summary>
        /// <param name="type">inspected object type</param>
        /// <returns>true if serializer can pack instances of this type</returns>
        ///
        bool IsApplicable(Type type);

        /// <summary>
        /// Check if serializer can pack this object component
        /// </summary>
        /// <param name="obj">object component to be packed</param>
        /// <returns>true if serializer can pack this object inside some other object</returns>
        ///
        bool IsEmbedded(object obj);
    }
}
