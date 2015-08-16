//-< JSQLRuntimeException.java >-------------------------------------*--------*
// JSQL                       Version 1.04       (c) 1999  GARRET    *     ?  *
// (Java SQL)                                                        *   /\|  *
//                                                                   *  /  \  *
//                          Created:      9-Dec-2002  K.A. Knizhnik  * / [] \ *
//                          Last update:  9-Dec-2002  K.A. Knizhnik  * GARRET *
//-------------------------------------------------------------------*--------*
// Exception thown by JSQL at runtime
//-------------------------------------------------------------------*--------*
namespace Perst
{
    using System;
	
    /// <summary> Exception thown by JSQL at runtime which should be ignored and boolean expression caused this
    /// exption should be treated as false
    /// </summary>
    public class JSQLRuntimeException:System.Exception
    {
        /// <summary> Get class in which lookup was performed
        /// </summary>
        virtual public Type Target
        {
            get
            {
                return target;
            }
			
        }
        /// <summary> Get name of the field
        /// </summary>
        virtual public string FieldName
        {
            get
            {
                return fieldName;
            }
			
        }
        /// <summary> Constructor of exception
        /// </summary>
        /// <param name="target">class of the target object in which field was not found
        /// </param>
        /// <param name="fieldName">name of the locate field
        /// 
        /// </param>
        public JSQLRuntimeException(string message, Type target, string fieldName)
            :base(message)
        {
            this.target = target;
            this.fieldName = fieldName;
        }
		
		
		
        internal string fieldName;
        internal Type   target;
    }
}