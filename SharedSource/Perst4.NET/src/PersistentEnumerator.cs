namespace Perst
{
    using System.Collections;

    /// <summary>Interface implemented by all Perst enumerators allowing to get Oid of the current object
    /// </summary>
    public interface PersistentEnumerator:IEnumerator       
    {
        /// <summary>Get OID of the current object</summary>
        int CurrentOid 
        {
            get;
        }
    }
}
