//-< Resolver.java >-------------------------------------------------*--------*
// JSQL                       Version 1.04       (c) 1999  GARRET    *     ?  *
// (Java SQL)                                                        *   /\|  *
//                                                                   *  /  \  *
//                          Created:     10-Dec-2002  K.A. Knizhnik  * / [] \ *
//                          Last update: 10-Dec-2002  K.A. Knizhnik  * GARRET *
//-------------------------------------------------------------------*--------*
// Abstraction of class resolver which can be used in JSQL
// Resolver can be used to replaced SQL JOINs: given object ID,
// it will provide reference to the resolved object
//-------------------------------------------------------------------*--------*
namespace Perst
{	
	/// <summary> Abstraction of class resolver.
	/// Resolver can be used to replaced SQL JOINs: given object ID, 
	/// it will provide reference to the resolved object
	/// </summary>
	public interface Resolver
		{
			/// <summary> Resolve object
			/// </summary>
			/// <param name="obj">original object to be resolved
			/// </param>
			/// <returns> resolved object
			/// </returns>
			object Resolve(object obj);
		}
}