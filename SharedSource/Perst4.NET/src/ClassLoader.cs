namespace Perst
{    
    using System;

    /// <summary>
    /// Interface to provide application apecific class loading
    /// </summary>
    public interface ClassLoader 
    {
        /// <summary>
        /// Load class with specified name.
        /// </summary>
        /// <param name="name">full name of the class to be loaded</param>
        /// <returns>loaded class or <b>null</b> if class can not be loaded</returns>
        Type LoadClass(string name);
    }
}
