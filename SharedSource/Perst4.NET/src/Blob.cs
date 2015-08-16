namespace Perst
{    
    /// <summary>
	/// Interface to store/fetch large binary objects
	/// </summary>
	public interface Blob : IPersistent, IResource 
	{
        /// <summary>
        /// Get stream to fetch/store BLOB data 
        /// </summary>
        /// <returns>BLOB read/write stream</returns>
        System.IO.Stream GetStream();
	}
}
