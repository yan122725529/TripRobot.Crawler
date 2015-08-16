//-< CompileError.cs >----------------------------------------------*--------*
// JSQL                       Version 1.04       (c) 1999  GARRET    *     ?  *
// (Java SQL)                                                        *   /\|  *
//                                                                   *  /  \  *
//                          Created:     23-Mar-2009  K.A. Knizhnik  * / [] \ *
//                          Last update: 23-Mar-2009  K.A. Knizhnik  * GARRET *
//-------------------------------------------------------------------*--------*
// Exception thrown by code generator
//-------------------------------------------------------------------*--------*

namespace Perst
{
    using System;    

    /// <summary>
    /// Exception thrown by code generator
    /// </summary>
    public class CodeGeneratorException : Exception 
    { 
        public  CodeGeneratorException(string msg) 
        : base(msg)
        {
        }
    }
}