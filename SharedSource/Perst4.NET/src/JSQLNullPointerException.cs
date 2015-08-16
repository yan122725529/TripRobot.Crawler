//-< JSQLNullPointerException.java >---------------------------------*--------*
// JSQL                       Version 1.04       (c) 1999  GARRET    *     ?  *
// (Java SQL)                                                        *   /\|  *
//                                                                   *  /  \  *
//                          Created:      7-Dec-2002  K.A. Knizhnik  * / [] \ *
//                          Last update:  9-Dec-2002  K.A. Knizhnik  * GARRET *
//-------------------------------------------------------------------*--------*
// Exception thown when null reference field is dereferenced
//-------------------------------------------------------------------*--------*
namespace Perst
{
    using System;
	
    /// <summary> Exception thown when null reference field is dereferenced
    /// </summary>
    public class JSQLNullPointerException:JSQLRuntimeException
    {
        /// <summary> Constructor of exception
        /// </summary>
        /// <param name="target">class of the target object in which field was not found
        /// </param>
        /// <param name="fieldName">name of the locate field
        /// 
        /// </param>
        public JSQLNullPointerException(Type target, string fieldName)
            :base("Dereferencing null reference ", target, fieldName)
        {
        }
    }
}