//-< CompileError.java >---------------------------------------------*--------*
// JSQL                       Version 1.04       (c) 1999  GARRET    *     ?  *
// (Java SQL)                                                        *   /\|  *
//                                                                   *  /  \  *
//                          Created:      5-Mar-99    K.A. Knizhnik  * / [] \ *
//                          Last update:  6-Mar-99    K.A. Knizhnik  * GARRET *
//-------------------------------------------------------------------*--------*
// Exception thrown by compiler
//-------------------------------------------------------------------*--------*
namespace Perst
{
	using System;
	
	/// <summary> Exception thrown by compiler
	/// </summary>
	public class CompileError:Exception
	{
		public CompileError(string msg, int pos):base(msg + " in position " + pos)
		{
		}
	}
}