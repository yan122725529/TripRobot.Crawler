//-< JSQLArithmeticException.java >----------------------------------*--------*
// JSQL                       Version 1.04       (c) 1999  GARRET    *     ?  *
// (Java SQL)                                                        *   /\|  *
//                                                                   *  /  \  *
//                          Created:     10-Dec-2002  K.A. Knizhnik  * / [] \ *
//                          Last update: 10-Dec-2002  K.A. Knizhnik  * GARRET *
//-------------------------------------------------------------------*--------*
// Exception thown in case of incorect operands for integer operations
//-------------------------------------------------------------------*--------*
namespace Perst
{
    /// <summary> Exception thown in case of incorect operands for integer operations
    /// </summary>
    public class JSQLArithmeticException:JSQLRuntimeException
    {
        /// <summary> Constructor of exception
        /// </summary>
        public JSQLArithmeticException(string msg):base(msg, null, null)
        {
        }
    }
}